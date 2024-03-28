using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DIYComponents.DistributeLock;


// 基于时间轮的后台定时任务
public class BackgroundTask
{
    private static readonly int DEFAULT_INTERVAL = 100;
    private static readonly int DEFAULT_WHELLSIZE = 600;

    private AutoResetEvent _autoResetEvent = new AutoResetEvent(false);
    // 时间间隔，单位毫秒
    private readonly int _interval;
    // 时间轮的大小
    private readonly int _wheelSize;
    // 任务桶
    private readonly List<Action>[] _buckets;
    // 当前刻度
    private int _currentTick;
    private readonly CancellationTokenSource _cancellationTokenSource;

    public BackgroundTask(int? interval = null, int? wheelSize = null)
    {
        _interval = interval ?? DEFAULT_INTERVAL;
        _wheelSize = wheelSize ?? DEFAULT_WHELLSIZE;
        _buckets = new List<Action>[_wheelSize];
        _cancellationTokenSource = new CancellationTokenSource();
        _currentTick = 0;

        for (int i = 0; i < _wheelSize; i++)
        {
            _buckets[i] = new List<Action>();
        }

        Thread backgroundThread = new Thread(StartTimer);
        backgroundThread.IsBackground = true;
        backgroundThread.Start();
    }

    /// <summary>
    /// 添加任务
    /// </summary>
    /// <param name="task">任务</param>
    /// <param name="delay">毫秒数</param>
    public void AddTask(Action task, int delay)
    {
        int ticks = (delay / _interval + _currentTick) % _wheelSize;
        _buckets[ticks].Add(task);
    }

    public void Stop()
    {
        _cancellationTokenSource.Cancel();
    }

    private async void StartTimer()
    {
        while (!_cancellationTokenSource.Token.IsCancellationRequested)
        {
            if (_buckets[_currentTick].Count > 0)
            {
                foreach (var task in _buckets[_currentTick])
                    await Task.Run(task);
                _buckets[_currentTick].Clear();
            }

            _autoResetEvent.WaitOne(_interval);
            _currentTick = (_currentTick + 1) % _wheelSize;
        }
    }
}
