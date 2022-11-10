using Microsoft.Extensions.Configuration;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

using Telegram.UpdateHandler1;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var botConfig = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build().GetSection("BotConfig").Get<BotConfig>();

var bot = new TelegramBotClient(botConfig.BotToken);
using var cancelTokenSource = new CancellationTokenSource();
var receiverOptions = new ReceiverOptions()
{
    AllowedUpdates = new[] { UpdateType.Message },
    ThrowPendingUpdates = true
};

var updateHandler = new UpdateHandler(bot);

bot.StartReceiving(
    updateHandler: updateHandler.HandleUpdateAsync,
    pollingErrorHandler: updateHandler.HandlePollingErrorAsync,
    receiverOptions: receiverOptions,
    cancellationToken: cancelTokenSource.Token);


app.Run();




public class BotConfig
{
    public string BotToken { get; set; } = null!;
}