using Avalonia.Controls;

namespace BetterGenshinImpact.MacApp.Views.Pages;

public partial class MacHomePage : UserControl
{
    public MacHomePage()
    {
        var viewModel = new MacHomeViewModel();
        DataContext = viewModel;
        InitializeComponent();
        Loaded += (_, _) => viewModel.Home.LoadedCommand.Execute(null);
    }
}
