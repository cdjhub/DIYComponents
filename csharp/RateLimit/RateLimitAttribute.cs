namespace DIYComponents.RateLimit;


[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RateLimitAttribute : Attribute
{
    public int LimitCount { get; }
    public TimeSpan Period { get; }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="limitCount">接口限制最多使用次数</param>
    /// <param name="periodSeconds">限制的单位时间</param>
    public RateLimitAttribute(int limitCount, int periodSeconds)
    {
        LimitCount = limitCount;
        Period = TimeSpan.FromSeconds(periodSeconds);
    }
}
