using System.Windows;
using System.Windows.Media;
using PixelAutomation.Tool.Overlay.WPF.Models;
using PixelAutomation.Tool.Overlay.WPF.Services;

namespace PixelAutomation.Tool.Overlay.WPF;

public partial class ClientConfigWindow : Window
{
    private readonly ClientViewModel _viewModel;

    public ClientConfigWindow(ClientViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        LoadViewModelData();
    }

    private void LoadViewModelData()
    {
        // HP Probe
        HpPosX.Text = _viewModel.HpProbe.X.ToString();
        HpPosY.Text = _viewModel.HpProbe.Y.ToString();
        UpdateHpExpectedColor(_viewModel.HpProbe.ExpectedColor);
        UpdateHpTriggerColor(_viewModel.HpProbe.TriggerColor);
        HpToleranceSlider.Value = _viewModel.HpProbe.Tolerance;
        HpActionX.Text = _viewModel.HpTrigger.X.ToString();
        HpActionY.Text = _viewModel.HpTrigger.Y.ToString();
        
        // MP Probe
        MpPosX.Text = _viewModel.MpProbe.X.ToString();
        MpPosY.Text = _viewModel.MpProbe.Y.ToString();
        UpdateMpExpectedColor(_viewModel.MpProbe.ExpectedColor);
        UpdateMpTriggerColor(_viewModel.MpProbe.TriggerColor);
        MpToleranceSlider.Value = _viewModel.MpProbe.Tolerance;
        MpActionX.Text = _viewModel.MpTrigger.X.ToString();
        MpActionY.Text = _viewModel.MpTrigger.Y.ToString();
        
        // Periodic Clicks
        YPeriodicEnabled.IsChecked = _viewModel.YClick.Enabled;
        YPeriodicX.Text = _viewModel.YClick.X.ToString();
        YPeriodicY.Text = _viewModel.YClick.Y.ToString();
        YPeriodicPeriod.Text = _viewModel.YClick.PeriodMs.ToString();
        
        Extra1PeriodicEnabled.IsChecked = _viewModel.Extra1Click.Enabled;
        Extra1PeriodicX.Text = _viewModel.Extra1Click.X.ToString();
        Extra1PeriodicY.Text = _viewModel.Extra1Click.Y.ToString();
        Extra1PeriodicPeriod.Text = _viewModel.Extra1Click.PeriodMs.ToString();
        
        Extra2PeriodicEnabled.IsChecked = _viewModel.Extra2Click.Enabled;
        Extra2PeriodicX.Text = _viewModel.Extra2Click.X.ToString();
        Extra2PeriodicY.Text = _viewModel.Extra2Click.Y.ToString();
        Extra2PeriodicPeriod.Text = _viewModel.Extra2Click.PeriodMs.ToString();
        
        Extra3PeriodicEnabled.IsChecked = _viewModel.Extra3Click.Enabled;
        Extra3PeriodicX.Text = _viewModel.Extra3Click.X.ToString();
        Extra3PeriodicY.Text = _viewModel.Extra3Click.Y.ToString();
        Extra3PeriodicPeriod.Text = _viewModel.Extra3Click.PeriodMs.ToString();
    }

    private void PickHpPosition_Click(object sender, RoutedEventArgs e)
    {
        PickCoordinate("Pick HP Probe Position", (x, y) =>
        {
            HpPosX.Text = x.ToString();
            HpPosY.Text = y.ToString();
            _viewModel.HpProbe.X = x;
            _viewModel.HpProbe.Y = y;
        });
    }

    private void PickMpPosition_Click(object sender, RoutedEventArgs e)
    {
        PickCoordinate("Pick MP Probe Position", (x, y) =>
        {
            MpPosX.Text = x.ToString();
            MpPosY.Text = y.ToString();
            _viewModel.MpProbe.X = x;
            _viewModel.MpProbe.Y = y;
        });
    }

    private void PickHpAction_Click(object sender, RoutedEventArgs e)
    {
        PickCoordinate("Pick HP Potion Click Position", (x, y) =>
        {
            HpActionX.Text = x.ToString();
            HpActionY.Text = y.ToString();
            _viewModel.HpTrigger.X = x;
            _viewModel.HpTrigger.Y = y;
        });
    }

    private void PickMpAction_Click(object sender, RoutedEventArgs e)
    {
        PickCoordinate("Pick MP Potion Click Position", (x, y) =>
        {
            MpActionX.Text = x.ToString();
            MpActionY.Text = y.ToString();
            _viewModel.MpTrigger.X = x;
            _viewModel.MpTrigger.Y = y;
        });
    }

    private void PickYPeriodic_Click(object sender, RoutedEventArgs e)
    {
        PickCoordinate("Pick Y Periodic Click Position", (x, y) =>
        {
            YPeriodicX.Text = x.ToString();
            YPeriodicY.Text = y.ToString();
            _viewModel.YClick.X = x;
            _viewModel.YClick.Y = y;
        });
    }

    private void PickExtra1Periodic_Click(object sender, RoutedEventArgs e)
    {
        PickCoordinate("Pick Extra1 Click Position", (x, y) =>
        {
            Extra1PeriodicX.Text = x.ToString();
            Extra1PeriodicY.Text = y.ToString();
            _viewModel.Extra1Click.X = x;
            _viewModel.Extra1Click.Y = y;
        });
    }

    private void PickExtra2Periodic_Click(object sender, RoutedEventArgs e)
    {
        PickCoordinate("Pick Extra2 Click Position", (x, y) =>
        {
            Extra2PeriodicX.Text = x.ToString();
            Extra2PeriodicY.Text = y.ToString();
            _viewModel.Extra2Click.X = x;
            _viewModel.Extra2Click.Y = y;
        });
    }

    private void PickExtra3Periodic_Click(object sender, RoutedEventArgs e)
    {
        PickCoordinate("Pick Extra3 Click Position", (x, y) =>
        {
            Extra3PeriodicX.Text = x.ToString();
            Extra3PeriodicY.Text = y.ToString();
            _viewModel.Extra3Click.X = x;
            _viewModel.Extra3Click.Y = y;
        });
    }

    private void SampleHpColor_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.TargetHwnd == IntPtr.Zero) return;
        
        var color = ColorSampler.GetAverageColorInArea(_viewModel.TargetHwnd, _viewModel.HpProbe.X, _viewModel.HpProbe.Y, 5);
        UpdateHpExpectedColor(color);
        _viewModel.HpProbe.ExpectedColor = color;
    }

    private void SampleMpColor_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.TargetHwnd == IntPtr.Zero) return;
        
        var color = ColorSampler.GetAverageColorInArea(_viewModel.TargetHwnd, _viewModel.MpProbe.X, _viewModel.MpProbe.Y, 5);
        UpdateMpExpectedColor(color);
        _viewModel.MpProbe.ExpectedColor = color;
    }

    private void UpdatePreview_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.TargetHwnd == IntPtr.Zero) return;
        
        var hpColor = ColorSampler.GetAverageColorInArea(_viewModel.TargetHwnd, _viewModel.HpProbe.X, _viewModel.HpProbe.Y, 5);
        var mpColor = ColorSampler.GetAverageColorInArea(_viewModel.TargetHwnd, _viewModel.MpProbe.X, _viewModel.MpProbe.Y, 5);
        
        HpCurrentColorText.Text = $"RGB({hpColor.R},{hpColor.G},{hpColor.B})";
        MpCurrentColorText.Text = $"RGB({mpColor.R},{mpColor.G},{mpColor.B})";
        
        var hpDistance = ColorSampler.CalculateColorDistance(hpColor, _viewModel.HpProbe.ExpectedColor);
        var mpDistance = ColorSampler.CalculateColorDistance(mpColor, _viewModel.MpProbe.ExpectedColor);
        
        HpDistanceText.Text = $"Distance: {hpDistance:F1}";
        MpDistanceText.Text = $"Distance: {mpDistance:F1}";
        
        _viewModel.HpProbe.CurrentColor = hpColor;
        _viewModel.MpProbe.CurrentColor = mpColor;
    }

    private void TestConfig_Click(object sender, RoutedEventArgs e)
    {
        UpdatePreview_Click(sender, e);
        
        var hpMatch = ColorSampler.IsColorMatch(_viewModel.HpProbe.CurrentColor ?? System.Drawing.Color.Black, 
            _viewModel.HpProbe.ExpectedColor, _viewModel.HpProbe.Tolerance);
        var mpMatch = ColorSampler.IsColorMatch(_viewModel.MpProbe.CurrentColor ?? System.Drawing.Color.Black,
            _viewModel.MpProbe.ExpectedColor, _viewModel.MpProbe.Tolerance);
        
        Title = $"HP: {(hpMatch ? "✅ YES" : "❌ NO")} | MP: {(mpMatch ? "✅ YES" : "❌ NO")}";
    }

    private void SaveConfig_Click(object sender, RoutedEventArgs e)
    {
        SaveViewModelData();
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void PickCoordinate(string title, Action<int, int> onPicked)
    {
        if (_viewModel.TargetHwnd == IntPtr.Zero)
        {
            Title = "❌ No target window selected!";
            return;
        }

        var picker = new CoordinatePicker(_viewModel.TargetHwnd, title);
        picker.CoordinatePicked += onPicked;
        picker.ShowDialog();
    }

    private void UpdateHpExpectedColor(System.Drawing.Color color)
    {
        HpExpectedColorDisplay.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(color.R, color.G, color.B));
        HpExpectedColorText.Text = $"{color.R},{color.G},{color.B}";
    }

    private void UpdateHpTriggerColor(System.Drawing.Color color)
    {
        HpTriggerColorDisplay.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(color.R, color.G, color.B));
        HpTriggerColorText.Text = $"{color.R},{color.G},{color.B}";
    }

    private void UpdateMpExpectedColor(System.Drawing.Color color)
    {
        MpExpectedColorDisplay.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(color.R, color.G, color.B));
        MpExpectedColorText.Text = $"{color.R},{color.G},{color.B}";
    }

    private void UpdateMpTriggerColor(System.Drawing.Color color)
    {
        MpTriggerColorDisplay.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(color.R, color.G, color.B));
        MpTriggerColorText.Text = $"{color.R},{color.G},{color.B}";
    }

    private void SaveViewModelData()
    {
        // Save all form data back to ViewModel
        if (int.TryParse(HpPosX.Text, out var hpX)) _viewModel.HpProbe.X = hpX;
        if (int.TryParse(HpPosY.Text, out var hpY)) _viewModel.HpProbe.Y = hpY;
        if (int.TryParse(MpPosX.Text, out var mpX)) _viewModel.MpProbe.X = mpX;
        if (int.TryParse(MpPosY.Text, out var mpY)) _viewModel.MpProbe.Y = mpY;
        
        if (int.TryParse(HpActionX.Text, out var hpAx)) _viewModel.HpTrigger.X = hpAx;
        if (int.TryParse(HpActionY.Text, out var hpAy)) _viewModel.HpTrigger.Y = hpAy;
        if (int.TryParse(MpActionX.Text, out var mpAx)) _viewModel.MpTrigger.X = mpAx;
        if (int.TryParse(MpActionY.Text, out var mpAy)) _viewModel.MpTrigger.Y = mpAy;
        
        _viewModel.HpProbe.Tolerance = (int)HpToleranceSlider.Value;
        _viewModel.MpProbe.Tolerance = (int)MpToleranceSlider.Value;
        
        // Periodic clicks
        _viewModel.YClick.Enabled = YPeriodicEnabled.IsChecked ?? false;
        if (int.TryParse(YPeriodicX.Text, out var yX)) _viewModel.YClick.X = yX;
        if (int.TryParse(YPeriodicY.Text, out var yY)) _viewModel.YClick.Y = yY;
        if (int.TryParse(YPeriodicPeriod.Text, out var yPeriod)) _viewModel.YClick.PeriodMs = yPeriod;
        
        _viewModel.Extra1Click.Enabled = Extra1PeriodicEnabled.IsChecked ?? false;
        if (int.TryParse(Extra1PeriodicX.Text, out var e1X)) _viewModel.Extra1Click.X = e1X;
        if (int.TryParse(Extra1PeriodicY.Text, out var e1Y)) _viewModel.Extra1Click.Y = e1Y;
        if (int.TryParse(Extra1PeriodicPeriod.Text, out var e1Period)) _viewModel.Extra1Click.PeriodMs = e1Period;
        
        _viewModel.Extra2Click.Enabled = Extra2PeriodicEnabled.IsChecked ?? false;
        if (int.TryParse(Extra2PeriodicX.Text, out var e2X)) _viewModel.Extra2Click.X = e2X;
        if (int.TryParse(Extra2PeriodicY.Text, out var e2Y)) _viewModel.Extra2Click.Y = e2Y;
        if (int.TryParse(Extra2PeriodicPeriod.Text, out var e2Period)) _viewModel.Extra2Click.PeriodMs = e2Period;
        
        _viewModel.Extra3Click.Enabled = Extra3PeriodicEnabled.IsChecked ?? false;
        if (int.TryParse(Extra3PeriodicX.Text, out var e3X)) _viewModel.Extra3Click.X = e3X;
        if (int.TryParse(Extra3PeriodicY.Text, out var e3Y)) _viewModel.Extra3Click.Y = e3Y;
        if (int.TryParse(Extra3PeriodicPeriod.Text, out var e3Period)) _viewModel.Extra3Click.PeriodMs = e3Period;
    }
}