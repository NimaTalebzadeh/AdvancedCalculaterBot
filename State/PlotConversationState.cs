namespace AdvancedCalculaterBot.State;

/// <summary>
/// Tracks the multi-step plot conversation state per chat.
/// Flow: expression -> ask start -> ask end -> ask step -> plot
/// </summary>
public class PlotConversationManager
{
    private readonly Dictionary<long, PlotState> _states = new();
    private readonly object _lock = new();

    public PlotState? GetState(long chatId)
    {
        lock (_lock)
        {
            return _states.TryGetValue(chatId, out var state) ? state : null;
        }
    }

    public PlotState SetState(long chatId, PlotState state)
    {
        lock (_lock)
        {
            _states[chatId] = state;
            return state;
        }
    }

    public void RemoveState(long chatId)
    {
        lock (_lock)
        {
            _states.Remove(chatId);
        }
    }
}

public class PlotState
{
    public string Expression { get; set; } = "";
    public double? Start { get; set; }
    public double? End { get; set; }
    public double? Step { get; set; }
    public PlotPhase Phase { get; set; } = PlotPhase.AskStart;
}

public enum PlotPhase
{
    AskStart,
    AskEnd,
    AskStep,
    Done
}
