namespace DIYComponents.RateLimit;

internal class FixedWindow : ITryConsume
{
    private readonly int _limitCount;
    private readonly TimeSpan _period;
    private readonly object _lock = new object();

    private DateTime _windowStart = DateTime.UtcNow;
    private int _count = 0;

    public FixedWindow(int limit, TimeSpan period)
    {
        _limitCount = limit;
        _period = period;
    }

    public bool TryConsume()
    {
        lock (_lock)
        {
            var now = DateTime.UtcNow;
            if (now > _windowStart + _period)
            {
                _windowStart = now;
                _count = 0;
            }

            if (_count < _limitCount)
            {
                _count++;
                return true;
            }
            return false;
        }
    }
}
