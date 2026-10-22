using System;
using System.Threading;
using Avalonia.Threading;
using BetterGenshinImpact.Platform;

namespace BetterGenshinImpact.MacApp.Infrastructure;

/// <summary>
/// 原版代码通过 Application.Current.Dispatcher 切换到 UI 线程，这里接到 Avalonia 的 UI 线程
/// </summary>
public sealed class AvaloniaUiThread : IUiThread
{
    public bool CheckAccess() => Dispatcher.UIThread.CheckAccess();

    public void Post(Action action) => Dispatcher.UIThread.Post(action);

    public void RunNestedLoop(CancellationToken ct) => Dispatcher.UIThread.MainLoop(ct);
}
