using System.Windows.Controls;
using PixelAutomation.Tool.Overlay.WPF.ViewModels;

namespace PixelAutomation.Tool.Overlay.WPF.Controls;

public partial class PartyHealControl : UserControl
{
    public PartyHealControl()
    {
        InitializeComponent();
    }

    public PartyHealControl(PartyHealViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }
}