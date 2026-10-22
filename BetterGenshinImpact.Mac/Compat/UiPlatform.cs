using System;
using System.Threading;
using System.Threading.Tasks;

namespace BetterGenshinImpact.Platform;

/// <summary>
/// UI 线程抽象。原版核心代码通过 WPF 的 Application.Current.Dispatcher 切换到 UI 线程，
/// macOS 版由 Avalonia 宿主在启动时注入实现；没有界面（命令行、单元测试）时使用默认实现。
/// </summary>
public interface IUiThread
{
    bool CheckAccess();

    void Post(Action action);

    /// <summary>
    /// 在 UI 线程上运行一个嵌套消息循环，直到 <paramref name="ct"/> 取消。用于同步弹窗
    /// </summary>
    void RunNestedLoop(CancellationToken ct);
}

/// <summary>
/// 没有界面时的默认实现：单独起一个专用线程充当 UI 线程
/// </summary>
public sealed class DedicatedUiThread : IUiThread
{
    private readonly System.Collections.Concurrent.BlockingCollection<Action> _queue = new();
    private readonly Thread _thread;

    public DedicatedUiThread()
    {
        _thread = new Thread(Loop) { IsBackground = true, Name = "BetterGI UI" };
        _thread.Start();
    }

    private void Loop()
    {
        foreach (var action in _queue.GetConsumingEnumerable())
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e);
            }
        }
    }

    public bool CheckAccess() => Thread.CurrentThread == _thread;

    public void Post(Action action) => _queue.Add(action);

    public void RunNestedLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            if (_queue.TryTake(out var action, 20))
            {
                action();
            }
        }
    }
}

/// <summary>
/// 弹窗抽象，由界面宿主实现。所有方法都在 UI 线程调用
/// </summary>
public interface IDialogHost
{
    Task<System.Windows.MessageBoxResult> ShowMessageAsync(string content, string title, System.Windows.MessageBoxButton button,
        Wpf.Ui.Violeta.Controls.MessageBoxIcon icon, System.Windows.MessageBoxResult defaultResult);

    Task<string?> PromptAsync(string question, string title, string defaultValue);

    Task<string?> PickFileAsync(string title, string? filter, bool save, string? defaultFileName);

    Task<string?> PickFolderAsync(string title);

    /// <summary>
    /// 显示带自定义内容的对话框（PromptDialog）。content 是 System.Windows.Controls 控件模型，返回 null 表示取消
    /// </summary>
    Task<bool> ShowContentAsync(string question, string title, System.Windows.UIElement? content, System.Windows.Size size,
        View.Windows.PromptDialogConfig? config);

    Task<Wpf.Ui.Controls.MessageBoxResult> ShowContentDialogAsync(Wpf.Ui.Controls.MessageBox messageBox);

    /// <summary>
    /// 打开某个原版窗口对应的 macOS 界面；返回 false 表示 macOS 版没有对应界面
    /// </summary>
    bool OpenFeatureWindow(System.Windows.Window window, bool modal);

    void OpenUrl(string url);
}

/// <summary>
/// 没有界面时的弹窗实现：只写日志，返回默认值
/// </summary>
public sealed class ConsoleDialogHost : IDialogHost
{
    public Task<System.Windows.MessageBoxResult> ShowMessageAsync(string content, string title, System.Windows.MessageBoxButton button,
        Wpf.Ui.Violeta.Controls.MessageBoxIcon icon, System.Windows.MessageBoxResult defaultResult)
    {
        Console.WriteLine($"[{title}] {content}");
        var result = defaultResult != System.Windows.MessageBoxResult.None
            ? defaultResult
            : button switch
            {
                System.Windows.MessageBoxButton.OK => System.Windows.MessageBoxResult.OK,
                System.Windows.MessageBoxButton.OKCancel => System.Windows.MessageBoxResult.OK,
                _ => System.Windows.MessageBoxResult.No,
            };
        return Task.FromResult(result);
    }

    public Task<string?> PromptAsync(string question, string title, string defaultValue) => Task.FromResult<string?>(defaultValue);

    public Task<string?> PickFileAsync(string title, string? filter, bool save, string? defaultFileName) => Task.FromResult<string?>(null);

    public Task<string?> PickFolderAsync(string title) => Task.FromResult<string?>(null);

    public Task<bool> ShowContentAsync(string question, string title, System.Windows.UIElement? content, System.Windows.Size size,
        View.Windows.PromptDialogConfig? config) => Task.FromResult(false);

    public Task<Wpf.Ui.Controls.MessageBoxResult> ShowContentDialogAsync(Wpf.Ui.Controls.MessageBox messageBox)
    {
        Console.WriteLine($"[{messageBox.Title}] {messageBox.Content}");
        return Task.FromResult(Wpf.Ui.Controls.MessageBoxResult.None);
    }

    public bool OpenFeatureWindow(System.Windows.Window window, bool modal) => false;

    public void OpenUrl(string url) => Console.WriteLine(url);
}

public static class UiPlatform
{
    private static IUiThread? _uiThread;

    public static IUiThread UiThread
    {
        get => _uiThread ??= new DedicatedUiThread();
        set => _uiThread = value;
    }

    public static IDialogHost Dialogs { get; set; } = new ConsoleDialogHost();

    /// <summary>
    /// 在 UI 线程上同步等待一个异步操作。若当前就在 UI 线程，用嵌套循环等待，避免死锁
    /// </summary>
    public static T RunSync<T>(Func<Task<T>> func)
    {
        if (UiThread.CheckAccess())
        {
            var task = func();
            if (!task.IsCompleted)
            {
                using var cts = new CancellationTokenSource();
                task.ContinueWith(_ => cts.Cancel(), TaskScheduler.Default);
                UiThread.RunNestedLoop(cts.Token);
            }

            return task.GetAwaiter().GetResult();
        }

        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        UiThread.Post(async () =>
        {
            try
            {
                tcs.SetResult(await func());
            }
            catch (Exception e)
            {
                tcs.SetException(e);
            }
        });
        return tcs.Task.GetAwaiter().GetResult();
    }

    public static Task<T> RunOnUiAsync<T>(Func<Task<T>> func)
    {
        if (UiThread.CheckAccess())
        {
            return func();
        }

        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        UiThread.Post(async () =>
        {
            try
            {
                tcs.SetResult(await func());
            }
            catch (Exception e)
            {
                tcs.SetException(e);
            }
        });
        return tcs.Task;
    }
}
