using System.Collections.Generic;

namespace AdvancedCalculaterBot.State;

public class ComplexityTracker
{
    private readonly object _lock = new();
    private readonly List<ComplexityEntry> _entries = new();

    private const int MaxItems = 10;

    public void Add(string expression)
    {
        int score = ComplexityCalculator.Compute(expression);
        var entry = new ComplexityEntry
        {
            Expression = expression,
            Score = score,
            Timestamp = DateTime.UtcNow
        };

        lock (_lock)
        {
            _entries.Add(entry);

            _entries.Sort((a, b) =>
            {
                int cmp = b.Score.CompareTo(a.Score);
                return cmp != 0 ? cmp : b.Timestamp.CompareTo(a.Timestamp);
            });

            if (_entries.Count > MaxItems)
            {
                _entries.RemoveRange(MaxItems, _entries.Count - MaxItems);
            }
        }
    }

    public List<ComplexityEntry> GetTop()
    {
        lock (_lock)
        {
            return new List<ComplexityEntry>(_entries);
        }
    }
}

public class ComplexityEntry
{
    public string Expression { get; set; } = "";
    public int Score { get; set; }
    public DateTime Timestamp { get; set; }
}
