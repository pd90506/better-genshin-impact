using System.Threading.Tasks;
using BetterGenshinImpact.Core.Script;
using BetterGenshinImpact.GameTask;
using BetterGenshinImpact.Helpers;
using BetterGenshinImpact.Service.Instance;
using BetterGenshinImpact.ViewModel.Pages;
using Wpf.Ui;

namespace BetterGenshinImpact.MacApp.Infrastructure;

/// <summary>
/// 命令行启动参数（与原版 ApplicationHostService.HandleActivationAsync 相同），例如：
/// open -a BetterGI --args startOneDragon
/// </summary>
public static class CommandLineActivation
{
    public static void Handle()
    {
        var options = CommandLineOptions.Instance;
        if (!options.HasTaskArgs)
        {
            return;
        }

        var scriptConfig = TaskContext.Instance().Config.ScriptConfig;
        if (InstanceBootstrap.Current.Context.IsRoot && scriptConfig.AutoUpdateBeforeCommandLineRun)
        {
            ScriptRepoUpdater.Instance.CommandLineAutoUpdateTask = Task.Run(() => ScriptRepoUpdater.Instance.AutoUpdateSubscribedScripts());
        }

        switch (options.Action)
        {
            case CommandLineAction.StartOneDragon:
                // 后续逻辑在 OneDragonFlowViewModel.OnLoaded 中
                NavigationService.NavigateHandler?.Invoke(typeof(View.Pages.OneDragonFlowPage));
                break;
            case CommandLineAction.StartGroups:
                NavigationService.NavigateHandler?.Invoke(typeof(View.Pages.ScriptControlPage));
                if (options.GroupNames.Length > 0)
                {
                    App.GetService<ScriptControlViewModel>()?.OnStartMultiScriptGroupWithNamesAsync(options.GroupNames);
                }

                break;
            case CommandLineAction.TaskProgress:
                NavigationService.NavigateHandler?.Invoke(typeof(View.Pages.ScriptControlPage));
                if (options.GroupNames.Length > 0)
                {
                    App.GetService<ScriptControlViewModel>()?.OnStartMultiScriptTaskProgressAsync(options.GroupNames);
                }

                break;
            case CommandLineAction.Start:
                // 后续逻辑在 HomePageViewModel.OnLoaded 中
                NavigationService.NavigateHandler?.Invoke(typeof(View.Pages.HomePage));
                break;
        }
    }
}
