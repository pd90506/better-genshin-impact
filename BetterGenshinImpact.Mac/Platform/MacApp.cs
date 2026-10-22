using System;
using System.Globalization;
using System.IO;
using BetterGenshinImpact.Core.Config;
using BetterGenshinImpact.Core.Mask;
using BetterGenshinImpact.Core.Monitor;
using BetterGenshinImpact.Core.Recognition.OCR;
using BetterGenshinImpact.Core.Recognition.ONNX;
using BetterGenshinImpact.GameTask;
using BetterGenshinImpact.GameTask.AutoSkip.Audio;
using BetterGenshinImpact.GameTask.Music.Service;
using BetterGenshinImpact.GameTask.Runtime;
using BetterGenshinImpact.GameTask.Runtime.Win32;
using BetterGenshinImpact.Helpers;
using BetterGenshinImpact.Platform;
using BetterGenshinImpact.Platform.Mac;
using BetterGenshinImpact.Service;
using BetterGenshinImpact.Service.I18n;
using BetterGenshinImpact.Service.Instance;
using BetterGenshinImpact.Service.Interface;
using BetterGenshinImpact.Service.Notification;
using BetterGenshinImpact.Service.Notifier;
using BetterGenshinImpact.Service.Tavern;
using BetterGenshinImpact.ViewModel;
using BetterGenshinImpact.ViewModel.Pages;
using BetterGenshinImpact.ViewModel.Pages.View;
using BetterGenshinImpact.ViewModel.Windows;
using LazyCache;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Wpf.Ui;

namespace BetterGenshinImpact;

/// <summary>
/// 原版 App（App.xaml.cs）的 macOS 版：只负责依赖注入与日志，界面由 Avalonia 宿主创建。
/// 服务注册与原版保持一致，去掉了 Windows 专属的服务（子会话、网页版、托盘、WebView2 等）。
/// </summary>
public partial class App : System.Windows.Application
{
    private static IHost? _host;
    private static readonly object InitLock = new();
    private static ILoggerFactory? _fallbackLoggerFactory;

    /// <summary>
    /// 界面层可以订阅的日志事件（Information 及以上）
    /// </summary>
    public static MacLogSink LogSink { get; } = new();

    public static IServiceProvider ServiceProvider => EnsureHost().Services;

    public static bool IsInitialized => _host != null;

    /// <summary>
    /// 创建依赖注入容器。<paramref name="configure"/> 供宿主追加自己的服务（主窗口、对话框等）
    /// </summary>
    public static IHost Initialize(Action<IServiceCollection>? configure = null)
    {
        lock (InitLock)
        {
            if (_host != null)
            {
                return _host;
            }

            MacAppEnvironment.Prepare();
            MacEventTap.Install();
            InstanceBootstrap.Initialize();

            _host = Host.CreateDefaultBuilder()
                .ConfigureLogging(builder => builder.ClearProviders())
                .ConfigureServices((_, services) =>
                {
                    RegisterServices(services);
                    configure?.Invoke(services);
                })
                .Build();
            return _host;
        }
    }

    private static IHost EnsureHost() => _host ?? Initialize();

    private static void RegisterServices(IServiceCollection services)
    {
        var configService = new ConfigService();
        services.AddSingleton<IConfigService>(_ => configService);
        var all = configService.Get();

        var logFolder = Global.Absolute("log");
        Directory.CreateDirectory(logFolder);
        Log.Logger = new LoggerConfiguration()
            .WriteTo.File(Path.Combine(logFolder, "better-genshin-impact.log"),
                outputTemplate: "[{Timestamp:HH:mm:ss.fff}] [{Level:u3}] {SourceContext}{NewLine}{Message}{NewLine}{Exception}{NewLine}",
                rollingInterval: RollingInterval.Day,
                shared: true,
                retainedFileCountLimit: 31)
            .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.Sink(LogSink, LogEventLevel.Information)
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Warning)
            .CreateLogger();
        services.AddLogging(c => c.AddSerilog());

        // macOS 版只使用 ONNX Runtime 自带的 CPU 推理
        all.HardwareAccelerationConfig ??= new();
        var inferenceStartupStatus = InferenceRuntimeService.PrepareStartup(all.HardwareAccelerationConfig, true);
        configService.Save();
        services.AddSingleton(inferenceStartupStatus);
        var onnxRuntimeLoader = new OnnxRuntimeLoader(all.HardwareAccelerationConfig, AppContext.BaseDirectory);
        services.AddSingleton(onnxRuntimeLoader);
        services.AddSingleton<IOnnxRuntimeSelection>(onnxRuntimeLoader);
        var onnxRuntimeInstaller = new OnnxRuntimeInstaller(AppContext.BaseDirectory);
        services.AddSingleton(onnxRuntimeInstaller);
        services.AddSingleton<IOnnxRuntimeInstaller>(onnxRuntimeInstaller);
        services.AddSingleton<IInferenceDeviceDiscovery, InferenceDeviceDiscovery>();
        services.AddSingleton<IInferenceBenchmarkLeaseProvider>(sp => new InferenceBenchmarkCoordinator(
            GameTask.Common.TaskControl.TaskSemaphore,
            () => sp.GetRequiredService<TaskTriggerDispatcher>().PauseForInference()));
        services.AddSingleton<InferenceModelDiagnostics>();
        services.AddSingleton<IInferenceModelDiagnostics>(sp => sp.GetRequiredService<InferenceModelDiagnostics>());
        services.AddSingleton<InferenceRuntimeService>();
        services.AddSingleton<HardwareAccelerationViewModelFactory>();
        services.AddSingleton<IInferenceSettingsInteraction, InferenceSettingsInteraction>();

        services.AddLocalization();
        var i18nService = I18nService.Instance;
        i18nService.ChangeLanguage(all.OtherConfig.UiCultureInfoName);
        services.AddSingleton(i18nService);

        services.AddSingleton(InstanceBootstrap.Current);
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<ISnackbarService, SnackbarService>();
        services.AddSingleton<IUpdateService, UpdateService>();
        services.AddSingleton<Service.ChildSession.ChildSessionService>();

        // 页面 ViewModel：与原版相同，界面由 Avalonia 绑定
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<HomePageViewModel>();
        services.AddSingleton<ScriptControlViewModel>();
        services.AddSingleton<TriggerSettingsPageViewModel>();
        services.AddSingleton<MacroSettingsPageViewModel>();
        services.AddSingleton<CommonSettingsPageViewModel>();
        services.AddSingleton<TaskSettingsPageViewModel>();
        services.AddSingleton<HotKeyPageViewModel>();
        services.AddSingleton<NotificationSettingsPageViewModel>();
        services.AddSingleton<KeyMouseRecordPageViewModel>();
        services.AddSingleton<JsListViewModel>();
        services.AddSingleton<MapPathingViewModel>();
        services.AddSingleton<OneDragonFlowViewModel>();
        services.AddSingleton<MusicPageViewModel>();
        services.AddSingleton<KeyBindingsSettingsPageViewModel>();
        services.AddSingleton<PathingConfigViewModel>();
        services.AddSingleton<IBannerImageService, BannerImageService>();
        services.AddTransient<WebImageInputViewModel>();

        services.AddSingleton<DirectInputMonitor>();
        services.AddSingleton<RawInputMonitor>();
        services.AddSingleton<IRelativeMouseInputMonitorFactory, RelativeMouseInputMonitorFactory>();
        services.AddSingleton<OverlayMetricsService>();
        services.AddSingleton<CustomHtmlMaskService>();

        // 遮罩：macOS 版暂不在游戏上叠加绘制，绘制数据仍然保留（界面可以选择性显示）
        services.AddSingleton<MaskWindowDrawingBoard>();
        services.AddSingleton<IMaskWindowDrawingBoard>(sp => sp.GetRequiredService<MaskWindowDrawingBoard>());
        services.AddSingleton<IMaskWindowSnapshotSource<MaskWindowDrawingSnapshot>>(sp => sp.GetRequiredService<MaskWindowDrawingBoard>());
        services.AddSingleton<MaskWindowMapState>();
        services.AddSingleton<IMaskWindowMapState>(sp => sp.GetRequiredService<MaskWindowMapState>());
        services.AddSingleton<IMaskWindowSnapshotSource<MaskWindowMapSnapshot>>(sp => sp.GetRequiredService<MaskWindowMapState>());
        services.AddSingleton<MaskWindowViewModel>();
        services.AddSingleton<IMaskWindowHost, MacMaskWindowHost>();
        services.AddSingleton<DialogueOptionVoiceDiagnosticState>();
        services.AddSingleton<DialogueOptionVoiceDiagnosticService>();
        services.AddSingleton<TaskTriggerDispatcher>();

        // 游戏运行环境：macOS 窗口（GeForce NOW）
        services.AddSingleton<Win32RuntimeProvider>();
        services.AddSingleton<IGameRuntimeProvider>(sp => sp.GetRequiredService<Win32RuntimeProvider>());
        services.AddSingleton<GameRuntimeService>();

        services.AddSingleton<RecognitionTemplateAssetService>();
        services.AddSingleton<RecognitionTemplateEditorService>();
        services.AddSingleton<NotificationService>();
        services.AddHostedService(sp => sp.GetRequiredService<NotificationService>());
        services.AddSingleton<NotifierManager>();
        services.AddSingleton<IScriptService, ScriptService>();
        services.AddSingleton<IMusicScoreParser, MusicScoreParser>();
        services.AddSingleton<IMusicStateStore, MusicStateStore>();
        services.AddSingleton<IInstrumentProfileService, InstrumentProfileService>();
        services.AddSingleton<IMusicTimelineBuilder, MusicTimelineBuilder>();
        services.AddSingleton<IMusicLibraryService, MusicLibraryService>();
        services.AddSingleton<IMusicCoverService, MusicCoverService>();
        services.AddSingleton<IMusicInstrumentSwitcher, MusicInstrumentSwitcher>();
        services.AddSingleton<IKeyInputTransport, SendInputKeyInputTransport>();
        services.AddSingleton<IMusicPlaybackService, MusicPlaybackService>();
        services.AddSingleton<BgiOnnxFactory>();
        services.AddSingleton<OcrFactory>();
        services.AddMemoryCache();
        services.AddSingleton<IAppCache, CachingService>();
        services.AddSingleton<MemoryFileCache>();
        services.AddSingleton<IMihoyoMapApiService, MihoyoMapApiService>();
        services.AddSingleton<IKongyingTavernApiService, KongyingTavernApiService>();
        services.AddSingleton<IHoYoLabMapApiService, HoYoLabMapApiService>();
        services.AddSingleton<IMaskMapPointService, MaskMapPointService>();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IServerTimeProvider, ServerTimeProvider>();

    }

    public static ILogger<T> GetLogger<T>()
    {
        if (_host != null)
        {
            return _host.Services.GetService<ILogger<T>>()!;
        }

        // 静态字段初始化可能早于容器创建，此时先输出到控制台
        _fallbackLoggerFactory ??= LoggerFactory.Create(b => b.AddSerilog(new LoggerConfiguration().WriteTo.Console().CreateLogger()));
        return _fallbackLoggerFactory.CreateLogger<T>();
    }

    public static T? GetService<T>() where T : class => EnsureHost().Services.GetService(typeof(T)) as T;

    public static object? GetService(Type type) => EnsureHost().Services.GetService(type);
}
