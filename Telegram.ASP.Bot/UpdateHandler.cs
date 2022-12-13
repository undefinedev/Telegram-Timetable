using System.Linq.Expressions;
using System.Net.Mime;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.DB;
using Telegram.Lang;

namespace Telegram.UpdateHandler;

public class UpdateHandler : IUpdateHandler
{
    
    private readonly ITelegramBotClient _botClient;
    private readonly ILogger<UpdateHandler> _logger;
    
    public UpdateHandler(ITelegramBotClient botClient, ILogger<UpdateHandler> logger)
    {
        _botClient = botClient;
        _logger = logger;
    }

    public async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        var hand = update switch
        {
            { Message: { } message } => MessageHandler(message, cancellationToken),
            { CallbackQuery: { } callbackQuery } => CallbackHandler(callbackQuery, cancellationToken),
            _ => UnknownHandler(update, cancellationToken)
        };

        await hand;
    }

    private async Task MessageHandler(Message message, CancellationToken cancellationToken)
    {
        _logger.LogInformation($"Received '{message.Text}' message in chat {message.Chat.Id}.");
        if (message.Text is not { } messageText)
        {
            return;
        }

        TelegramDB.User? user;
        try
        {
            await using var db = new TelegramDB();
            user = await db.Users.FindAsync(new object?[] { message.From!.Id }, cancellationToken: cancellationToken);
            if (user is null)
            {
                Message res;
                if (message.From.LanguageCode is { } lang)
                {
                    if (!Text.SupportedLanguages.Contains(lang)) lang = "en";
                    db.Users.Add(new TelegramDB.User(message.From.Id, message.From.FirstName, lang, 3));
                    await db.SaveChangesAsync(cancellationToken);
                    user = await db.Users.FindAsync(new object?[] { message.From.Id }, cancellationToken: cancellationToken);
                    res = await _botClient.SendTextMessageAsync(
                        chatId: message.From.Id,
                        text: Text.Welcome[lang],
                        replyMarkup: KeyboardHandler(user!),
                        cancellationToken: cancellationToken);
                }
                else
                {
                    res = await ChangeLanguage(_botClient, message, cancellationToken);
                }
                _logger.LogInformation("The message was sent with id: {SentMessageId}", res.MessageId);
                return;
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e.Message);
            return;
        }

        var dataBase = new TelegramDB();
        user = await dataBase.Users.FindAsync(message.From.Id);

        var command = Regex.Replace(messageText, @"\p{Cs}", "").Trim();
        
        var handle = true switch
        {
            true when command == "/start" => SendKeyboard(_botClient, message, user!, cancellationToken),
            true when command == "/help" => SendHelp(_botClient, message, user!, cancellationToken),
            true when Text.Account.ContainsValue(command) => AccountInfo(_botClient, message, user!, cancellationToken),
            true when Text.HelpButton.ContainsValue(command) => SendHelp(_botClient, message, user!, cancellationToken),
            true when Text.NewRecord.ContainsValue(command) => SendSpecialists(_botClient, message, user!, cancellationToken),
            true when Text.Language.ContainsValue(command) => ChangeLanguage(_botClient, message, cancellationToken),
            _ => SendKeyboard(_botClient, message, user!, cancellationToken)
        };
        var result = await handle;
        _logger.LogInformation("The message was sent with id: {SentMessageId}", result.MessageId);
        

        static async Task<Message> SendKeyboard(ITelegramBotClient botClient, Message message, TelegramDB.User user, CancellationToken cancellationToken)
        {
            return await botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: "\U00002328 Keyboard reloaded",
                replyMarkup: KeyboardHandler(user),
                cancellationToken: cancellationToken);
        }

        static async Task<Message> ChangeLanguage(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
        {
            InlineKeyboardMarkup keyboard = new(new[]
            {
                InlineKeyboardButton.WithCallbackData(text: "English \U0001F1FA\U0001F1F8", callbackData: "en"),
                InlineKeyboardButton.WithCallbackData(text: "Русский \U0001F1F7\U0001F1FA", callbackData: "ru")
            });

            return await botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: "\U0001F5E3 Choose language",
                replyMarkup: keyboard,
                cancellationToken: cancellationToken);
        }

        static async Task<Message> AccountInfo(ITelegramBotClient botClient, Message message, TelegramDB.User user, CancellationToken cancellationToken)
        {
            InlineKeyboardMarkup keyboard = new(new[]
            {
                InlineKeyboardButton.WithCallbackData(text: $"\U0001F9FE {Text.History[user.Language]}", callbackData: "History"),
                InlineKeyboardButton.WithCallbackData(text: $"\U0001F5C8 {Text.NewRecord[user.Language]}", callbackData: "NewRecord") 
            });
            var text = $"{Text.Name[user.Language]}: {message.From!.FirstName}\n" +
                       $"{Text.NumberOfOrders[user.Language]}: {user.History.Count}\n";
            if (user.Role <= 2)
            {
                text += $"{Text.NumberCompletedOrders[user.Language]}: {user.HistorySpec.Count}";
            }
            return await botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: text,
                replyMarkup: keyboard,
                cancellationToken: cancellationToken);
        }

        static async Task<Message> SendSpecialists(ITelegramBotClient botClient, Message message, TelegramDB.User user, CancellationToken cancellationToken)
        {
            var res = SpecialistsHandler(message, user);
            InlineKeyboardMarkup keyboard = new(res.Value);
            return await botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: res.Key,
                replyMarkup: keyboard,
                cancellationToken: cancellationToken);
        }

        static async Task<Message> SendHelp(ITelegramBotClient botClient, Message message, TelegramDB.User user, CancellationToken cancellationToken)
        {
            return await botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: Text.Help[user.Language],
                replyMarkup: KeyboardHandler(user),
                cancellationToken: cancellationToken);
        }
    }

    private async Task CallbackHandler(CallbackQuery message, CancellationToken cancellationToken)
    {
        _logger.LogInformation($"Received '{message.Data}' callback in chat {message.Message?.Chat.Id}.");
        if (message.Data is not { } messageText)
        {
            return;
        }

        if (message.Message is null)
        {
            await _botClient.SendTextMessageAsync(
                chatId: message.From.Id,
                text: "Message is too old for inline buttons \U0001F601",
                cancellationToken: cancellationToken);
            return;
        }
        
        if (Text.SupportedLanguages.Contains(messageText))
        {
            try
            {
                await ChangeLanguage(_botClient, message, messageText, cancellationToken);
                _logger.LogInformation("Change language to {lang} from user {id}", messageText, message.From.Id);
                return;
            }
            catch (Exception e)
            {
                _logger.LogError(e.Message);
                return;
            }
        }
        
        var db = new TelegramDB();
        var user = await db.Users.FindAsync(new object?[] { message.From.Id }, cancellationToken: cancellationToken);

        var handle = messageText switch
        {
            "History" or "HistorySpec" => ShowHistory(_botClient, message, user!, cancellationToken),
            "Page" when messageText[0] == 'h' => ShowHistory(_botClient, message, user!, cancellationToken),
            "backAcc" => AccountInfo(_botClient, message, user!, cancellationToken),
            "NewRecord" => SendSpecialists(_botClient, message, user!, cancellationToken),
            _ => AccountInfo(_botClient, message, user!, cancellationToken)
        };
        var result = await handle;
        
        _logger.LogInformation("The callback was processed {callback}", message.Id);
        _logger.LogInformation("Edit message {id}", result.MessageId);

        
        static async Task ChangeLanguage(ITelegramBotClient botClient, CallbackQuery message, string messageText, CancellationToken cancellationToken)
        {
            var db = new TelegramDB();
            var user = await db.Users.FindAsync(new object?[] { message.From.Id }, cancellationToken: cancellationToken);
            
            if (user is null)
            {
                db.Users.Add(new TelegramDB.User(message.From.Id, message.From.FirstName, messageText, 3));
                await db.SaveChangesAsync(cancellationToken);
                user = await db.Users.FindAsync(new object?[] { message.From.Id }, cancellationToken: cancellationToken);
                await botClient.SendTextMessageAsync(
                    chatId: message.From.Id,
                    text: Text.Welcome[messageText],
                    replyMarkup: KeyboardHandler(user!),
                    cancellationToken: cancellationToken);
            }
            else
            {
                user.Language = messageText;
                await db.SaveChangesAsync(cancellationToken);
                await botClient.AnswerCallbackQueryAsync(
                    callbackQueryId: message.Id,
                    text: Text.Success[messageText],
                    showAlert: true,
                    cancellationToken: cancellationToken);
                var key = await botClient.SendTextMessageAsync(
                    chatId: message.From.Id,
                    text: "\U00002328 Keyboard reloaded",
                    replyMarkup: KeyboardHandler(user),
                    cancellationToken: cancellationToken);
                /*await botClient.DeleteMessageAsync(
                    chatId: message.From.Id,
                    messageId: key.MessageId,
                    cancellationToken: cancellationToken);*/
            }
            
            await botClient.DeleteMessageAsync(
                chatId: message.From.Id,
                messageId: message.Message!.MessageId,
                cancellationToken: cancellationToken);
        }

        static async Task<Message> ShowHistory(ITelegramBotClient botClient, CallbackQuery message, TelegramDB.User user, CancellationToken cancellationToken)
        {
            var history = "";
            if (user.History.Count != 0 || user.HistorySpec.Count != 0 && user.Role <=2)
            {
                if (user.Role <= 2 && message.Data == "HistorySpec")
                {
                    foreach (var record in user.HistorySpec)
                    {
                        history += $"{record.Date:MM/dd/yyyy HH:mm}\n{Text.Specialist[user.Language]}: {record.User.TelegramName}" +
                                   $"\n{Text.FeedbackSpec[user.Language]} " +
                                   $"{string.Concat(Enumerable.Repeat("\U00002B50", record.Feedback.GetValueOrDefault()))}" +
                                   $"{string.Concat(Enumerable.Repeat("\U00002606", 5 - record.Feedback.GetValueOrDefault()))}\n \n";
                    }
                }
                else
                {
                    foreach (var record in user.History)
                    {
                        history += $"{record.Date:MM/dd/yyyy HH:mm}\n{Text.Specialist[user.Language]}: {record.Spec.TelegramName}" +
                                   $"\n{Text.FeedbackUser[user.Language]} " +
                                   $"{string.Concat(Enumerable.Repeat("\U00002B50", record.Feedback.GetValueOrDefault()))}" +
                                   $"{string.Concat(Enumerable.Repeat("\U00002606", 5 - record.Feedback.GetValueOrDefault()))}\n \n";
                    }
                }
            }

            InlineKeyboardMarkup keyboard;
            if (history == "")
            {
                history = $"\U0001F61E {Text.HistoryEmpty[user.Language]}";
                keyboard = new InlineKeyboardMarkup(new[]
                {
                    InlineKeyboardButton.WithCallbackData(text: $"\U000023F4{Text.Back[user.Language]}", callbackData: "backAcc")
                });
            }
            else
            {
                keyboard = new InlineKeyboardMarkup(new[]
                {
                    InlineKeyboardButton.WithCallbackData(text: $"\U000023F4 {Text.Back[user.Language]}", callbackData: "backAcc")
                });
            }
            return await botClient.EditMessageTextAsync(
                chatId: message.From.Id,
                messageId: message.Message!.MessageId,
                text: history,
                replyMarkup: keyboard, 
                cancellationToken: cancellationToken);
        }
        
        static async Task<Message> AccountInfo(ITelegramBotClient botClient, CallbackQuery message, TelegramDB.User user, CancellationToken cancellationToken)
        {
            InlineKeyboardMarkup keyboard = new(new[]
            {
                InlineKeyboardButton.WithCallbackData(text: $"\U0001F9FE {Text.History[user.Language]}", callbackData: "History"),
                InlineKeyboardButton.WithCallbackData(text: $"\U0001F5C8 {Text.NewRecord[user.Language]}", callbackData: "NewRecord")
            });
            var text = $"{Text.Name[user.Language]}: {message.From!.FirstName}\n" +
                       $"{Text.NumberOfOrders[user.Language]}: {user.History.Count}\n";
            if (user.Role <= 2)
            {
                text += $"{Text.NumberCompletedOrders[user.Language]}: {user.HistorySpec.Count}";
            }
            return await botClient.EditMessageTextAsync(
                chatId: message.From.Id,
                messageId: message.Message!.MessageId,
                text: text,
                replyMarkup: keyboard,
                cancellationToken: cancellationToken);
        }
        
        static async Task<Message> SendSpecialists(ITelegramBotClient botClient, CallbackQuery message, TelegramDB.User user, CancellationToken cancellationToken)
        {
            var res = SpecialistsHandler(message.Message!, user);
            InlineKeyboardMarkup keyboard = new(res.Value);
            return await botClient.EditMessageTextAsync(
                chatId: message.From.Id,
                messageId: message.Message!.MessageId,
                text: res.Key,
                replyMarkup: keyboard,
                cancellationToken: cancellationToken);
        }
    }

    private Task UnknownHandler(Update update, CancellationToken cancellationToken)
    {
        _logger.LogWarning("Unknown update: {}", update.Type);
        return Task.CompletedTask;
    }

    private static ReplyKeyboardMarkup KeyboardHandler(TelegramDB.User user)
    {
        return user.Role switch
        {
            3 => new ReplyKeyboardMarkup(
                new[]
                {
                    new[]
                    {
                        new KeyboardButton($"\U0001F194 {Text.Account[user.Language]}"),
                        new KeyboardButton($"\U0001F5C8 {Text.NewRecord[user.Language]}")
                    },
                    new[]
                    {
                        new KeyboardButton($"\U0001F30D {Text.Language[user.Language]}")
                    },
                    new[]
                    {
                        new KeyboardButton($"\U0001F198 {Text.HelpButton[user.Language]}")
                    }
                })
            {
                ResizeKeyboard = true
            },
            _ => new ReplyKeyboardMarkup(
                new[]
                {
                    new[]
                    {
                        new KeyboardButton($"\U0001F194 {Text.Account[user.Language]}")
                    },
                    new[]
                    {
                        new KeyboardButton($"\U0001F30D {Text.Language[user.Language]}")
                    },
                    new[]
                    {
                        new KeyboardButton($"\U0001F198 {Text.HelpButton[user.Language]}")
                    }
                })
            {
                ResizeKeyboard = true
            }
        };
        return new ReplyKeyboardMarkup(
            new[]
            {
                new []
                {
                    new KeyboardButton($"\U0001F194 {Text.Account[user.Language]}")
                },
                new []
                {
                    new KeyboardButton($"\U0001F30D {Text.Language[user.Language]}")
                },
                new []
                {
                    new KeyboardButton($"\U0001F198 {Text.Help[user.Language]}")
                }
            })
        {
            ResizeKeyboard = true
        };
            
        /*return await botClient.SendTextMessageAsync(
            chatId: id,
            text: "Keyboard reloaded",
            replyMarkup: keyboard,
            cancellationToken: cancellationToken);*/
    }

    private static KeyValuePair<string, List<InlineKeyboardButton>> SpecialistsHandler(Message message, TelegramDB.User user)
    {
        var db = new TelegramDB();
        var text = "";
        var lang = user.Language;
        var buttons = new List<InlineKeyboardButton>();
        foreach (var spec in db.Specialists)
        {
            if (spec.Work && spec.SpecialistId != message.From!.Id)
            {
                text += $"{Text.Specialist[lang]}: {spec.DisplayName}\n{Text.FeedbackSpec[lang]}" +
                        $"{string.Concat(Enumerable.Repeat("\U00002B50", (int)Math.Round(spec.MeanFeedback.GetValueOrDefault())))}" +
                        $"{string.Concat(Enumerable.Repeat("\U00002606", 5 - (int)Math.Round(spec.MeanFeedback.GetValueOrDefault())))}\n \n";
                buttons.Add(InlineKeyboardButton.WithCallbackData(text: $"{spec.DisplayName}", callbackData: $"spec.{spec.SpecialistId}"));
            }
        }

        if (text == "")
        {
            text = $"\U0001F61E {Text.SpecialistsEmpty[user.Language]}";
        } 
        buttons.Add(InlineKeyboardButton.WithCallbackData(text: $"\U000023F4{Text.Back[user.Language]}", callbackData: "backAcc"));
        return new KeyValuePair<string, List<InlineKeyboardButton>>(text, buttons);
    }


    public async Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        var errorMessage = exception switch
        {
            ApiRequestException apiRequestException => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
            _ => exception.ToString()
        };

        _logger.LogInformation("HandleError: {errorMessage}", errorMessage);
        
        if (exception is RequestException)
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
    }
}