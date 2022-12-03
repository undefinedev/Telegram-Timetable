using System.Net.Mime;
using Microsoft.EntityFrameworkCore;
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
        
        /*
        // Only process Message updates: https://core.telegram.org/bots/api#message
        if (update.Message is not { } message)
            return;
        // Only process text messages
        if (message.Text is not { } messageText)
            return;

        var chatId = message.Chat.Id;

        _logger.LogInformation($"Received a '{messageText}' message in chat {chatId}.");

        // Echo received message text
        Message sentMessage = await botClient.SendTextMessageAsync(
            chatId: chatId,
            text: "You said:\n" + messageText,
            cancellationToken: cancellationToken);
            */
    }

    private async Task MessageHandler(Message message, CancellationToken cancellationToken)
    {
        _logger.LogInformation($"Received '{message.Text}' message in chat {message.Chat.Id}.");
        if (message.Text is not { } messageText)
        {
            return;
        }
        
        try
        {
            await using var db = new TelegramDB();
            if (db.Users.FindAsync(message.From!.Id).GetAwaiter().GetResult() is null)
            {
                await ChangeLanguage(_botClient, message, cancellationToken);
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e.Message);
            return;
        }

        var handle = messageText.Split(' ')[0] switch
        {
            "/start" => SendKeyboard(_botClient, message, cancellationToken),
            _ => SendKeyboard(_botClient, message, cancellationToken)
        };
        var result = await handle;
        _logger.LogInformation("The message was sent with id: {SentMessageId}", result.MessageId);

        /*static async Task<Message> Welcome(ITelegramBotClient botClient, Message message,
            CancellationToken cancellationToken)
        {
            
        }*/

        static async Task<Message> SendKeyboard(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
        {
            ReplyKeyboardMarkup keyboard = new(
                new[]
                {
                    new KeyboardButton("Ping")
                })
            {
                ResizeKeyboard = true
            };
            
            return await botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: "Keyboard reloaded",
                replyMarkup: keyboard,
                cancellationToken: cancellationToken);
        }

        static async Task<Message> ChangeLanguage(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
        {
            InlineKeyboardMarkup keyboard = new(new[]
            {
                InlineKeyboardButton.WithCallbackData(text: "English \U0001F1FA\U0001F1F8", callbackData: "English"),
                InlineKeyboardButton.WithCallbackData(text: "Русский \U0001F1F7\U0001F1FA", callbackData: "Russian")
            });

            return await botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: "Choose language",
                replyMarkup: keyboard,
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

        Message result;
        if (Text.SupportedLanguages.Contains(messageText.Split(' ')[0]))
        {
            try
            {
                result = await ChangeLanguage(_botClient, message, messageText, cancellationToken);
            }
            catch (Exception e)
            {
                _logger.LogError(e.Message);
                return;
            }
        }


        _logger.LogInformation("The callback was processed {callback}", message.Id);

        static async Task<Message> ChangeLanguage(ITelegramBotClient botClient, CallbackQuery message, string messageText, CancellationToken cancellationToken)
        {
            await using var db = new TelegramDB();
            var user = await db.Users.FindAsync(new object?[] { message.From.Id }, cancellationToken: cancellationToken);
            var result = messageText.Split(' ')[0] switch
            {
                "English" => "en",
                "Russian" => "ru",
                _ => "ru"
            };
            if (user is null)
            {
                db.Users.Add(new TelegramDB.User(message.From.Id, result, new List<TelegramDB.Record>(), 3));
            }
            else
            {
                user.Language = result;
            }
            await db.SaveChangesAsync(cancellationToken);

            return await botClient.SendTextMessageAsync(
                chatId: message.From.Id,
                text: Text.Success[result],
                cancellationToken: cancellationToken);
        }
    }

    private Task UnknownHandler(Update update, CancellationToken cancellationToken)
    {
        _logger.LogWarning("Unknown update: {}", update.Type);
        return Task.CompletedTask;
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