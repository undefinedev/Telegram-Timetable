using Microsoft.Extensions.Configuration;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InputFiles;
using Telegram.UpdateHandler;

var builder = WebApplication.CreateBuilder(args);
var botConfig = builder.Configuration.GetSection("BotConfig").Get<BotConfig>();
var app = builder.Build();

var bot = new TelegramBotClient(botConfig.BotToken);
using var cancelTokenSource = new CancellationTokenSource();

var receiverOptions = new ReceiverOptions()
{
    AllowedUpdates = new[] { UpdateType.Message },
    ThrowPendingUpdates = true
};

var updateHandler = new UpdateHandler(bot, new Logger<UpdateHandler>(LoggerFactory.Create(logger => logger.AddConsole())));

/*bot.StartReceiving(
    updateHandler: updateHandler.HandleUpdateAsync,
    pollingErrorHandler: updateHandler.HandlePollingErrorAsync,
    receiverOptions: receiverOptions,
    cancellationToken: cancelTokenSource.Token);*/

await bot.SetWebhookAsync(
    url: $"https://176.124.170.112:8443/bot/{botConfig.BotToken}",
    certificate: new InputFileStream( new FileStream("C:\\Users\\alexa\\Desktop\\key\\testssl1.pem", FileMode.Open)),
    allowedUpdates: new[] { UpdateType.Message },
    cancellationToken: cancelTokenSource.Token);


app.Run();

Console.ReadLine();
await bot.DeleteWebhookAsync(cancellationToken: cancelTokenSource.Token);



public class BotConfig
{
    public string BotToken { get; init; } = default!;
}