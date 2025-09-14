using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using BabeMakro.Services;

namespace BabeMakro
{
    public partial class LoginWindow : Window
    {
        private readonly ApiService _apiService;
        private bool _isProcessing = false;

        public LoginWindow()
        {
            InitializeComponent();
            _apiService = new ApiService();
            EmailTextBox.Focus();
        }

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            await PerformLogin();
        }

        private async Task PerformLogin()
        {
            if (_isProcessing) return;

            var email = EmailTextBox.Text.Trim();
            var password = PasswordBox.Password;

            if (string.IsNullOrWhiteSpace(email))
            {
                MessageBox.Show("Please enter your email address.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                EmailTextBox.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                MessageBox.Show("Please enter your password.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                PasswordBox.Focus();
                return;
            }

            _isProcessing = true;
            SetControlsEnabled(false);
            LoadingProgressBar.Visibility = Visibility.Visible;

            try
            {
                var response = await _apiService.LoginAsync(email, password);

                if (response.Success && response.Data != null)
                {
                    // Save email and password for later use (tokenless license activation)
                    BabeMakro.Properties.Settings.Default.LastLoggedInEmail = email;
                    BabeMakro.Properties.Settings.Default.LastLoggedInPassword = password;
                    BabeMakro.Properties.Settings.Default.Save();

                    // Check license status from login response
                    if (response.Data.User != null && response.Data.User.HasActiveLicense)
                    {
                        // User has an active license, go to main window
                        var mainWindow = new PixelAutomation.Tool.Overlay.WPF.MainWindow();
                        mainWindow.Show();
                        this.Close();
                    }
                    else
                    {
                        // User doesn't have an active license, go to license window
                        var licenseWindow = new LicenseWindow();
                        licenseWindow.Show();
                        this.Close();
                    }
                }
                else
                {
                    var errorMessage = response.Message ?? "Login failed. Please check your credentials.";
                    if (string.IsNullOrWhiteSpace(errorMessage))
                    {
                        errorMessage = "Login failed with unknown error. Please try again.";
                    }
                    MessageBox.Show(errorMessage, "Login Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    PasswordBox.Clear();
                    PasswordBox.Focus();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred during login: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isProcessing = false;
                SetControlsEnabled(true);
                LoadingProgressBar.Visibility = Visibility.Collapsed;
            }
        }

        private void SetControlsEnabled(bool enabled)
        {
            EmailTextBox.IsEnabled = enabled;
            PasswordBox.IsEnabled = enabled;
            LoginButton.IsEnabled = enabled;
            RegisterLink.IsEnabled = enabled;
        }

        private void RegisterLink_Click(object sender, RoutedEventArgs e)
        {
            if (_isProcessing) return;

            var registerWindow = new RegisterWindow();
            registerWindow.Show();
            this.Close();
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

        private async void TextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (sender == EmailTextBox)
                {
                    PasswordBox.Focus();
                }
                else
                {
                    await PerformLogin();
                }
            }
        }

        private async void PasswordBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                await PerformLogin();
            }
        }
    }
}