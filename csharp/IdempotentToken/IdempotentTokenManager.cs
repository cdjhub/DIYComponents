using System.Collections.Concurrent;

namespace DIYComponents.IdempotentToken;

/// <summary>
/// 令牌桶，防止重放攻击，每个请求只能被消费一次
/// </summary>
public class IdempotentTokenManager
{
    public const string CHAR_SET = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
    private static readonly Random RANDOM = new Random();

    private static BackgroundTask backgroundService = new BackgroundTask();
    public static int LimitCount = 10;
    // 统一生成令牌的时间
    public static int GenerateSeconds = 6;
    // 统一清理令牌的时间
    public static int ClearSeconds = 10;

    public static string GenerateRandomString(int len = 8)
    {
        string res = "";
        for(int i = 0; i < len; i ++)
        {
            res += CHAR_SET[RANDOM.Next() % CHAR_SET.Length];
        }
        return res;
    }

    private ConcurrentDictionary<string, IdempotentTokenStatus> _cache = new();

    public IdempotentTokenManager()
    {
        GenerateTokens();
        void addTokenTask()
        {
            GenerateTokens();
            backgroundService.AddTask(addTokenTask, GenerateSeconds * 1000);
        }

        void clearTokenTask()
        {
            ClearTokens();
            backgroundService.AddTask(clearTokenTask, ClearSeconds * 1000);
        }

        backgroundService.AddTask(addTokenTask, GenerateSeconds * 1000);
        backgroundService.AddTask(clearTokenTask, GenerateSeconds * 1000);
    }

    private void GenerateTokens()
    {
        while(_cache.Count < LimitCount)
        {
            GenerateOneToken();
        }

        Console.WriteLine($"当前时间：{DateTime.Now}，{_cache.Count}");
    }

    /// <summary>
    /// 生成一个令牌
    /// </summary>
    /// <returns></returns>
    private void GenerateOneToken()
    {
        string token = GenerateRandomString();
        while(_cache.TryGetValue(token, out _))
            token = GenerateRandomString();
        _cache.TryAdd(token, IdempotentTokenStatus.New);
    }

    /// <summary>
    /// 必须拿旧换新的
    /// </summary>
    /// <param name="oldToken"></param>
    /// <returns></returns>
    async public Task<string?> GetToken(string oldToken)
    {
        if(_cache.TryUpdate(oldToken, IdempotentTokenStatus.Completed, IdempotentTokenStatus.Used))
            return await GetToken();
        return null;
    }

    public void CompleteToken(string token)
    {
        _cache.TryUpdate(token, IdempotentTokenStatus.Completed, IdempotentTokenStatus.Used);
    }

    /// <summary>
    /// 新用户尝试获取一个令牌
    /// </summary>
    /// <returns></returns>
    async public Task<string> GetToken()
    {
        while (_cache.Count <= 0)
        {
            Thread.Sleep(10);
        }

        bool gotToken = false;
        string token = "";
        await Task.Run(() =>
        {
            while (!gotToken)
            {
                foreach (var kv in _cache)
                {
                    if (kv.Value == IdempotentTokenStatus.New)
                    {
                        gotToken = true;
                        token = kv.Key;
                        _cache.TryUpdate(token, IdempotentTokenStatus.Used, IdempotentTokenStatus.New);
                        break;
                    }
                }
                if (!gotToken)
                    Task.Delay(10);
            }
        });

        return token;
    }

    /// <summary>
    /// 验证令牌是否有效，并删除过期令牌
    /// </summary>
    /// <param name="token"></param>
    /// <returns></returns>
    public bool ValidataToken(string token)
    {
        if(_cache.TryUpdate(token, IdempotentTokenStatus.Used, IdempotentTokenStatus.New))
            return true;
        return false;
    }

    public void ClearTokens()
    {
        foreach(var kv in _cache)
        {
            if(kv.Value == IdempotentTokenStatus.Completed)
            {
                _cache.TryRemove(kv);
            }
        }
    }
}


public enum IdempotentTokenStatus 
{
    New = 0,
    Used = 1,
    Completed = 2,
}