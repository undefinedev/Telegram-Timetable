using System.Net.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InputFiles;
using Telegram.UpdateHandler;
using Telegram.Lang;
using Telegram.DB;


var builder = WebApplication.CreateBuilder(args);
var botConfig = builder.Configuration.GetSection("BotConfig").Get<BotConfig>();
var app = builder.Build();

var bot = new TelegramBotClient(botConfig.BotToken);
var logger = new Logger<UpdateHandler>(LoggerFactory.Create(logger => logger.AddConsole()));
using var cancelTokenSource = new CancellationTokenSource();
var cancelToken = cancelTokenSource.Token;

var receiverOptions = new ReceiverOptions()
{
    AllowedUpdates = new[]
        { UpdateType.Message, UpdateType.InlineQuery, UpdateType.ChosenInlineResult, UpdateType.CallbackQuery },
    ThrowPendingUpdates = true
};

new Text();
var db = new TelegramDB();
//db.Database.EnsureDeleted();
db.Database.EnsureCreated();

var updateHandler = new UpdateHandler(bot, logger);
bot.StartReceiving(
    updateHandler: updateHandler.HandleUpdateAsync,
    pollingErrorHandler: updateHandler.HandlePollingErrorAsync,
    receiverOptions: receiverOptions,
    cancellationToken: cancelTokenSource.Token);

Task.Factory.StartNew(() =>
{
    while (!cancelToken.IsCancellationRequested)
    {
        var toDelete = new List<int>();
        db.CheckTime(ref toDelete);
        db.SaveChangesAsync(cancelToken);
        foreach (var record in toDelete)
        {
            var message = updateHandler.SendNotification(bot, record, cancelToken).Result;
            if (message == null) continue;
            logger.LogInformation(
                $"Send notification about feedback to {message.Chat.Id} about order #{record}");
            Thread.Sleep(5000);
        }

        Thread.Sleep(300000);
    }
}, cancelToken);

app.Run();

Console.ReadLine();
cancelTokenSource.Cancel();

public class BotConfig
{
    public string BotToken { get; init; } = default!;
}