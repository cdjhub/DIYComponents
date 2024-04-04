namespace DIYComponents.RateLimit;

public class SlidingWindow : ITryConsume
{
    private readonly object _lock = new object();
    private readonly int _limitCount;
    private readonly TimeSpan _period;

    private Queue<DateTime> _timeRecord = new Queue<DateTime>();

    public SlidingWindow(int limitCount, TimeSpan period)
    {
        _limitCount = limitCount;
        _period = period;
    }

    public bool TryConsume()
    {
        lock (_lock)
        {
            var now = DateTime.UtcNow;
            while (_timeRecord.Count > 0 && now > _timeRecord.Peek() + _period)
                _timeRecord.Dequeue();

            if (_timeRecord.Count >= _limitCount)
            {
                return false;
            }

            _timeRecord.Enqueue(now);

            return true;
        }
    }
}
