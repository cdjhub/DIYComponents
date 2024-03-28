namespace DIYComponents.RateLimit;

public interface ITryConsume
{
    bool TryConsume();
}

public class TryConsumeFactory
{
    public static ITryConsume CreateInstance<T>(int limitCount, TimeSpan period) 
    {
        return typeof(T) switch
        {
            var t when t == typeof(FixedWindow) => new FixedWindow(limitCount, period),
            var t when t == typeof(SlidingWindow) => new SlidingWindow(limitCount, period),
            _ => throw new InvalidOperationException()
        };
    }
}