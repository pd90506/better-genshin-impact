using System.Runtime.CompilerServices;

namespace BetterGenshinImpact.UnitTest;

/// <summary>
/// 原版测试用 @"..\..\..\Assets\xxx.png" 这样的 Windows 相对路径读取截图。
/// macOS / Linux 上反斜杠是普通字符，这里在测试输出目录中为每个测试素材创建同名（含反斜杠）的符号链接，
/// 让原版测试代码不经修改即可运行。只影响测试环境。
/// </summary>
internal static class WindowsPathShim
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var baseDir = AppContext.BaseDirectory;
        var assets = Path.Combine(baseDir, "Assets");
        if (!Directory.Exists(assets))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(assets, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(assets, file).Replace('/', '\\');
            foreach (var prefix in new[] { @"..\..\..\Assets\", @"Assets\" })
            {
                var link = Path.Combine(baseDir, prefix + rel);
                if (!File.Exists(link))
                {
                    try
                    {
                        File.CreateSymbolicLink(link, file);
                    }
                    catch (IOException)
                    {
                        // 并行测试时可能已被创建
                    }
                }
            }
        }

        Directory.SetCurrentDirectory(baseDir);
    }
}
