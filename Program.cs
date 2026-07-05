using System.Text.Json;
using Serilog;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using AdvancedCalculaterBot.Services;
using AdvancedCalculaterBot.State;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls("http://0.0.0.0:" + (Environment.GetEnvironmentVariable("PORT") ?? "5000"));

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddSingleton<ITelegramBotClient>(sp =>
{
    var token = Environment.GetEnvironmentVariable("TELEGRAM_BOTTOKEN");
    if (string.IsNullOrWhiteSpace(token))
    {
        throw new InvalidOperationException("Set TELEGRAM_BOTTOKEN environment variable.");
    }
    return new TelegramBotClient(token);
});

builder.Services.AddSingleton<ComplexityTracker>();

var app = builder.Build();

app.UseSerilogRequestLogging();

var botClient = app.Services.GetRequiredService<ITelegramBotClient>();
var tracker = app.Services.GetRequiredService<ComplexityTracker>();
var adminIds = (Environment.GetEnvironmentVariable("ADMIN_IDS") ?? "")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    .Select(id => long.TryParse(id, out var parsed) ? parsed : 0)
    .Where(id => id != 0)
    .ToArray();

var cts = new CancellationTokenSource();

var receiverOptions = new ReceiverOptions
{
    AllowedUpdates = Array.Empty<UpdateType>()
};

botClient.StartReceiving(
    updateHandler: async (bot, update, token) =>
    {
        if (update.Message is not { Text: { } text } message)
            return;

        Log.Information("Received message from {User}: {Text}", message.From?.Username, text);

        string response;
        try
        {
            string expression = text.Trim();

            if (expression.Equals("/top", StringComparison.OrdinalIgnoreCase))
            {
                if (message.From != null && adminIds.Contains(message.From.Id))
                {
                    var top = tracker.GetTop();
                    var list = string.Join("\n", top.Select((item, i) =>
                        $"{i + 1}. {item.Expression} (score: {item.Score})"));
                    response = top.Any()
                        ? $"Top 10 complex questions:\n{list}"
                        : "No complex questions recorded.";
                }
                else
                {
                    response = "You are not authorized to view the top list.";
                }
                await bot.SendMessage(chatId: message.Chat.Id, text: response, cancellationToken: token);
                return;
            }

            if (expression.StartsWith("/calc", StringComparison.OrdinalIgnoreCase))
            {
                expression = expression.Substring(5).Trim();
            }

            if (string.IsNullOrWhiteSpace(expression))
            {
                response = "Please provide a mathematical expression.\nExample: /calc 2 + 2 * (3 + 4)";
            }
            else
            {
                try
                {
                    tracker.Add(expression);
                    var calculatorService = new CalculatorService(expression);
                    var result = calculatorService.Evaluate();
                    response = $"{expression} = {result}";
                }
                catch
                {
                    response = $"Sorry, I couldn't understand \"{expression}\" as a mathematical expression.";
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error calculating expression");
            response = "Sorry, there was an error processing your calculation.";
        }

        await bot.SendMessage(
            chatId: message.Chat.Id,
            text: response,
            cancellationToken: token);
    },
    errorHandler: async (bot, exception, token) =>
    {
        Log.Error(exception, "Error handling update");
        await Task.CompletedTask;
    },
    receiverOptions: receiverOptions,
    cancellationToken: cts.Token);

var me = await botClient.GetMe(cts.Token);
Log.Information("Bot started as @{Username}", me.Username);

app.Map("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

app.Lifetime.ApplicationStopping.Register(() =>
{
    cts.Cancel();
    Log.Information("Bot shutting down...");
});

app.Run();
