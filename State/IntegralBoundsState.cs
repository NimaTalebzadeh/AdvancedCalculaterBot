namespace AdvancedCalculatorBot.State;

/// <summary>
/// Manages multi-step conversation for asking integral bounds after
/// a non-elementary indefinite integral is shown.
/// Flow: indefinite result shown -> AskLower -> AskUpper -> compute & display
/// </summary>
public class IntegralBoundsManager
{
    private readonly Dictionary<long, IntegralBoundsState> _states = new();
    private readonly object _lock = new();

    public IntegralBoundsState? GetState(long chatId)
    {
        lock (_lock)
            return _states.TryGetValue(chatId, out var state) ? state : null;
    }

    public void SetState(long chatId, IntegralBoundsState state)
    {
        lock (_lock)
            _states[chatId] = state;
    }

    public void RemoveState(long chatId)
    {
        lock (_lock)
            _states.Remove(chatId);
    }
}

public class IntegralBoundsState
{
    public string Expression { get; set; } = "";
    public string Variable { get; set; } = "x";
    public string IndefiniteResult { get; set; } = "";
    public double? Lower { get; set; }
    public double? Upper { get; set; }
    public IntegralBoundsPhase Phase { get; set; } = IntegralBoundsPhase.AskLower;
}

public enum IntegralBoundsPhase
{
    AskLower,
    AskUpper,
    Done
}
