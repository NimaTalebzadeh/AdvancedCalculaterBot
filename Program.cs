using Serilog;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using AdvancedCalculatorBot.Services;
using AdvancedCalculatorBot.Services.Equations;
using AdvancedCalculatorBot.Services.Plot;
using AdvancedCalculatorBot.State;
using AdvancedCalculatorBot.Services.Mathematics;

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
builder.Services.AddSingleton<PlotConversationManager>();
builder.Services.AddSingleton<IntegralBoundsManager>();

var app = builder.Build();

app.UseSerilogRequestLogging();

var botClient = app.Services.GetRequiredService<ITelegramBotClient>();
var tracker = app.Services.GetRequiredService<ComplexityTracker>();
var plotManager = app.Services.GetRequiredService<PlotConversationManager>();
var integralBoundsManager = app.Services.GetRequiredService<IntegralBoundsManager>();
var adminIds = (Environment.GetEnvironmentVariable("ADMIN_IDS") ?? "")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    .Select(id => long.TryParse(id, out var parsed) ? parsed : 0)
    .Where(id => id != 0)
    .ToArray();

var cts = new CancellationTokenSource();

var receiverOptions = new ReceiverOptions
{
    AllowedUpdates = [UpdateType.Message]
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

                    // ── /start command ──
                    if (expression.Equals("/start", StringComparison.OrdinalIgnoreCase))
                    {
                        await bot.SendMessage(chatId: message.Chat.Id,
                            text: "🧮 Advanced Calculator Bot\n\nSolves equations & mathematical expressions.\nType /help to see available commands.",
                            cancellationToken: token);
                        return;
                    }

                    // ── /help command ──
                    if (expression.Equals("/help", StringComparison.OrdinalIgnoreCase))
                    {
                        await bot.SendMessage(chatId: message.Chat.Id,
                            text: "Commands:\n\n"
                                + "/calc <expr> — Evaluate an expression\n"
                                + "Example: /calc 2 + 2 * (3 + 4)\n\n"
                                + "Equation solving — Type the equation directly\n"
                                + "Example: x^2 - 4 = 0\n\n"
                                + "📊 plot(expr) — Plot a function graph\n"
                                + "Example: plot(x^2*sin(x))\n\n"
                                + "Math operations:\n"
                                + "d(expr) — Derivative\n"
                                + "int(expr) — Integral\n"
                                + "lim(expr, point) — Limit\n"
                                + "simplify(expr) — Simplify\n"
                                + "expand(expr) — Expand\n"
                                + "factor(expr) — Factorize\n"
                                + "taylor(expr, var, pt, n) — Taylor series\n\n"
                                + "⚙️ /top — Top complex expressions (admin only)",
                            cancellationToken: token);
                        return;
                    }

                    // ── Integral bounds conversation state machine ──
                    long chatId = message.Chat.Id;
                    var integralState = integralBoundsManager.GetState(chatId);

                    if (integralState != null)
                    {
                        string param = text.Trim();

                        if (param.Equals("/cancel", StringComparison.OrdinalIgnoreCase))
                        {
                            integralBoundsManager.RemoveState(chatId);
                            await bot.SendMessage(chatId: chatId, text: "Definite integral cancelled.", cancellationToken: token);
                            return;
                        }

                        try
                        {
                            switch (integralState.Phase)
                            {
                                case IntegralBoundsPhase.AskLower:
                                    if (!double.TryParse(param, System.Globalization.NumberStyles.Any,
                                            System.Globalization.CultureInfo.InvariantCulture, out double lowerVal))
                                    {
                                        await bot.SendMessage(chatId: chatId,
                                            text: "Invalid number. Please enter the lower bound (e.g. 0):",
                                            cancellationToken: token);
                                        return;
                                    }
                                    integralState.Lower = lowerVal;
                                    integralState.Phase = IntegralBoundsPhase.AskUpper;
                                    integralBoundsManager.SetState(chatId, integralState);
                                    await bot.SendMessage(chatId: chatId,
                                        text: $"Lower bound = {lowerVal:G}. Now enter the upper bound:",
                                        cancellationToken: token);
                                    return;

                                case IntegralBoundsPhase.AskUpper:
                                    if (!double.TryParse(param, System.Globalization.NumberStyles.Any,
                                            System.Globalization.CultureInfo.InvariantCulture, out double upperVal))
                                    {
                                        await bot.SendMessage(chatId: chatId,
                                            text: "Invalid number. Please enter the upper bound (e.g. 1):",
                                            cancellationToken: token);
                                        return;
                                    }
                                    integralState.Upper = upperVal;
                                    integralBoundsManager.RemoveState(chatId);

                                    // Compute all three numerical methods
                                    try
                                    {
                                        var comparison = NumericalIntegrator.CompareMethods(
                                            integralState.Expression,
                                            integralState.Variable,
                                            integralState.Lower!.Value,
                                            upperVal);

                                        string result = $@"📐 Definite integral ∫_{{a}}^{{b}} {integralState.Expression} d{integralState.Variable}

Indefinite form:
{integralState.IndefiniteResult}

{comparison.Format()}";

                                        // Show the result (adaptive simpson is most reliable)
                                        await bot.SendMessage(chatId: chatId, text: result, cancellationToken: token);
                                    }
                                    catch (Exception numEx)
                                    {
                                        await bot.SendMessage(chatId: chatId,
                                            text: $"Error computing numerical integral: {numEx.Message}",
                                            cancellationToken: token);
                                    }
                                    return;
                            }
                        }
                        catch (Exception)
                        {
                            integralBoundsManager.RemoveState(chatId);
                            await bot.SendMessage(chatId: chatId,
                                text: "Definite integral cancelled due to an error.",
                                cancellationToken: token);
                            return;
                        }
                    }

                    // ── Plot conversation state machine ──
                    var plotState = plotManager.GetState(chatId);

                    if (plotState != null)
                    {
                        // User is mid-conversation for a plot
                        string param = text.Trim();

                        // Allow canceling with /cancel
                        if (param.Equals("/cancel", StringComparison.OrdinalIgnoreCase))
                        {
                            plotManager.RemoveState(chatId);
                            await bot.SendMessage(chatId: chatId, text: "Plot cancelled.", cancellationToken: token);
                            return;
                        }

                        try
                        {
                            switch (plotState.Phase)
                            {
                                case PlotPhase.AskStart:
                                    if (!double.TryParse(param, System.Globalization.NumberStyles.Any,
                                            System.Globalization.CultureInfo.InvariantCulture, out double startVal))
                                    {
                                        await bot.SendMessage(chatId: chatId, text: "Invalid number. Please enter a numeric start value (e.g. -10):", cancellationToken: token);
                                        return;
                                    }
                                    plotState.Start = startVal;
                                    plotState.Phase = PlotPhase.AskEnd;
                                    plotManager.SetState(chatId, plotState);
                                    await bot.SendMessage(chatId: chatId, text: "Enter end value:", cancellationToken: token);
                                    return;

                                case PlotPhase.AskEnd:
                                    if (!double.TryParse(param, System.Globalization.NumberStyles.Any,
                                            System.Globalization.CultureInfo.InvariantCulture, out double endVal))
                                    {
                                        await bot.SendMessage(chatId: chatId, text: "Invalid number. Please enter a numeric end value (e.g. 10):", cancellationToken: token);
                                        return;
                                    }
                                    plotState.End = endVal;
                                    plotState.Phase = PlotPhase.AskStep;
                                    plotManager.SetState(chatId, plotState);
                                    await bot.SendMessage(chatId: chatId, text: "Enter step value (e.g. 0.01):", cancellationToken: token);
                                    return;

                                case PlotPhase.AskStep:
                                    if (!double.TryParse(param, System.Globalization.NumberStyles.Any,
                                            System.Globalization.CultureInfo.InvariantCulture, out double stepVal) || stepVal <= 0)
                                    {
                                        await bot.SendMessage(chatId: chatId, text: "Invalid step. Please enter a positive number (e.g. 0.01):", cancellationToken: token);
                                        return;
                                    }
                                    plotState.Step = stepVal;
                                    plotManager.RemoveState(chatId);

                                    // Generate the plot
                                    try
                                    {
                                        byte[] imageBytes = PlotService.GeneratePlot(
                                            plotState.Expression,
                                            plotState.Start!.Value,
                                            plotState.End!.Value,
                                            plotState.Step.Value);

                                        using var stream = new MemoryStream(imageBytes);
                                        var photo = new Telegram.Bot.Types.InputFileStream(stream, "plot.png");
                                        await bot.SendPhoto(
                                            chatId: chatId,
                                            photo: photo,
                                            caption: $"y = {plotState.Expression}\nRange: [{plotState.Start}, {plotState.End}], Step: {plotState.Step}",
                                            cancellationToken: token);
                                    }
                                    catch (Exception plotEx)
                                    {
                                        await bot.SendMessage(chatId: chatId,
                                            text: $"Error generating plot: {plotEx.Message}",
                                            cancellationToken: token);
                                    }
                                    return;
                            }
                        }
                        catch (Exception)
                        {
                            plotManager.RemoveState(chatId);
                            await bot.SendMessage(chatId: chatId,
                                text: "Plot cancelled due to an error. Try again with plot(expression).",
                                cancellationToken: token);
                            return;
                        }
                    }

                    // ── Detect new plot() command ──
                    string exprForPlot = expression;
                    if (exprForPlot.StartsWith("/calc", StringComparison.OrdinalIgnoreCase))
                        exprForPlot = exprForPlot.Substring(5).Trim();

                    var plotMatch = System.Text.RegularExpressions.Regex.Match(
                        exprForPlot, @"^plot\s*\((.+)\)$",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                    if (plotMatch.Success)
                    {
                        string plotExpr = plotMatch.Groups[1].Value.Trim();
                        if (string.IsNullOrWhiteSpace(plotExpr))
                        {
                            await bot.SendMessage(chatId: chatId,
                                text: "Please provide an expression. Example: plot(x^2*sin(x))",
                                cancellationToken: token);
                            return;
                        }

                        var newState = new PlotState
                        {
                            Expression = plotExpr,
                            Phase = PlotPhase.AskStart
                        };
                        plotManager.SetState(chatId, newState);
                        await bot.SendMessage(chatId: chatId,
                            text: $"Plotting: y = {plotExpr}\nEnter start value (e.g. -10):",
                            cancellationToken: token);
                        return;
                    }

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
                        // Split by newlines and process each line
                        var lines = expression.Split('\n')
                            .Select(l => l.Trim())
                            .Where(l => l.Length > 0)
                            .ToArray();

                        var results = new List<string>();

                        foreach (var line in lines)
                        {
                            try
                            {
                                string resultStr;
                                string expr = line.Trim();

                                if (expr.StartsWith("/calc", StringComparison.OrdinalIgnoreCase))
                                    expr = expr.Substring(5).Trim();

                                // Strip trailing commas (common when listing equations)
                                expr = expr.TrimEnd(',').Trim();

                                if (string.IsNullOrWhiteSpace(expr))
                                    continue;

                                // Check if it's a mathematical operation (d, int, lim, simplify, expand, factor)
                                using var exprTimeout = CancellationTokenSource.CreateLinkedTokenSource(token);
                                // Complex symbolic derivatives/integrals can take several seconds.
                                // Keep a timeout to prevent total bot hangs, but allow enough
                                // time for advanced calculus expressions to complete.
                                exprTimeout.CancelAfter(TimeSpan.FromSeconds(60));

                                var evaluationTask = Task.Run(() =>
                                {
                                    if (MathOperationHandler.IsMathematicalOperation(expr))
                                    {
                                        var calculatorService = new CalculatorService(expr);
                                        var result = calculatorService.Evaluate();
                                        if (result is MathResult mr && mr.Success && mr.IsNonElementary)
                                            return result;
                                        tracker.Add(expr);
                                        return $"{expr} = {result}";
                                    }

                                    if (SystemOfEquationsSolver.IsSystemOfEquations(expr))
                                        return SystemOfEquationsSolver.Solve(expr);

                                    bool isEquation = expr.Contains('=')
                                                        && (expr.Contains('x', StringComparison.OrdinalIgnoreCase)
                                                            || expr.Contains('y', StringComparison.OrdinalIgnoreCase)
                                                            || expr.Contains('z', StringComparison.OrdinalIgnoreCase));

                                    if (isEquation)
                                        return EquationSolverService.Solve(expr);

                                    var service = new CalculatorService(expr);
                                    var eval = service.Evaluate();
                                    tracker.Add(expr);
                                    return $"{expr} = {eval}";
                                }, exprTimeout.Token);

                                var completed = await Task.WhenAny(evaluationTask, Task.Delay(Timeout.Infinite, exprTimeout.Token));

                                if (completed == evaluationTask && evaluationTask.IsCompletedSuccessfully)
                                {
                                    var evalResult = evaluationTask.Result;

                                    // Handle non-elementary integral: show indefinite form, ask for bounds
                                    if (evalResult is MathResult mr && mr.Success && mr.IsNonElementary)
                                    {
                                        await bot.SendMessage(chatId: chatId,
                                            text: $"\u222B {mr.OriginalExpression} d{mr.IntegralVariable} = {mr.FormattedValue}\n\n\uD83E\uDDE0 Non-elementary integral (involves special functions).\n\nTo compute the definite numerical value, enter the lower bound:",
                                            cancellationToken: token);

                                        var newState = new IntegralBoundsState
                                        {
                                            Expression = mr.OriginalExpression ?? expr,
                                            Variable = mr.IntegralVariable ?? "x",
                                            IndefiniteResult = mr.FormattedValue ?? "",
                                            Phase = IntegralBoundsPhase.AskLower
                                        };
                                        integralBoundsManager.SetState(chatId, newState);
                                        return;
                                    }

                                    resultStr = evalResult.ToString() ?? "";
                                }
                                else
                                    resultStr = $"{expr} = TIMEOUT";

                                results.Add(resultStr);
                            }
                            catch (Exception ex)
                            {
                                results.Add($"Error: {line} => {ex.Message}");
                            }
                        }

                        response = results.Count > 0
                            ? string.Join("\n", results)
                            : "Please provide a mathematical expression.";
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error calculating expression");
                    response = "Sorry, there was an error processing your calculation.";
                }

                const int telegramLimit = 4000;
                if (response.Length <= telegramLimit)
                {
                    await bot.SendMessage(
                        chatId: message.Chat.Id,
                        text: response,
                        cancellationToken: token);
                }
                else
                {
                    for (int i = 0; i < response.Length; i += telegramLimit)
                    {
                        string chunk = response.Substring(i, Math.Min(telegramLimit, response.Length - i));
                        await bot.SendMessage(
                            chatId: message.Chat.Id,
                            text: chunk,
                            cancellationToken: token);
                    }
                }
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