using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BetterGenshinImpact.Core.Config;
using BetterGenshinImpact.Core.Script;
using BetterGenshinImpact.GameTask;
using BetterGenshinImpact.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BetterGenshinImpact.View.Windows;
using Wpf.Ui.Violeta.Controls;

namespace BetterGenshinImpact.MacApp.Views.Pages;

/// <summary>
/// 仓库中的一个文件或文件夹，子节点在展开时才读取
/// </summary>
public partial class RepoNode : ObservableObject
{
    private static readonly RepoNode Placeholder = new("…", string.Empty, string.Empty, false);
    private bool _loaded;

    public RepoNode(string name, string fullPath, string relativePath, bool isDirectory)
    {
        Name = name;
        FullPath = fullPath;
        RelativePath = relativePath;
        IsDirectory = isDirectory;
        if (isDirectory)
        {
            Children.Add(Placeholder);
        }
    }

    public string Name { get; }

    public string FullPath { get; }

    /// <summary>
    /// 相对仓库 repo 目录的路径（使用 / 分隔），即订阅链接中的路径
    /// </summary>
    public string RelativePath { get; }

    public bool IsDirectory { get; }

    public string Icon => IsDirectory ? "📁" : Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? "🗺️" : "📄";

    public ObservableCollection<RepoNode> Children { get; } = [];

    [ObservableProperty]
    private bool _isExpanded;

    partial void OnIsExpandedChanged(bool value)
    {
        if (value && !_loaded)
        {
            _loaded = true;
            Children.Clear();
            foreach (var child in ReadChildren(FullPath, RelativePath))
            {
                Children.Add(child);
            }
        }
    }

    public static IEnumerable<RepoNode> ReadChildren(string dir, string relative)
    {
        if (!Directory.Exists(dir))
        {
            yield break;
        }

        foreach (var d in Directory.GetDirectories(dir).OrderBy(d => d, StringComparer.CurrentCulture))
        {
            var name = Path.GetFileName(d);
            if (!name.StartsWith('.'))
            {
                yield return new RepoNode(name, d, Combine(relative, name), true);
            }
        }

        foreach (var f in Directory.GetFiles(dir).OrderBy(f => f, StringComparer.CurrentCulture))
        {
            var name = Path.GetFileName(f);
            if (!name.StartsWith('.'))
            {
                yield return new RepoNode(name, f, Combine(relative, name), false);
            }
        }
    }

    private static string Combine(string a, string b) => string.IsNullOrEmpty(a) ? b : a + "/" + b;
}

/// <summary>
/// macOS 版脚本仓库：更新中央仓库、浏览并订阅脚本、导入订阅链接。
/// 原版使用 WebView2 打开本地仓库网页，macOS 版改为原生树形浏览
/// </summary>
public partial class ScriptRepoViewModel : ObservableObject
{
    private const string CustomChannel = "自定义";

    public ScriptRepoViewModel()
    {
        Config = TaskContext.Instance().Config.ScriptConfig;
        Channels = [.. ScriptRepoUpdater.RepoChannels.Keys, CustomChannel];
        _selectedChannel = Channels.Contains(Config.SelectedChannelName) ? Config.SelectedChannelName : Channels[0];
        LoadTree();
    }

    public ScriptConfig Config { get; }

    public List<string> Channels { get; }

    [ObservableProperty]
    private string _selectedChannel;

    [ObservableProperty]
    private bool _isUpdating;

    [ObservableProperty]
    private string _progressText = string.Empty;

    [ObservableProperty]
    private double _progressValue;

    [ObservableProperty]
    private string _importLink = string.Empty;

    [ObservableProperty]
    private string _repoStatus = string.Empty;

    [ObservableProperty]
    private RepoNode? _selectedNode;

    [ObservableProperty]
    private string _subscribedText = string.Empty;

    public ObservableCollection<RepoNode> Roots { get; } = [];

    public bool IsCustomChannel => SelectedChannel == CustomChannel;

    public string RepoUrl
    {
        get => IsCustomChannel ? Config.CustomRepoUrl : ScriptRepoUpdater.RepoChannels.GetValueOrDefault(SelectedChannel, string.Empty);
        set
        {
            if (IsCustomChannel)
            {
                Config.CustomRepoUrl = value;
                OnPropertyChanged();
            }
        }
    }

    partial void OnSelectedChannelChanged(string value)
    {
        Config.SelectedChannelName = value;
        OnPropertyChanged(nameof(IsCustomChannel));
        OnPropertyChanged(nameof(RepoUrl));
        LoadTree();
    }

    private void LoadTree()
    {
        Roots.Clear();
        string repoPath;
        try
        {
            repoPath = ScriptRepoUpdater.Instance.FindCenterRepoPath();
        }
        catch
        {
            RepoStatus = "本地还没有脚本仓库，请先点击「更新仓库」下载（首次约几百 MB，请耐心等待）";
            SubscribedText = string.Empty;
            return;
        }

        foreach (var key in ScriptRepoUpdater.PathMapper.Keys)
        {
            var dir = Path.Combine(repoPath, key);
            if (Directory.Exists(dir))
            {
                Roots.Add(new RepoNode(key switch
                {
                    "pathing" => "pathing（地图追踪 / 采集）",
                    "js" => "js（JS 脚本）",
                    "combat" => "combat（战斗策略）",
                    "tcg" => "tcg（七圣召唤）",
                    _ => key,
                }, dir, key, true));
            }
        }

        RepoStatus = $"本地仓库：{repoPath}";
        RefreshSubscribed();
    }

    private void RefreshSubscribed()
    {
        try
        {
            var paths = ScriptRepoUpdater.GetSubscribedPathsForCurrentRepo();
            SubscribedText = paths.Count == 0 ? "尚未订阅任何脚本" : $"已订阅 {paths.Count} 项：" + string.Join("、", paths.Take(30)) + (paths.Count > 30 ? " …" : string.Empty);
        }
        catch (Exception e)
        {
            SubscribedText = "读取订阅失败：" + e.Message;
        }
    }

    [RelayCommand]
    private async Task UpdateRepoAsync()
    {
        var url = RepoUrl;
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out _))
        {
            Toast.Warning("请输入有效的仓库地址");
            return;
        }

        IsUpdating = true;
        ProgressValue = 0;
        ProgressText = "正在更新脚本仓库，请耐心等待…";
        try
        {
            var (_, updated) = await Task.Run(() => ScriptRepoUpdater.Instance.UpdateCenterRepoByGit(url, (path, steps, total) =>
            {
                UiPlatform.UiThread.Post(() =>
                {
                    ProgressValue = total > 0 ? Math.Min(100, steps * 100.0 / total) : 0;
                    ProgressText = path ?? string.Empty;
                });
            }));
            Toast.Success(updated ? "脚本仓库更新成功，有新内容" : "脚本仓库已是最新");
            LoadTree();
        }
        catch (Exception e)
        {
            await ThemedMessageBox.ErrorAsync($"更新失败，可尝试重置仓库后重新更新。失败原因：{e.Message}");
        }
        finally
        {
            IsUpdating = false;
        }
    }

    [RelayCommand]
    private async Task ResetRepoAsync()
    {
        if (await ThemedMessageBox.QuestionAsync("确定要删除本地仓库并重新下载吗？") != System.Windows.MessageBoxResult.Yes)
        {
            return;
        }

        await ScriptRepoUpdater.Instance.ResetCurrentRepoAsync();
        LoadTree();
    }

    /// <summary>
    /// 订阅所选文件夹或文件：复制到 User 目录，之后可在「地图追踪」「JS 脚本」「调度器」中使用
    /// </summary>
    [RelayCommand]
    private async Task SubscribeSelectedAsync()
    {
        if (SelectedNode == null || string.IsNullOrEmpty(SelectedNode.RelativePath))
        {
            Toast.Warning("请先在左侧选择要订阅的文件夹或脚本");
            return;
        }

        await ScriptRepoUpdater.Instance.ImportScriptFromPathJson(Newtonsoft.Json.JsonConvert.SerializeObject(new[] { SelectedNode.RelativePath }));
        RefreshSubscribed();
    }

    [RelayCommand]
    private async Task ImportLinkAsync()
    {
        var link = ImportLink.Trim();
        if (!link.StartsWith("bettergi://", StringComparison.OrdinalIgnoreCase))
        {
            Toast.Warning("请粘贴以 bettergi://script?import= 开头的订阅链接");
            return;
        }

        await ScriptRepoUpdater.Instance.ImportScriptFromUri(link, false);
        ImportLink = string.Empty;
        RefreshSubscribed();
    }

    [RelayCommand]
    private async Task UpdateSubscribedAsync()
    {
        await ScriptRepoUpdater.Instance.ManualUpdateSubscribedScripts();
        RefreshSubscribed();
    }

    [RelayCommand]
    private async Task ImportZipAsync()
    {
        var file = await UiPlatform.Dialogs.PickFileAsync("选择仓库压缩包", "仓库压缩包|*.zip", false, null);
        if (file == null)
        {
            return;
        }

        IsUpdating = true;
        try
        {
            ProgressText = await ScriptRepoUpdater.Instance.ImportLocalRepoZip(file, (p, text) => UiPlatform.UiThread.Post(() =>
            {
                ProgressValue = p;
                ProgressText = text;
            }));
            LoadTree();
        }
        catch (Exception e)
        {
            await ThemedMessageBox.ErrorAsync("导入失败：" + e.Message);
        }
        finally
        {
            IsUpdating = false;
        }
    }

    [RelayCommand]
    private void OpenOnlineRepo() => UiPlatform.Dialogs.OpenUrl("https://www.bettergi.com/");

    [RelayCommand]
    private void OpenUserFolder() => UiPlatform.Dialogs.OpenUrl(Global.Absolute("User"));
}
