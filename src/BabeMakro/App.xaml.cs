using System;
using System.Windows;
using BabeMakro.Services;

namespace BabeMakro;

public partial class App : Application
{
    private readonly ApiService _apiService;

    public App()
    {
        _apiService = new ApiService();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Handle unhandled exceptions
        this.DispatcherUnhandledException += (s, ex) =>
        {
            MessageBox.Show($"Application Error:\n{ex.Exception.Message}\n\nStack Trace:\n{ex.Exception.StackTrace}",
                "BabeMakro Error", MessageBoxButton.OK, MessageBoxImage.Error);
            ex.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (s, ex) =>
        {
            MessageBox.Show($"Critical Error:\n{ex.ExceptionObject}",
                "BabeMakro Critical Error", MessageBoxButton.OK, MessageBoxImage.Error);
        };

        // Check if user is already logged in and has valid license
        await CheckAuthenticationAndLicense();
    }

    private async System.Threading.Tasks.Task CheckAuthenticationAndLicense()
    {
        try
        {
            // For testing purposes, always show login window first
            // Later this will check token and license status
            ShowLoginWindow();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error during startup: {ex.Message}",
                "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ShowLoginWindow()
    {
        var loginWindow = new LoginWindow();
        loginWindow.Show();
    }

    private void ShowLicenseWindow()
    {
        var licenseWindow = new LicenseWindow();
        licenseWindow.Show();
    }

    private void ShowMainWindow()
    {
        var mainWindow = new PixelAutomation.Tool.Overlay.WPF.MainWindow();
        mainWindow.Show();
    }
}