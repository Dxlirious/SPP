using System;
using System.Collections.Generic;
using System.Threading;

namespace CustomThreadPool
{
    public class ThreadPoolEventArgs : EventArgs
    {
        public string Message { get; }
        public int ActiveThreads { get; }
        public int QueueLength { get; }
        public DateTime Timestamp { get; } = DateTime.Now;

        public ThreadPoolEventArgs(string message, int activeThreads, int queueLength)
        {
            Message = message;
            ActiveThreads = activeThreads;
            QueueLength = queueLength;
        }
    }

    public class CustomThreadPool
    {
        private readonly int _minThreads;
        private readonly int _maxThreads;
        private readonly int _idleTimeoutMs;
        private readonly int _queueThreshold;
        private readonly int _hungThreadTimeoutMs;

        private readonly Queue<Action> _taskQueue = new Queue<Action>();
        private readonly List<Thread> _threads = new List<Thread>();
        private readonly List<DateTime> _threadLastActivity = new List<DateTime>();
        private readonly object _lock = new object();
        private readonly Semaphore _semaphore;
        private readonly Mutex _mutex = new Mutex();

        private bool _shutdown = false;
        private int _activeThreads = 0;

        // ===== СОБЫТИЯ ЖИЗНЕННОГО ЦИКЛА (ЛР4) =====
        public event EventHandler<ThreadPoolEventArgs> ThreadCreated;
        public event EventHandler<ThreadPoolEventArgs> ThreadDestroyed;
        public event EventHandler<ThreadPoolEventArgs> TaskEnqueued;
        public event EventHandler<ThreadPoolEventArgs> TaskStarted;
        public event EventHandler<ThreadPoolEventArgs> TaskCompleted;
        public event EventHandler<ThreadPoolEventArgs> ThreadHungDetected;

        public int ActiveThreads => _activeThreads;
        public int QueueLength { get { lock (_lock) return _taskQueue.Count; } }

        public CustomThreadPool(
            int minThreads = 2,
            int maxThreads = 8,
            int idleTimeoutMs = 3000,
            int queueThreshold = 3,
            int hungThreadTimeoutMs = 5000)
        {
            _minThreads = minThreads;
            _maxThreads = maxThreads;
            _idleTimeoutMs = idleTimeoutMs;
            _queueThreshold = queueThreshold;
            _hungThreadTimeoutMs = hungThreadTimeoutMs;

            _semaphore = new Semaphore(maxThreads, maxThreads);

            // Создаём минимальное число потоков сразу
            for (int i = 0; i < _minThreads; i++)
                CreateThread();

            StartMonitor();
            StartHungThreadWatchdog();
        }

        //ДОБАВЛЕНИЕ ЗАДАЧИ В ОЧЕРЕДЬ
        public void Enqueue(Action task)
        {
            lock (_lock)
            {
                _taskQueue.Enqueue(task);
                Log($"[QUEUE] задача добавлена. Очередь: {_taskQueue.Count}");

                TaskEnqueued?.Invoke(this, new ThreadPoolEventArgs(
                    "Task enqueued", _activeThreads, _taskQueue.Count));

                // Динамическое масштабирование
                if (_taskQueue.Count >= _queueThreshold && _threads.Count < _maxThreads)
                {
                    Log($"[SCALE UP] очередь {_taskQueue.Count} >= {_queueThreshold}, создаю поток");
                    CreateThread();
                }

                Monitor.Pulse(_lock);
            }
        }

        //  СОЗДАНИЕ ПОТОКА — через new Thread
        private void CreateThread()
        {
            int index = _threads.Count;
            var thread = new Thread(WorkerLoop)
            {
                IsBackground = true,
                Name = $"CustomWorker-{index + 1}"
            };

            _threads.Add(thread);
            _threadLastActivity.Add(DateTime.Now);
            _activeThreads++;

            Log($"[THREAD CREATED] {thread.Name}, активных: {_activeThreads}");

            ThreadCreated?.Invoke(this, new ThreadPoolEventArgs(
                $"Thread created: {thread.Name}", _activeThreads, _taskQueue.Count));

            thread.Start(index);
        }

        // РАБОЧИЙ ЦИКЛ ПОТОКА
        private void WorkerLoop(object indexObj)
        {
            int index = (int)indexObj;

            while (true)
            {
                Action task = null;

                lock (_lock)
                {
                    // Monitor.Wait — системный примитив
                    while (_taskQueue.Count == 0 && !_shutdown)
                    {
                        bool signaled = Monitor.Wait(_lock, _idleTimeoutMs);

                        // Адаптивное сжатие
                        if (!signaled && _threads.Count > _minThreads)
                        {
                            Log($"[SCALE DOWN] {Thread.CurrentThread.Name} простаивал {_idleTimeoutMs}мс, завершается");

                            _activeThreads--;
                            _threads.Remove(Thread.CurrentThread);
                            if (index < _threadLastActivity.Count)
                                _threadLastActivity.RemoveAt(index);

                            ThreadDestroyed?.Invoke(this, new ThreadPoolEventArgs(
                                $"Thread destroyed: {Thread.CurrentThread.Name}",
                                _activeThreads, _taskQueue.Count));

                            return;
                        }
                    }

                    if (_shutdown && _taskQueue.Count == 0) return;

                    if (_taskQueue.Count > 0)
                    {
                        task = _taskQueue.Dequeue();
                        if (index < _threadLastActivity.Count)
                            _threadLastActivity[index] = DateTime.Now;
                    }
                }

                if (task != null)
                {
                    TaskStarted?.Invoke(this, new ThreadPoolEventArgs(
                        $"Task started on {Thread.CurrentThread.Name}",
                        _activeThreads, _taskQueue.Count));

                    // Отказоустойчивость
                    try
                    {
                        task();
                    }
                    catch (Exception ex)
                    {
                        Log($"[ERROR] {Thread.CurrentThread.Name}: {ex.Message}");
                    }

                    TaskCompleted?.Invoke(this, new ThreadPoolEventArgs(
                        $"Task completed on {Thread.CurrentThread.Name}",
                        _activeThreads, _taskQueue.Count));
                }
            }
        }

        //МОНИТОРИНГ
        private void StartMonitor()
        {
            var monitor = new Thread(() =>
            {
                while (!_shutdown)
                {
                    Thread.Sleep(2000);
                    lock (_lock)
                    {
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.WriteLine(
                            $"[MONITOR {DateTime.Now:HH:mm:ss}] " +
                            $"Потоков: {_activeThreads}/{_maxThreads} | " +
                            $"Очередь: {_taskQueue.Count}");
                        Console.ResetColor();
                    }
                }
            })
            {
                IsBackground = true,
                Name = "PoolMonitor"
            };
            monitor.Start();
        }

        // ЗАМЕНА ЗАВИСШИХ ПОТОКОВ
        private void StartHungThreadWatchdog()
        {
            var watchdog = new Thread(() =>
            {
                while (!_shutdown)
                {
                    Thread.Sleep(_hungThreadTimeoutMs / 2);
                    lock (_lock)
                    {
                        for (int i = _threads.Count - 1; i >= 0; i--)
                        {
                            if (!_threads[i].IsAlive)
                            {
                                Log($"[WATCHDOG] поток {_threads[i].Name} мёртв, заменяю");

                                ThreadHungDetected?.Invoke(this, new ThreadPoolEventArgs(
                                    $"Hung thread replaced: {_threads[i].Name}",
                                    _activeThreads, _taskQueue.Count));

                                _activeThreads--;
                                _threads.RemoveAt(i);
                                if (i < _threadLastActivity.Count)
                                    _threadLastActivity.RemoveAt(i);

                                CreateThread();
                                break;
                            }
                        }
                    }
                }
            })
            {
                IsBackground = true,
                Name = "HungThreadWatchdog"
            };
            watchdog.Start();
        }

        public void Shutdown()
        {
            lock (_lock)
            {
                _shutdown = true;
                Monitor.PulseAll(_lock);
            }
        }

        private static void Log(string message)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] {message}");
            Console.ResetColor();
        }
    }
}