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
using var cancelTokenSource = new CancellationTokenSource();

var receiverOptions = new ReceiverOptions()
{
    AllowedUpdates = new[] { UpdateType.Message, UpdateType.InlineQuery, UpdateType.ChosenInlineResult, UpdateType.CallbackQuery },
    ThrowPendingUpdates = true
};

new Text();
var db = new TelegramDB();
db.Database.EnsureDeleted();
db.Database.EnsureCreated();

var updateHandler = new UpdateHandler(bot, new Logger<UpdateHandler>(LoggerFactory.Create(logger => logger.AddConsole())));
//bot.DeleteWebhookAsync();
bot.StartReceiving(
    updateHandler: updateHandler.HandleUpdateAsync,
    pollingErrorHandler: updateHandler.HandlePollingErrorAsync,
    receiverOptions: receiverOptions,
    cancellationToken: cancelTokenSource.Token);



//app.Map("/", () => "Hello there");
//app.Map("/hello", () => "Hi");


/*await bot.SetWebhookAsync(
    url: $"https://67e7-213-87-154-147.eu.ngrok.io/bot/{botConfig.BotToken}",
    certificate: new InputFileStream( new FileStream("C:\\Users\\undefinedev\\Desktop\\keys\\testsslt.pem", FileMode.Open)),
    allowedUpdates: new[] { UpdateType.Message },
    cancellationToken: cancelTokenSource.Token);*/



app.Run();

Console.ReadLine();




public class BotConfig
{
    public string BotToken { get; init; } = default!;
}