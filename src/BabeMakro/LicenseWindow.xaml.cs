using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using BabeMakro.Services;

namespace BabeMakro
{
    public partial class LicenseWindow : Window
    {
        private readonly ApiService _apiService;
        private bool _isProcessing = false;

        public LicenseWindow()
        {
            InitializeComponent();
            _apiService = new ApiService();
            LicenseKeyTextBox.Focus();

            // User came here because they don't have an active license
            // No need to check again
            StatusText.Text = "Please enter your license key to activate.";
            StatusText.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(139, 233, 253));
        }

        private async Task RefreshLicenseStatus()
        {
            if (_isProcessing) return;

            _isProcessing = true;
            SetControlsEnabled(false);
            LoadingProgressBar.Visibility = Visibility.Visible;
            StatusText.Text = "Checking license status...";
            StatusText.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(139, 233, 253));

            try
            {
                var response = await _apiService.ValidateLicenseAsync();

                if (response.Success && response.Data != null)
                {
                    if (response.Data.IsValid)
                    {
                        StatusText.Text = $"License is active! {response.Data.DaysRemaining} days remaining.";
                        StatusText.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(80, 250, 123));

                        await Task.Delay(1500);

                        var mainWindow = new PixelAutomation.Tool.Overlay.WPF.MainWindow();
                        mainWindow.Show();
                        this.Close();
                    }
                    else
                    {
                        StatusText.Text = "License expired or invalid. Please enter a new license key.";
                        StatusText.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 85, 85));
                    }
                }
                else
                {
                    if (response.Message != null && response.Message.Contains("Session expired"))
                    {
                        StatusText.Text = "Session expired. Please login again.";
                        StatusText.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 184, 108));
                    }
                    else
                    {
                        StatusText.Text = response.Message ?? "No active license found. Please enter your license key.";
                        StatusText.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 85, 85));
                    }
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Error checking license: {ex.Message}";
                StatusText.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 85, 85));
            }
            finally
            {
                _isProcessing = false;
                SetControlsEnabled(true);
                LoadingProgressBar.Visibility = Visibility.Collapsed;
            }
        }

        private async void ActivateButton_Click(object sender, RoutedEventArgs e)
        {
            await ActivateLicense();
        }

        private async Task ActivateLicense()
        {
            if (_isProcessing) return;

            var licenseKey = LicenseKeyTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(licenseKey))
            {
                MessageBox.Show("Please enter a license key.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                LicenseKeyTextBox.Focus();
                return;
            }

            if (!IsValidLicenseKeyFormat(licenseKey))
            {
                MessageBox.Show("Invalid license key format. Please use the format: XXXXX-XXXXX-XXXXX-XXXXX-XXXXX", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                LicenseKeyTextBox.Focus();
                return;
            }

            _isProcessing = true;
            SetControlsEnabled(false);
            LoadingProgressBar.Visibility = Visibility.Visible;
            StatusText.Text = "Activating license...";
            StatusText.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(139, 233, 253));

            try
            {
                // Check if user is logged in (has token)
                var hasToken = !string.IsNullOrEmpty(BabeMakro.Properties.Settings.Default.Token);

                ApiResponse<LicenseActivateResponse> response;

                if (hasToken)
                {
                    // User is logged in, use token-based activation
                    System.Diagnostics.Debug.WriteLine("Using token-based license activation");
                    response = await _apiService.ActivateLicenseAsync(licenseKey);
                }
                else
                {
                    // User is not logged in, use saved credentials for tokenless activation
                    System.Diagnostics.Debug.WriteLine("User not logged in, using saved credentials for tokenless activation");

                    var email = BabeMakro.Properties.Settings.Default.LastLoggedInEmail ?? "";
                    var password = BabeMakro.Properties.Settings.Default.LastLoggedInPassword ?? "";

                    if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
                    {
                        MessageBox.Show("No saved login credentials found. Please login first.", "Login Required", MessageBoxButton.OK, MessageBoxImage.Information);

                        // Redirect to login window
                        var loginWindow = new LoginWindow();
                        loginWindow.Show();
                        this.Close();
                        return;
                    }

                    response = await _apiService.ActivateLicenseTokenlessAsync(email, password, licenseKey);
                }

                if (response.Success && response.Data != null)
                {
                    StatusText.Text = "License activated successfully!";
                    StatusText.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(80, 250, 123));

                    await Task.Delay(1500);

                    var mainWindow = new PixelAutomation.Tool.Overlay.WPF.MainWindow();
                    mainWindow.Show();
                    this.Close();
                }
                else
                {
                    MessageBox.Show(response.Message ?? "License activation failed. Please check your license key.", "Activation Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    StatusText.Text = "";
                    LicenseKeyTextBox.Clear();
                    LicenseKeyTextBox.Focus();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred during activation: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText.Text = "";
            }
            finally
            {
                _isProcessing = false;
                SetControlsEnabled(true);
                LoadingProgressBar.Visibility = Visibility.Collapsed;
            }
        }

        private bool IsValidLicenseKeyFormat(string key)
        {
            // Pattern for 29 characters: XXXXX-XXXXX-XXXXX-XXXXX-XXXXX (5-5-5-5-5 format)
            var pattern = @"^[A-Z0-9]{5}-[A-Z0-9]{5}-[A-Z0-9]{5}-[A-Z0-9]{5}-[A-Z0-9]{5}$";
            return Regex.IsMatch(key, pattern);
        }

        private void SetControlsEnabled(bool enabled)
        {
            LicenseKeyTextBox.IsEnabled = enabled;
            ActivateButton.IsEnabled = enabled;
            LogoutButton.IsEnabled = enabled;
            RefreshButton.IsEnabled = enabled;
        }

        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isProcessing) return;

            var result = MessageBox.Show("Are you sure you want to logout?", "Confirm Logout", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                _apiService.ClearToken();
                var loginWindow = new LoginWindow();
                loginWindow.Show();
                this.Close();
            }
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            await RefreshLicenseStatus();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private async void LicenseKeyTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                await ActivateLicense();
            }
        }

        private void LicenseKeyTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            var text = LicenseKeyTextBox.Text.Replace("-", "").ToUpper();
            if (text.Length > 25)
            {
                text = text.Substring(0, 25);
            }

            var formattedText = "";
            for (int i = 0; i < text.Length; i++)
            {
                // Add dash after every 5 characters, except for the last group which can be 4 characters
                if (i > 0 && i % 5 == 0)
                {
                    formattedText += "-";
                }
                formattedText += text[i];
            }

            if (LicenseKeyTextBox.Text != formattedText)
            {
                var caretIndex = LicenseKeyTextBox.CaretIndex;
                var oldLength = LicenseKeyTextBox.Text.Length;
                LicenseKeyTextBox.Text = formattedText;

                // Adjust caret position for new formatting
                if (caretIndex < formattedText.Length)
                {
                    LicenseKeyTextBox.CaretIndex = Math.Min(caretIndex + (formattedText.Length - oldLength), formattedText.Length);
                }
                else
                {
                    LicenseKeyTextBox.CaretIndex = formattedText.Length;
                }
            }

            if (IsValidLicenseKeyFormat(formattedText))
            {
                LicenseKeyFormatText.Text = "✓ Valid format";
                LicenseKeyFormatText.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(80, 250, 123));
            }
            else
            {
                LicenseKeyFormatText.Text = "Format: XXXXX-XXXXX-XXXXX-XXXXX-XXXXX";
                LicenseKeyFormatText.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(108, 111, 133));
            }
        }
    }
}