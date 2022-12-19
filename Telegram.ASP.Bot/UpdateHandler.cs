using System.Collections;
using System.Globalization;
using System.Linq.Expressions;
using System.Net.Mime;
using System.Text.RegularExpressions;
using Castle.Components.DictionaryAdapter.Xml;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
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

    public async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update,
        CancellationToken cancellationToken)
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
                    user = await db.Users.FindAsync(new object?[] { message.From.Id },
                        cancellationToken: cancellationToken);
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
            true when Text.NewRecord.ContainsValue(command) => SendSpecialists(_botClient, message, user!,
                cancellationToken),
            true when command == "Add specialist" && user!.Role == 0 => AddSpec(_botClient, message, user,
                cancellationToken),
            true when command.Split('/')[0] == "AddSpec71" && user!.Role == 0 => AddSpec(_botClient, message, user,
                cancellationToken),
            true when Text.Language.ContainsValue(command) => ChangeLanguage(_botClient, message, cancellationToken),
            _ => SendKeyboard(_botClient, message, user!, cancellationToken)
        };
        var result = await handle;
        _logger.LogInformation("The message was sent with id: {SentMessageId}", result?.MessageId);


        static async Task<Message> SendKeyboard(ITelegramBotClient botClient, Message message, TelegramDB.User user,
            CancellationToken cancellationToken)
        {
            return await botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: "\U00002328 Keyboard reloaded",
                replyMarkup: KeyboardHandler(user),
                cancellationToken: cancellationToken);
        }

        static async Task<Message> ChangeLanguage(ITelegramBotClient botClient, Message message,
            CancellationToken cancellationToken)
        {
            InlineKeyboardMarkup keyboard = new(new[]
            {
                InlineKeyboardButton.WithCallbackData("English \U0001F1FA\U0001F1F8", "en"),
                InlineKeyboardButton.WithCallbackData("Русский \U0001F1F7\U0001F1FA", "ru")
            });

            return await botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: "\U0001F5E3 Choose language",
                replyMarkup: keyboard,
                cancellationToken: cancellationToken);
        }

        static async Task<Message> AccountInfo(ITelegramBotClient botClient, Message message, TelegramDB.User user,
            CancellationToken cancellationToken)
        {
            var acc = AccountHandler(message.From!, user);
            return await botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: acc.Key,
                replyMarkup: acc.Value,
                cancellationToken: cancellationToken);
        }

        static async Task<Message> SendSpecialists(ITelegramBotClient botClient, Message message, TelegramDB.User user,
            CancellationToken cancellationToken)
        {
            var res = SpecialistsHandler(message.From!, user);
            InlineKeyboardMarkup keyboard = new(res.Value);
            return await botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: res.Key,
                replyMarkup: keyboard,
                cancellationToken: cancellationToken);
        }

        static async Task<Message> SendHelp(ITelegramBotClient botClient, Message message, TelegramDB.User user,
            CancellationToken cancellationToken)
        {
            return await botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: Text.Help[user.Language],
                replyMarkup: KeyboardHandler(user),
                cancellationToken: cancellationToken);
        }

        static async Task<Message> AddSpec(ITelegramBotClient botClient, Message message, TelegramDB.User user,
            CancellationToken cancellationToken)
        {
            var parse = message.Text!.Split('/');
            if (message.Text == "Add specialist" || parse.Length != 6 || parse[3].Split(':').Length != 2 ||
                parse[4].Split(':').Length != 2)
            {
                return await botClient.SendTextMessageAsync(
                    chatId: message.Chat.Id,
                    text: "To add specialist send command in format 'AddSpec71/TelegramId/Name/StartTime/EndTime/" +
                          "Interval'\nTime in format HH:mm\nInterval in minutes",
                    cancellationToken: cancellationToken);
            }

            var db = new TelegramDB();
            if (await db.Users.FindAsync(Convert.ToInt64(parse[1])) == null ||
                await db.Specialists.FindAsync(Convert.ToInt64(parse[1])) != null)
            {
                return await botClient.SendTextMessageAsync(
                    chatId: message.Chat.Id,
                    text: $"{Text.Error[user.Language]}",
                    cancellationToken: cancellationToken);
            }

            TelegramDB.Specialist? temp;
            var timeS = parse[3].Split(':');
            var timeE = parse[4].Split(':');
            try
            {
                db.Specialists.Add(new TelegramDB.Specialist(Convert.ToInt64(parse[1]), parse[2],
                    new TimeSpan(Convert.ToInt32(timeS[0]), Convert.ToInt32(timeS[1]), 0),
                    new TimeSpan(Convert.ToInt32(timeE[0]), Convert.ToInt32(timeE[1]), 0),
                    Convert.ToInt32(parse[5])));
                await db.SaveChangesAsync(cancellationToken);
                var spec = await db.Users.FindAsync(Convert.ToInt64(parse[1]));
                if (spec == null)
                {
                    return await botClient.SendTextMessageAsync(
                        chatId: message.Chat.Id,
                        text: $"{Text.Error[user.Language]}",
                        cancellationToken: cancellationToken);
                }

                spec.Role = 2;
                await db.SaveChangesAsync(cancellationToken);
                temp = spec.Specialist;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }

            var text = $"#{temp!.SpecialistId}\n{temp.DisplayName}" +
                       $"\nStart: {temp.Start}\nEnd: {temp.End}" +
                       $"\nInterval: {temp.Interval}";

            return await botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: text,
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

        var handle = messageText.Split('.')[0] switch
        {
            "History" or "HistorySpec" or "Future" => ShowHistory(_botClient, message, user!, cancellationToken),
            "Page" when messageText[0] == 'h' => ShowHistory(_botClient, message, user!, cancellationToken),
            "backAcc" => AccountInfo(_botClient, message, user!, cancellationToken),
            "NewRecord" => SendSpecialists(_botClient, message, user!, cancellationToken),
            "backSpecList" => SendSpecialists(_botClient, message, user!, cancellationToken),
            "spec" => SendCalendar(_botClient, message, user!, cancellationToken),
            "stayCalm" => SendCalendar(_botClient, message, user!, cancellationToken),
            "newOrder" => TimeChoose(_botClient, message, user!, cancellationToken),
            "Confirm" => ConfirmOrder(_botClient, message, user!, cancellationToken),
            "Create" => CreateOrder(_botClient, message, user!, cancellationToken),
            "Rate" => RateOrder(_botClient, message, user!, cancellationToken),
            "Don`t touch" => null,
            _ => AccountInfo(_botClient, message, user!, cancellationToken)
        };
        if (handle is null)
        {
            return;
        }

        var result = await handle;

        _logger.LogInformation("The callback was processed {callback}", message.Id);
        _logger.LogInformation("Edit message {id}", result.MessageId);


        static async Task ChangeLanguage(ITelegramBotClient botClient, CallbackQuery message, string messageText,
            CancellationToken cancellationToken)
        {
            var db = new TelegramDB();
            var user = await db.Users.FindAsync(new object?[] { message.From.Id },
                cancellationToken: cancellationToken);

            if (user is null)
            {
                db.Users.Add(new TelegramDB.User(message.From.Id, message.From.FirstName, messageText, 3));
                await db.SaveChangesAsync(cancellationToken);
                user = await db.Users.FindAsync(new object?[] { message.From.Id },
                    cancellationToken: cancellationToken);
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

        static async Task<Message> ShowHistory(ITelegramBotClient botClient, CallbackQuery message,
            TelegramDB.User user, CancellationToken cancellationToken)
        {
            var history = "";
            var buttons = new List<List<InlineKeyboardButton>>();
            var rowButtons = new List<InlineKeyboardButton>();
            if (user.History.Count != 0 || user.HistorySpec.Count != 0 && user.Role <= 2)
            {
                if (user.Role <= 2 && message.Data == "HistorySpec")
                {
                    var temp = user.HistorySpec.ToList();
                    temp.Sort((record, record1) => 
                        record.Date > record1.Date ? 1 : record.Date == record1.Date ? 0 : -1);
                    if (temp.Count > 7)
                    {
                        temp.RemoveRange(0, temp.Count - 7);
                    }

                    foreach (var record in temp)
                    {
                        history +=
                            $"#{record.RecordId} {record.Date:dd/MM/yyyy HH:mm}" +
                            $"\n{Text.Specialist[user.Language]}: {record.User.TelegramName}" +
                            $"\n{Text.FeedbackSpec[user.Language]} " +
                            $"{string.Concat(Enumerable.Repeat("\U00002B50", record.Feedback.GetValueOrDefault()))}" +
                            $"{string.Concat(Enumerable.Repeat("\U00002606", 5 - record.Feedback.GetValueOrDefault()))}\n \n";
                    }
                }
                else if (message.Data == "Future")
                {
                    var temp = user.History.ToList();
                    temp.Sort((record, record1) => 
                        record.Date > record1.Date ? 1 : record.Date == record1.Date ? 0 : -1);
                    foreach (var record in temp)
                    {
                        if (record.FutureRecord == null) continue;
                        history +=
                            $"#{record.RecordId} {record.Date:dd/MM/yyyy HH:mm}" +
                            $"\n{Text.Specialist[user.Language]}: {record.Spec.TelegramName}\n \n";
                    }
                }
                else
                {
                    var temp = user.History.ToList();
                    temp.Sort((record, record1) => 
                        record.Date > record1.Date ? 1 : record.Date == record1.Date ? 0 : -1);
                    if (temp.Count > 10)
                    {
                        temp.RemoveRange(0, temp.Count - 10);
                    }
                    foreach (var record in temp)
                    {
                        if (record.FutureRecord != null) continue;
                        if (record.FutureRecord == null && record.Feedback == 0)
                        {
                            rowButtons.Add(InlineKeyboardButton.WithCallbackData(
                                $"{Text.Rate[user.Language]} #{record.RecordId}", $"Rate.{record.RecordId}"));
                            if (rowButtons.Count >= 3)
                            {
                                buttons.Add(new List<InlineKeyboardButton>(rowButtons));
                                rowButtons.Clear();
                            }
                        }

                        history +=
                            $"#{record.RecordId} {record.Date:dd/MM/yyyy HH:mm}" +
                            $"\n{Text.Specialist[user.Language]}: {record.Spec.TelegramName}" +
                            $"\n{Text.FeedbackUser[user.Language]} " +
                            $"{string.Concat(Enumerable.Repeat("\U00002B50", record.Feedback.GetValueOrDefault()))}" +
                            $"{string.Concat(Enumerable.Repeat("\U00002606", 5 - record.Feedback.GetValueOrDefault()))}\n \n";
                    }
                }
            }

            if (history == "")
            {
                history = $"\U0001F61E {Text.HistoryEmpty[user.Language]}";
            }

            buttons.Add(new List<InlineKeyboardButton>(rowButtons));
            buttons.Add(new List<InlineKeyboardButton>
            {
                InlineKeyboardButton.WithCallbackData($"\U000025C0{Text.Back[user.Language]}", "backAcc")
            });
            InlineKeyboardMarkup keyboard = new(buttons);

            return await botClient.EditMessageTextAsync(
                chatId: message.From.Id,
                messageId: message.Message!.MessageId,
                text: history,
                replyMarkup: keyboard,
                cancellationToken: cancellationToken);
        }

        static async Task<Message> AccountInfo(ITelegramBotClient botClient, CallbackQuery message,
            TelegramDB.User user, CancellationToken cancellationToken)
        {
            if (user.Role < 3 && message.Data == "WorkChange")
            {
                var db = new TelegramDB();
                var spec = await db.Specialists.FindAsync(new object?[] { message.From.Id },
                    cancellationToken: cancellationToken);
                spec!.Work = !spec.Work;
                await db.SaveChangesAsync(cancellationToken);
            }

            var acc = AccountHandler(message.From, user);
            return await botClient.EditMessageTextAsync(
                chatId: message.From.Id,
                messageId: message.Message!.MessageId,
                text: acc.Key,
                replyMarkup: acc.Value,
                cancellationToken: cancellationToken);
        }

        static async Task<Message> SendSpecialists(ITelegramBotClient botClient, CallbackQuery message,
            TelegramDB.User user, CancellationToken cancellationToken)
        {
            var res = SpecialistsHandler(message.From, user);
            InlineKeyboardMarkup keyboard = new(res.Value);
            return await botClient.EditMessageTextAsync(
                chatId: message.From.Id,
                messageId: message.Message!.MessageId,
                text: res.Key,
                replyMarkup: keyboard,
                cancellationToken: cancellationToken);
        }

        static async Task<Message> SendCalendar(ITelegramBotClient botClient, CallbackQuery message,
            TelegramDB.User user, CancellationToken cancellationToken)
        {
            var db = new TelegramDB();
            var spec = await db.Specialists.FindAsync(new object?[] { Convert.ToInt64(message.Data!.Split('.')[1]) },
                cancellationToken: cancellationToken);
            if (spec == null)
            {
                return await botClient.SendTextMessageAsync(
                    chatId: message.From.Id,
                    text: Text.Error[user.Language],
                    cancellationToken: cancellationToken);
            }

            var data = DateTime.Now;
            var day = data.DayOfWeek;
            //var data = DateTime.Now.AddDays(0);  // Developer mode
            //var day = DayOfWeek.Sunday;
            var dayNames = Text.Days[user.Language];
            var buttons = new List<List<InlineKeyboardButton>>();

            var days = new List<InlineKeyboardButton>
            {
                InlineKeyboardButton.WithCallbackData($"{dayNames.Split(',')[0]}", $"stayCalm.{spec.SpecialistId}"),
                InlineKeyboardButton.WithCallbackData($"{dayNames.Split(',')[1]}", $"stayCalm.{spec.SpecialistId}"),
                InlineKeyboardButton.WithCallbackData($"{dayNames.Split(',')[2]}", $"stayCalm.{spec.SpecialistId}"),
                InlineKeyboardButton.WithCallbackData($"{dayNames.Split(',')[3]}", $"stayCalm.{spec.SpecialistId}"),
                InlineKeyboardButton.WithCallbackData($"{dayNames.Split(',')[4]}", $"stayCalm.{spec.SpecialistId}"),
                InlineKeyboardButton.WithCallbackData($"{dayNames.Split(',')[5]}", $"stayCalm.{spec.SpecialistId}"),
                InlineKeyboardButton.WithCallbackData($"{dayNames.Split(',')[6]}", $"stayCalm.{spec.SpecialistId}")
            };
            buttons.Add(days);
            for (var i = 0; buttons.Count < 5; ++i)
            {
                var temp = new List<InlineKeyboardButton>();
                if (i == 0 && (int)day != 0)
                {
                    for (var d = 1; d < (int)day; ++d)
                    {
                        temp.Add(InlineKeyboardButton.WithCallbackData($"{data.AddDays(-(int)day + d):dd}",
                            $"stayCalm.{spec.SpecialistId}"));
                    }

                    temp.Add(data.TimeOfDay > spec.End.Add(TimeSpan.FromMinutes(-spec.Interval))
                        ? InlineKeyboardButton.WithCallbackData($"{data:dd}",
                            $"stayCalm.{spec.SpecialistId}")
                        : InlineKeyboardButton.WithCallbackData(text: $"{data:dd}",
                            $"newOrder.{spec.SpecialistId}.{data:dd/MM/yyyy}"));

                    for (var d = (int)day + 1; d < 7; ++d)
                    {
                        temp.Add(InlineKeyboardButton.WithCallbackData(text: $"{data.AddDays(-(int)day + d):dd}",
                            $"newOrder.{spec.SpecialistId}.{data.AddDays(-(int)day + d):dd/MM/yyyy}"));
                    }

                    temp.Add(InlineKeyboardButton.WithCallbackData(
                        $"{Text.Holidays[user.Language]}" /*$"{data.AddDays(-(int)day + 7):dd}"*/,
                        $"stayCalm.{spec.SpecialistId}"));
                }
                else
                {
                    for (var j = 1; j < 7; ++j)
                    {
                        temp.Add(InlineKeyboardButton.WithCallbackData(
                            text: $"{data.AddDays(-(int)day + j + i * 7):dd}",
                            $"newOrder.{spec.SpecialistId}.{data.AddDays(-(int)day + j + i * 7):dd/MM/yyyy}"));
                    }

                    temp.Add(InlineKeyboardButton.WithCallbackData(
                        $"{Text.Holidays[user.Language]}" /*$"{data.AddDays(-(int)day + (i + 1) * 7):dd}"*/,
                        $"stayCalm.{spec.SpecialistId}"));
                }

                buttons.Add(temp);
            }

            var culture = user.Language switch
            {
                "ru" => new CultureInfo("ru-RU"),
                "en" => new CultureInfo("en-GB"),
                _ => new CultureInfo("en-GB")
            };

            buttons.Add(new List<InlineKeyboardButton>
            {
                InlineKeyboardButton.WithCallbackData($"\U000025C0{Text.Back[user.Language]}",
                    "backSpecList"),
                InlineKeyboardButton.WithCallbackData($"{data.ToString("MMMM yyyy", culture)}",
                    $"stayCalm.{spec.SpecialistId}")
            });

            InlineKeyboardMarkup keyboard = new(buttons);

            if (message.Data.Split('.')[0] != "stayCalm")
            {
                return await botClient.EditMessageTextAsync(
                    chatId: message.From.Id,
                    messageId: message.Message!.MessageId,
                    text: Text.DateChoose[user.Language],
                    replyMarkup: keyboard,
                    cancellationToken: cancellationToken);
            }

            var reply = "";
            reply += message.Message!.Text!.Count(t => t == '⚠') == 1 ||
                     Text.DateChoose.ContainsValue(message.Message!.Text!)
                ? "\U000026A0 \U000026A0"
                : "\U000026A0";
            reply += Text.RecordWarning[user.Language];
            return await botClient.EditMessageTextAsync(
                chatId: message.From.Id,
                messageId: message.Message!.MessageId,
                text: $"{reply}\n{Text.DateChoose[user.Language]}",
                replyMarkup: keyboard,
                cancellationToken: cancellationToken);
        }

        static async Task<Message> TimeChoose(ITelegramBotClient botClient, CallbackQuery message, TelegramDB.User user,
            CancellationToken cancellationToken)
        {
            var splitText = message.Data!.Split('.');
            var db = new TelegramDB();
            var spec = await db.Specialists.FindAsync(new object?[] { Convert.ToInt64(splitText[1]) },
                cancellationToken: cancellationToken);
            if (spec == null)
            {
                return await botClient.SendTextMessageAsync(
                    chatId: message.From.Id,
                    text: Text.Error[user.Language],
                    cancellationToken: cancellationToken);
            }

            var closedTimes = db.FutureRecords.AsEnumerable()
                .Where(r => r.Record.SpecId == spec.SpecialistId &&
                            $"{r.Record.Date:dd/MM/yyyy}" == $"{splitText[2]}.{splitText[3]}.{splitText[4]}")
                .Select(t => t.Record.Date.TimeOfDay);

            var buttons = new List<List<InlineKeyboardButton>>(10);
            var data = DateTime.Now;
            var workInterval = new List<TimeSpan>();
            var temp = spec.Start;
            while (temp < spec.End)
            {
                if (!closedTimes.Any(r => r == temp))
                {
                    workInterval.Add(temp);
                }

                temp = temp.Add(TimeSpan.FromMinutes(spec.Interval));
            }

            var resultInterval = $"{splitText[2]}.{splitText[3]}.{splitText[4]}" == data.ToString("dd/MM/yyyy")
                ? workInterval.Where(t => t > data.TimeOfDay)
                : workInterval;
            var buttonsRow = new List<InlineKeyboardButton>();
            var callbackText = $"{splitText[1]}.{splitText[2]}:{splitText[3]}:{splitText[4]}";
            foreach (var time in resultInterval)
            {
                buttonsRow.Add(InlineKeyboardButton.WithCallbackData($"{time.Hours:00}:{time.Minutes:00}",
                    $"Confirm.{callbackText}.{time.Hours:00}:{time.Minutes:00}"));
                if (buttonsRow.Count != 7) continue;
                buttons.Add(new List<InlineKeyboardButton>(buttonsRow));
                buttonsRow.Clear();
                if (buttons.Count == 10) break;
            }

            buttons.Add(new List<InlineKeyboardButton>(buttonsRow));
            buttons.Add(new List<InlineKeyboardButton>
            {
                InlineKeyboardButton.WithCallbackData($"\U000025C0{Text.Back[user.Language]}", "backSpecList"),
                InlineKeyboardButton.WithCallbackData($"{splitText[2]}/{splitText[3]}/{splitText[4]}", "Don`t touch")
            });
            InlineKeyboardMarkup keyboard = new(buttons);
            return await botClient.EditMessageTextAsync(
                chatId: message.From.Id,
                messageId: message.Message!.MessageId,
                text: Text.TimeChoose[user.Language],
                replyMarkup: keyboard,
                cancellationToken: cancellationToken);
        }

        static async Task<Message> ConfirmOrder(ITelegramBotClient botClient, CallbackQuery message,
            TelegramDB.User user,
            CancellationToken cancellationToken)
        {
            var buttons = new List<InlineKeyboardButton>
            {
                InlineKeyboardButton.WithCallbackData($"\U00002705 {Text.Confirm[user.Language]}",
                    $"Create.{message.Data}"),
                InlineKeyboardButton.WithCallbackData($"\U0000274C {Text.Decline[user.Language]}",
                    "wtf for whom i showed all this tables")
            };

            var db = new TelegramDB();
            var parseData = message.Data!.Split('.');
            var spec = await db.Specialists.FindAsync(new object?[] { Convert.ToInt64(parseData[1]) },
                cancellationToken: cancellationToken);
            if (spec == null)
            {
                return await botClient.SendTextMessageAsync(
                    chatId: message.From.Id,
                    text: Text.Error[user.Language],
                    cancellationToken: cancellationToken);
            }

            var order = $"\n \n{spec.DisplayName}\n{parseData[2].Replace(':', '/')}\n{parseData[3]}";

            InlineKeyboardMarkup keyboard = new(buttons);
            return await botClient.EditMessageTextAsync(
                chatId: message.From.Id,
                messageId: message.Message!.MessageId,
                text: Text.ConfirmText[user.Language] + order,
                replyMarkup: keyboard,
                cancellationToken: cancellationToken);
        }

        static async Task<Message> CreateOrder(ITelegramBotClient botClient, CallbackQuery message,
            TelegramDB.User user,
            CancellationToken cancellationToken)
        {
            var splitText = message.Data!.Split('.');
            var db = new TelegramDB();
            var spec = await db.Specialists.FindAsync(new object?[] { Convert.ToInt64(splitText[2]) },
                cancellationToken: cancellationToken);
            if (spec == null ||
                db.FutureRecords.AsEnumerable().Any(t =>
                    $"{t.Record.Date:dd/MM/yyyy HH:mm}" == $"{splitText[2].Replace(':', '.')} {splitText[3]}"))
            {
                return await botClient.SendTextMessageAsync(
                    chatId: message.From.Id,
                    text: Text.Error[user.Language],
                    cancellationToken: cancellationToken);
            }

            var data = splitText[3].Split(':');
            var time = splitText[4].Split(':');
            var record = new TelegramDB.Record(message.From.Id, spec.SpecialistId,
                new DateTime(Convert.ToInt32(data[2]), Convert.ToInt32(data[1]), Convert.ToInt32(data[0]),
                    Convert.ToInt32(time[0]), Convert.ToInt32(time[1]), 0), 0);

            var rec = db.Records.Add(record);
            await db.SaveChangesAsync(cancellationToken);
            db.FutureRecords.Add(new TelegramDB.FutureRecord(rec.Entity.RecordId));
            await db.SaveChangesAsync(cancellationToken);

            var text = $"#{rec.Entity.RecordId}\n{spec.DisplayName}\n{rec.Entity.Date:dd/MM/yyyy HH:mm}";
            var textSpec = $"#{rec.Entity.RecordId}\n{message.From.FirstName}\n{rec.Entity.Date:dd/MM/yyyy HH:mm}";

            await botClient.SendTextMessageAsync(
                chatId: spec.SpecialistId,
                text: textSpec,
                cancellationToken: cancellationToken);

            InlineKeyboardMarkup keyboard =
                new(InlineKeyboardButton.WithCallbackData($"\U0001F44C {Text.Success[user.Language]}", "backAcc"));
            return await botClient.EditMessageTextAsync(
                chatId: message.From.Id,
                messageId: message.Message!.MessageId,
                text: text,
                replyMarkup: keyboard,
                cancellationToken: cancellationToken);
        }

        static async Task<Message> RateOrder(ITelegramBotClient botClient, CallbackQuery message,
            TelegramDB.User user, CancellationToken cancellationToken)
        {
            var db = new TelegramDB();
            var order = await db.Records.FindAsync(new object?[] { Convert.ToInt32(message.Data!.Split('.')[1]) },
                cancellationToken: cancellationToken);
            if (order == null)
            {
                return await botClient.SendTextMessageAsync(
                    chatId: message.From.Id,
                    text: Text.Error[user.Language],
                    cancellationToken: cancellationToken);
            }

            var buttons = new List<List<InlineKeyboardButton>>();
            string text;
            if (message.Data!.Split('.').Length == 3)
            {
                order.Feedback = Convert.ToInt32(message.Data!.Split('.')[2]);
                await db.SaveChangesAsync(cancellationToken);
                if (order.Spec.Specialist != null)
                {
                    float? temp = 0f;
                    order.Spec.Specialist.MeanFeedback =
                        order.Spec.HistorySpec.Select(t => t.Feedback).Aggregate(temp, (temp, i) => temp + i) /
                        order.Spec.HistorySpec.Count(t => t.FutureRecord == null && t.Feedback != 0);
                    await db.SaveChangesAsync(cancellationToken);
                }

                buttons.Add(new List<InlineKeyboardButton>
                {
                    InlineKeyboardButton.WithCallbackData($"\U0001F44C {Text.Success[user.Language]}", "backAcc")
                });
                text = $"{Text.RateThanks[user.Language]}";
            }
            else
            {
                text = $"#{order.RecordId}\n{order.Spec.Specialist?.DisplayName}\n{order.Date:dd/MM/yyyy HH:mm}";

                for (var i = 1; i < 6; ++i)
                {
                    buttons.Add(new List<InlineKeyboardButton>
                    {
                        InlineKeyboardButton.WithCallbackData(
                            $"{string.Concat(Enumerable.Repeat("\U00002B50", i))}", $"{message.Data}.{i}")
                    });
                }

                buttons.Add(new List<InlineKeyboardButton>
                {
                    InlineKeyboardButton.WithCallbackData($"\U000025C0{Text.Back[user.Language]}", "backAcc")
                });
            }


            InlineKeyboardMarkup keyboard = new(buttons);
            return await botClient.EditMessageTextAsync(
                chatId: message.From.Id,
                messageId: message.Message!.MessageId,
                text: text,
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
        var buttons = new List<List<KeyboardButton>>();
        buttons.Add(new List<KeyboardButton>
        {
            new($"\U0001F194 {Text.Account[user.Language]}"),
            new($"\U0001F4CC {Text.NewRecord[user.Language]}")
        });

        var tempRow = new List<KeyboardButton>();
        if (user.Role < 3)
        {
            //tempRow.Add(new KeyboardButton($"\U0001F4C8 {Text.Rating[user.Language]}"));
        }

        tempRow.Add(new KeyboardButton($"\U0001F30D {Text.Language[user.Language]}"));
        buttons.Add(new List<KeyboardButton>(tempRow));
        tempRow.Clear();

        if (user.Role < 2)
        {
            tempRow.Add("Add specialist");
        }

        tempRow.Add(new KeyboardButton($"\U0001F198 {Text.HelpButton[user.Language]}"));
        buttons.Add(new List<KeyboardButton>(tempRow));

        return new ReplyKeyboardMarkup(buttons)
        {
            ResizeKeyboard = true
        };
    }

    private static KeyValuePair<string, List<List<InlineKeyboardButton>>> SpecialistsHandler(User userTelegram,
        TelegramDB.User user)
    {
        var db = new TelegramDB();
        var text = "";
        var lang = user.Language;
        var buttons = new List<List<InlineKeyboardButton>>();
        foreach (var spec in db.Specialists)
        {
            if (spec.Work && spec.SpecialistId != userTelegram.Id)
            {
                text += $"{Text.Specialist[lang]}: {spec.DisplayName}\n{Text.Rating[lang]}" +
                        $"{string.Concat(Enumerable.Repeat("\U00002B50", (int)Math.Round(spec.MeanFeedback.GetValueOrDefault())))}" +
                        $"{string.Concat(Enumerable.Repeat("\U00002606", 5 - (int)Math.Round(spec.MeanFeedback.GetValueOrDefault())))}\n \n";
                buttons.Add(new List<InlineKeyboardButton>
                    { InlineKeyboardButton.WithCallbackData($"{spec.DisplayName}", $"spec.{spec.SpecialistId}") });
            }
        }

        if (text == "")
        {
            text = $"\U0001F61E {Text.SpecialistsEmpty[user.Language]}";
        }

        buttons.Add(new List<InlineKeyboardButton>
            { InlineKeyboardButton.WithCallbackData($"\U000025C0{Text.Back[user.Language]}", "backAcc") });
        return new KeyValuePair<string, List<List<InlineKeyboardButton>>>(text, buttons);
    }

    private static KeyValuePair<string, InlineKeyboardMarkup> AccountHandler(User userTelegram, TelegramDB.User user)
    {
        var buttons = new List<List<InlineKeyboardButton>>();
        buttons.Add(new List<InlineKeyboardButton>
            { InlineKeyboardButton.WithCallbackData($"\U0001F4CC {Text.NewRecord[user.Language]}", "NewRecord") });
        buttons.Add(new List<InlineKeyboardButton>
            { InlineKeyboardButton.WithCallbackData($"\U0001F9FE {Text.History[user.Language]}", "History") });
        if (user.History.Any(h => h.FutureRecord != null))
        {
            buttons.Add(new List<InlineKeyboardButton>
                { InlineKeyboardButton.WithCallbackData($"{Text.FutureOrd[user.Language]}", "Future") });
        }

        if (user.Role <= 2)
        {
            buttons.Add(new List<InlineKeyboardButton>
            {
                InlineKeyboardButton.WithCallbackData($"{Text.SpecOrders[user.Language]}", "HistorySpec")
            });
            var smile = user.Specialist!.Work ? "\U0001F7E2" : "\U0001F534";
            buttons.Add(new List<InlineKeyboardButton>
            {
                InlineKeyboardButton.WithCallbackData($"{Text.WorkStatus[user.Language]} {smile}", "WorkChange")
            });
        }

        InlineKeyboardMarkup keyboard = new(buttons);
        var text = $"{Text.Name[user.Language]}: {userTelegram.FirstName}\n" +
                   $"{Text.NumberOfOrders[user.Language]}: {user.History.Count}\n" +
                   $"{Text.Completed[user.Language]}: {user.History.Count(t => t.FutureRecord == null)}\n";
        if (user.Role <= 2)
        {
            text +=
                $"{Text.NumberSpecOrders[user.Language]}: {user.HistorySpec.Count}";
        }

        return new KeyValuePair<string, InlineKeyboardMarkup>(text, keyboard);
    }

    public async Task<Message?> SendNotification(ITelegramBotClient botClient, int record,
        CancellationToken cancellationToken)
    {
        var db = new TelegramDB();
        var order = await db.Records.FindAsync(new object?[] { record }, cancellationToken: cancellationToken);
        if (order == null)
        {
            return null;
        }
        var text = $"\U0001F680 {Text.RateNotification[order.User.Language]}\n \n#{order.RecordId}\n" +
                   $"{order.Spec.Specialist?.DisplayName}\n" +
                   $"{order.Date:dd/MM/yyyy HH:mm}";

        var buttons = new List<List<InlineKeyboardButton>>();
        for (var i = 1; i < 6; ++i)
        {
            buttons.Add(new List<InlineKeyboardButton>
            {
                InlineKeyboardButton.WithCallbackData(
                    $"{string.Concat(Enumerable.Repeat("\U00002B50", i))}", $"Rate.{order.RecordId}.{i}")
            });
        }

        buttons.Add(new List<InlineKeyboardButton>
        {
            InlineKeyboardButton.WithCallbackData($"\U000025C0{Text.Back[order.User.Language]}", "backAcc")
        });
        InlineKeyboardMarkup keyboard = new(buttons);
        return await botClient.SendTextMessageAsync(
            chatId: order.UserId,
            text: text,
            replyMarkup: keyboard,
            cancellationToken: cancellationToken);
    }


    public async Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception,
        CancellationToken cancellationToken)
    {
        var errorMessage = exception switch
        {
            ApiRequestException apiRequestException =>
                $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
            _ => exception.ToString()
        };

        _logger.LogInformation("HandleError: {errorMessage}", errorMessage);

        if (exception is RequestException)
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
    }
}