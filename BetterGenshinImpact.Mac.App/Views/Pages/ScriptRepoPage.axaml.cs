using Avalonia.Controls;

namespace BetterGenshinImpact.MacApp.Views.Pages;

public partial class ScriptRepoPage : UserControl
{
    public ScriptRepoPage()
    {
        DataContext = new ScriptRepoViewModel();
        InitializeComponent();
    }
}
