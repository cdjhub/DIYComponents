using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DIYComponents.DistributeLock;

/// <summary>
/// 基于Redis的分布式锁，实现互斥性,可重入性，看门狗
/// </summary>
public class DistributedLock
{
    /// <summary>
    /// 默认超时时间
    /// </summary>
    private static readonly TimeSpan DEFAULT_EXPIRE_TIME = TimeSpan.FromSeconds(30);

    /// <summary>
    /// 每次续费时长
    /// </summary>
    private static readonly TimeSpan DEFAULT_RENEW_EXPIRE_TIME = TimeSpan.FromSeconds(10);

    // 如果默认值有更改，因为解锁校验ID，需要把Redis中旧版本的所有默认ID改为新版的
    private static readonly string DEFAULT_LOCK_ID = "1";

    private readonly ConfigurationOptions _options;

    private readonly BackgroundTask _backgroundTask;

    private readonly Dictionary<string, CancellationTokenSource> _tokenSources;

    // <锁名，(线程Id，计数器)>
    private readonly Dictionary<string, (int, int)> _threadCounter;

    private readonly object _lockCounters = new object();

    public DistributedLock(string redisHost, string pswd, int? redisPort = null)
    {
        _backgroundTask = new BackgroundTask();
        _tokenSources = new Dictionary<string, CancellationTokenSource>();
        _threadCounter = new Dictionary<string, (int, int)>();

        _options = new ConfigurationOptions()
        {
            EndPoints = { { redisHost, redisPort ?? 6379 } },
            Password = pswd,
        };
    }

    async private Task<bool> LockAsync(string lockKey, int threadId, string? lockId = null, TimeSpan? expireTime = null)
    {
        using var conn = ConnectionMultiplexer.Connect(_options);
        var db = conn.GetDatabase();
        expireTime = expireTime ?? DEFAULT_EXPIRE_TIME;
        lockId = lockId ?? DEFAULT_LOCK_ID;

        if (db.StringSet(lockKey, lockId, expireTime, When.NotExists, CommandFlags.None))
        {
            lock (_lockCounters)
                _threadCounter.Add(lockKey, (threadId, 1));
            // 只有大于续费时间才开启看门狗
            if (expireTime > DEFAULT_RENEW_EXPIRE_TIME)
            {
                JoinBackgroundTask(lockKey, expireTime ?? DEFAULT_EXPIRE_TIME);
            }
            return true;
        }

        return false;
    }


    public bool Lock(string lockKey, string? lockId = null, TimeSpan? expireTime = null)
    {
        int threadId = Thread.CurrentThread.ManagedThreadId;
        // 如果本地已经持有了
        if (_threadCounter.ContainsKey(lockKey))
        {
            lock (_lockCounters)
            {
                if (_threadCounter.ContainsKey(lockKey))
                {
                    (int tId, int counter) = _threadCounter[lockKey];
                    if (tId != threadId) return false;

                    _threadCounter[lockKey] = (tId, counter + 1);
                    return true;
                }
            }
        }

        return LockAsync(lockKey, threadId, lockId, expireTime).Result;
    }

    async private Task UnLockAsync(string lockKey, string? lockId = null)
    {
        using var conn = ConnectionMultiplexer.Connect(_options);
        var db = conn.GetDatabase();

        string storedId = db.StringGet(lockKey).ToString();
        lockId = lockId ?? DEFAULT_LOCK_ID;

        // 不是它上的锁不能解
        if (storedId == lockId)
        {
            CancelJoinBackgroundTask(lockKey);
            db.KeyDelete(lockKey);
        }
    }

    public void UnLock(string lockKey, string? lockId = null)
    {
        int threadId = Thread.CurrentThread.ManagedThreadId;
        if (_threadCounter.ContainsKey(lockKey))
        {
            lock (_lockCounters)
            {
                // 如果是本地记录有，肯定是已经获得锁了，所以可以进行本地的删除操作而不判断远程
                // 如果没获得到锁，本地肯定是不会有这个记录的
                if (_threadCounter.ContainsKey(lockKey))
                {
                    (int tId, int counter) = _threadCounter[lockKey];

                    // 不是这个线程加的锁，不释放
                    if (tId != threadId) return;

                    // 是这个线程的锁，释放，减少计数
                    if (counter > 1)
                    {
                        _threadCounter[lockKey] = (tId, counter - 1);
                        // 还有多次重入就不继续往下执行了
                        return;
                    }
                    _threadCounter.Remove(lockKey);
                }
            }
        }

        UnLockAsync(lockKey, lockId).Wait();
    }

    /// <summary>
    /// 加入看门狗
    /// </summary>
    /// <param name="lockKey"></param>
    /// <param name="expireTime">锁的超时时间</param>
    void JoinBackgroundTask(string lockKey, TimeSpan expireTime)
    {
        CancellationTokenSource tokenSource = new CancellationTokenSource();
        _tokenSources.Add(lockKey, tokenSource);

        void task()
        {
            if (!tokenSource.Token.IsCancellationRequested)
            {
                RenewExpireTime(lockKey);
                _backgroundTask.AddTask(task, (int)DEFAULT_RENEW_EXPIRE_TIME.TotalMilliseconds);
            }
        };

        _backgroundTask.AddTask(
            task,
            Math.Max(((int)DEFAULT_RENEW_EXPIRE_TIME.TotalMilliseconds) / 2, (int)expireTime.TotalMilliseconds - 2 * (int)DEFAULT_RENEW_EXPIRE_TIME.TotalMilliseconds)
        );
    }

    /// <summary>
    /// 延长锁时间
    /// </summary>
    /// <param name="lockKey"></param>
    void RenewExpireTime(string lockKey)
    {
        using var conn = ConnectionMultiplexer.Connect(_options);
        var db = conn.GetDatabase();

        if (db.KeyExists(lockKey))
        {
            db.KeyExpire(lockKey, DEFAULT_RENEW_EXPIRE_TIME + DEFAULT_RENEW_EXPIRE_TIME, ExpireWhen.HasExpiry, CommandFlags.None);
        }
    }

    /// <summary>
    /// 取消延长
    /// </summary>
    /// <param name="lockKey"></param>
    void CancelJoinBackgroundTask(string lockKey)
    {
        if (!_tokenSources.ContainsKey(lockKey))
            return;
        _tokenSources[lockKey].Cancel();
        _tokenSources.Remove(lockKey);
    }
}
