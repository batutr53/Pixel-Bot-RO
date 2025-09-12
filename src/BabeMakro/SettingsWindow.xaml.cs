using System.Windows;
using PixelAutomation.Tool.Overlay.WPF.ViewModels;

namespace PixelAutomation.Tool.Overlay.WPF;

public partial class SettingsWindow : Window
{
    public SettingsWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}