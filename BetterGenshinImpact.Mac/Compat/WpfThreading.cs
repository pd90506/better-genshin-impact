using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using BetterGenshinImpact.Platform;

namespace System.Windows.Threading
{
    public enum DispatcherPriority
    {
        Invalid = -1,
        Inactive = 0,
        SystemIdle = 1,
        ApplicationIdle = 2,
        ContextIdle = 3,
        Background = 4,
        Input = 5,
        Loaded = 6,
        Render = 7,
        DataBind = 8,
        Normal = 9,
        Send = 10,
    }

    public enum DispatcherOperationStatus
    {
        Pending,
        Aborted,
        Completed,
        Executing,
    }

    public class DispatcherOperation
    {
        internal DispatcherOperation(Task task)
        {
            Task = task;
        }

        public Task Task { get; }

        public DispatcherOperationStatus Status => Task.IsCompleted ? DispatcherOperationStatus.Completed : DispatcherOperationStatus.Pending;

        public TaskAwaiter GetAwaiter() => Task.GetAwaiter();

        public DispatcherOperationStatus Wait()
        {
            Task.Wait();
            return Status;
        }

        public bool Abort() => false;
    }

    public sealed class DispatcherOperation<TResult> : DispatcherOperation
    {
        internal DispatcherOperation(Task<TResult> task) : base(task)
        {
            Task = task;
        }

        public new Task<TResult> Task { get; }

        public TResult Result => Task.GetAwaiter().GetResult();

        public new TaskAwaiter<TResult> GetAwaiter() => Task.GetAwaiter();
    }

    /// <summary>
    /// WPF Dispatcher 的替代实现，所有操作转发到 <see cref="UiPlatform.UiThread"/>
    /// </summary>
    public sealed class Dispatcher
    {
        public static Dispatcher Instance { get; } = new();

        public static Dispatcher CurrentDispatcher => Instance;

        public Thread Thread => Thread.CurrentThread;

        public bool HasShutdownStarted { get; internal set; }

        public bool HasShutdownFinished { get; internal set; }

        public bool CheckAccess() => UiPlatform.UiThread.CheckAccess();

        public void VerifyAccess()
        {
            if (!CheckAccess())
            {
                throw new InvalidOperationException("调用线程无法访问此对象，因为另一个线程拥有该对象。");
            }
        }

        public static void Run()
        {
            UiPlatform.UiThread.RunNestedLoop(CancellationToken.None);
        }

        public static DispatcherPriorityAwaitable Yield(DispatcherPriority priority = DispatcherPriority.Background) => new();

        private static Task<T> Schedule<T>(Func<T> func)
        {
            var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            UiPlatform.UiThread.Post(() =>
            {
                try
                {
                    tcs.SetResult(func());
                }
                catch (Exception e)
                {
                    tcs.SetException(e);
                }
            });
            return tcs.Task;
        }

        // ---------- Invoke（同步） ----------

        public void Invoke(Action callback) => Invoke(callback, DispatcherPriority.Send);

        public void Invoke(Action callback, DispatcherPriority priority) => Invoke(() =>
        {
            callback();
            return true;
        }, priority);

        public void Invoke(Action callback, DispatcherPriority priority, CancellationToken cancellationToken) => Invoke(callback, priority);

        public void Invoke(Action callback, DispatcherPriority priority, CancellationToken cancellationToken, TimeSpan timeout) => Invoke(callback, priority);

        public TResult Invoke<TResult>(Func<TResult> callback) => Invoke(callback, DispatcherPriority.Send);

        public TResult Invoke<TResult>(Func<TResult> callback, DispatcherPriority priority)
        {
            if (CheckAccess())
            {
                return callback();
            }

            return Schedule(callback).GetAwaiter().GetResult();
        }

        public TResult Invoke<TResult>(Func<TResult> callback, DispatcherPriority priority, CancellationToken cancellationToken) => Invoke(callback, priority);

        public TResult Invoke<TResult>(Func<TResult> callback, DispatcherPriority priority, CancellationToken cancellationToken, TimeSpan timeout)
        {
            if (CheckAccess())
            {
                return callback();
            }

            var task = Schedule(callback);
            if (!task.Wait(timeout, cancellationToken))
            {
                throw new TimeoutException("等待 UI 线程超时");
            }

            return task.GetAwaiter().GetResult();
        }

        public object? Invoke(Delegate method, params object?[]? args) => Invoke(() => method.DynamicInvoke(args), DispatcherPriority.Send);

        public object? Invoke(DispatcherPriority priority, Delegate method) => Invoke(() => method.DynamicInvoke(), priority);

        public object? Invoke(DispatcherPriority priority, Delegate method, object? arg) => Invoke(() => method.DynamicInvoke(arg), priority);

        // ---------- InvokeAsync ----------

        public DispatcherOperation InvokeAsync(Action callback) => InvokeAsync(callback, DispatcherPriority.Normal);

        public DispatcherOperation InvokeAsync(Action callback, DispatcherPriority priority) =>
            new(Schedule(() =>
            {
                callback();
                return true;
            }));

        public DispatcherOperation InvokeAsync(Action callback, DispatcherPriority priority, CancellationToken cancellationToken) => InvokeAsync(callback, priority);

        public DispatcherOperation<TResult> InvokeAsync<TResult>(Func<TResult> callback) => new(Schedule(callback));

        public DispatcherOperation<TResult> InvokeAsync<TResult>(Func<TResult> callback, DispatcherPriority priority) => new(Schedule(callback));

        public DispatcherOperation<TResult> InvokeAsync<TResult>(Func<TResult> callback, DispatcherPriority priority, CancellationToken cancellationToken) => new(Schedule(callback));

        // ---------- BeginInvoke ----------

        public DispatcherOperation BeginInvoke(Delegate method, params object?[]? args) =>
            new(Schedule(() => method.DynamicInvoke(args)));

        public DispatcherOperation BeginInvoke(DispatcherPriority priority, Delegate method) =>
            new(Schedule(() => method.DynamicInvoke()));

        public DispatcherOperation BeginInvoke(DispatcherPriority priority, Delegate method, object? arg) =>
            new(Schedule(() => method.DynamicInvoke(arg)));

        public DispatcherOperation BeginInvoke(Delegate method, DispatcherPriority priority, params object?[]? args) =>
            new(Schedule(() => method.DynamicInvoke(args)));

        public void InvokeShutdown()
        {
            HasShutdownStarted = true;
        }

        public void BeginInvokeShutdown(DispatcherPriority priority)
        {
            HasShutdownStarted = true;
        }

        public event EventHandler? ShutdownStarted;

        public event DispatcherUnhandledExceptionEventHandler? UnhandledException;

        internal void RaiseShutdownStarted() => ShutdownStarted?.Invoke(this, EventArgs.Empty);
    }

    public delegate void DispatcherUnhandledExceptionEventHandler(object sender, DispatcherUnhandledExceptionEventArgs e);

    public class DispatcherUnhandledExceptionEventArgs : EventArgs
    {
        public DispatcherUnhandledExceptionEventArgs(Exception exception)
        {
            Exception = exception;
        }

        public Exception Exception { get; }

        public bool Handled { get; set; }
    }

    public readonly struct DispatcherPriorityAwaitable
    {
        public DispatcherPriorityAwaiter GetAwaiter() => new();
    }

    public readonly struct DispatcherPriorityAwaiter : INotifyCompletion
    {
        public bool IsCompleted => false;

        public void GetResult()
        {
        }

        public void OnCompleted(Action continuation) => UiPlatform.UiThread.Post(continuation);
    }

    /// <summary>
    /// WPF DispatcherTimer 的替代实现：计时在线程池，Tick 在 UI 线程触发
    /// </summary>
    public class DispatcherTimer
    {
        private Timer? _timer;
        private TimeSpan _interval;

        public DispatcherTimer() : this(DispatcherPriority.Background)
        {
        }

        public DispatcherTimer(DispatcherPriority priority)
        {
        }

        public DispatcherTimer(DispatcherPriority priority, Dispatcher dispatcher)
        {
        }

        public DispatcherTimer(TimeSpan interval, DispatcherPriority priority, EventHandler callback, Dispatcher dispatcher)
        {
            _interval = interval;
            Tick += callback;
            Start();
        }

        public Dispatcher Dispatcher => Dispatcher.Instance;

        public event EventHandler? Tick;

        public object? Tag { get; set; }

        public TimeSpan Interval
        {
            get => _interval;
            set
            {
                _interval = value;
                if (IsEnabled)
                {
                    _timer?.Change(value, value);
                }
            }
        }

        public bool IsEnabled
        {
            get => _timer != null;
            set
            {
                if (value)
                {
                    Start();
                }
                else
                {
                    Stop();
                }
            }
        }

        public void Start()
        {
            if (_timer != null)
            {
                return;
            }

            var period = _interval <= TimeSpan.Zero ? TimeSpan.FromMilliseconds(1) : _interval;
            _timer = new Timer(_ => UiPlatform.UiThread.Post(() =>
            {
                if (_timer != null)
                {
                    Tick?.Invoke(this, EventArgs.Empty);
                }
            }), null, period, period);
        }

        public void Stop()
        {
            _timer?.Dispose();
            _timer = null;
        }
    }
}
