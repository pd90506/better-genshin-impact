using System;
using System.IO;
using BetterGenshinImpact.Core.Config;

namespace BetterGenshinImpact.Platform;

/// <summary>
/// 程序目录布局。
/// <para>
/// 原版把只读资源（Assets、GameTask/**/Assets）和用户数据（User、log）都放在程序目录下，统一用 Global.Absolute 访问。
/// macOS 的 .app 包是只读的，这里把数据目录放到 ~/Library/Application Support/BetterGI，
/// 再把程序目录中的资源文件夹以符号链接的形式挂到数据目录下，原版的相对路径因此保持不变。
/// </para>
/// </summary>
public static class MacAppEnvironment
{
    /// <summary>
    /// 不链接到数据目录的文件夹：它们本身就是用户数据
    /// </summary>
    private static readonly string[] WritableFolders = ["User", "log", "Repos", "Temp", "Cache"];

    public static string ResourceDirectory => AppContext.BaseDirectory;

    public static string DataDirectory { get; private set; } = AppContext.BaseDirectory;

    /// <summary>
    /// 当前运行的 .app 包路径（不是从 .app 启动时为 null）
    /// </summary>
    public static string? AppBundlePath
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                if (dir.Name.EndsWith(".app", StringComparison.OrdinalIgnoreCase))
                {
                    return dir.FullName;
                }

                dir = dir.Parent;
            }

            return null;
        }
    }

    public static void Prepare()
    {
        var overrideDir = Environment.GetEnvironmentVariable("BGI_DATA_DIR");
        if (!string.IsNullOrWhiteSpace(overrideDir))
        {
            DataDirectory = Path.GetFullPath(overrideDir);
        }
        else if (OperatingSystem.IsMacOS() && AppBundlePath != null)
        {
            DataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support",
                "BetterGI");
        }
        else
        {
            DataDirectory = AppContext.BaseDirectory;
        }

        Directory.CreateDirectory(DataDirectory);
        if (!SamePath(DataDirectory, ResourceDirectory))
        {
            LinkResources();
            SeedUserFolder();
        }

        Global.StartUpPath = DataDirectory;
        Directory.SetCurrentDirectory(DataDirectory);
    }

    private static void LinkResources()
    {
        foreach (var source in Directory.GetDirectories(ResourceDirectory))
        {
            var name = Path.GetFileName(source);
            if (Array.Exists(WritableFolders, f => f.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var target = Path.Combine(DataDirectory, name);
            var info = new DirectoryInfo(target);
            if (info.LinkTarget != null)
            {
                if (SamePath(info.LinkTarget, source))
                {
                    continue;
                }

                // 程序被移动或更新后重新指向
                info.Delete();
            }
            else if (info.Exists)
            {
                continue;
            }

            Directory.CreateSymbolicLink(target, source);
        }
    }

    /// <summary>
    /// 首次运行时复制程序自带的默认用户文件（不覆盖已有文件）
    /// </summary>
    private static void SeedUserFolder()
    {
        var source = Path.Combine(ResourceDirectory, "User");
        if (!Directory.Exists(source))
        {
            return;
        }

        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(DataDirectory, "User", Path.GetRelativePath(source, file));
            if (File.Exists(target))
            {
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
    }

    private static bool SamePath(string a, string b) =>
        string.Equals(Path.GetFullPath(a).TrimEnd(Path.DirectorySeparatorChar), Path.GetFullPath(b).TrimEnd(Path.DirectorySeparatorChar),
            StringComparison.Ordinal);
}
