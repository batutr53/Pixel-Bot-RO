using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using BabeMakro.Services;

namespace BabeMakro
{
    public partial class RegisterWindow : Window
    {
        private readonly ApiService _apiService;
        private bool _isProcessing = false;

        public RegisterWindow()
        {
            InitializeComponent();
            _apiService = new ApiService();
            EmailTextBox.Focus();
        }

        private async void RegisterButton_Click(object sender, RoutedEventArgs e)
        {
            await PerformRegister();
        }

        private async Task PerformRegister()
        {
            if (_isProcessing) return;

            var email = EmailTextBox.Text.Trim();
            var password = PasswordBox.Password;
            var confirmPassword = ConfirmPasswordBox.Password;

            if (string.IsNullOrWhiteSpace(email))
            {
                MessageBox.Show("Please enter your email address.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                EmailTextBox.Focus();
                return;
            }

            if (!IsValidEmail(email))
            {
                MessageBox.Show("Please enter a valid email address.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                EmailTextBox.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                MessageBox.Show("Please enter a password.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                PasswordBox.Focus();
                return;
            }

            if (password.Length < 6)
            {
                MessageBox.Show("Password must be at least 6 characters long.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                PasswordBox.Focus();
                return;
            }

            if (password != confirmPassword)
            {
                MessageBox.Show("Passwords do not match.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                ConfirmPasswordBox.Focus();
                return;
            }

            _isProcessing = true;
            SetControlsEnabled(false);
            LoadingProgressBar.Visibility = Visibility.Visible;

            try
            {
                var response = await _apiService.RegisterAsync(email, password);

                if (response.Success)
                {
                    MessageBox.Show("Registration successful! Please login with your credentials.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                    var loginWindow = new LoginWindow();
                    loginWindow.Show();
                    this.Close();
                }
                else
                {
                    MessageBox.Show(response.Message ?? "Registration failed. Please try again.", "Registration Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred during registration: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isProcessing = false;
                SetControlsEnabled(true);
                LoadingProgressBar.Visibility = Visibility.Collapsed;
            }
        }

        private bool IsValidEmail(string email)
        {
            try
            {
                var regex = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$");
                return regex.IsMatch(email);
            }
            catch
            {
                return false;
            }
        }

        private void SetControlsEnabled(bool enabled)
        {
            EmailTextBox.IsEnabled = enabled;
            PasswordBox.IsEnabled = enabled;
            ConfirmPasswordBox.IsEnabled = enabled;
            RegisterButton.IsEnabled = enabled;
            LoginLink.IsEnabled = enabled;
        }

        private void LoginLink_Click(object sender, RoutedEventArgs e)
        {
            if (_isProcessing) return;

            var loginWindow = new LoginWindow();
            loginWindow.Show();
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

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            CheckPasswordMatch();
        }

        private void ConfirmPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            CheckPasswordMatch();
        }

        private void CheckPasswordMatch()
        {
            if (string.IsNullOrEmpty(PasswordBox.Password) || string.IsNullOrEmpty(ConfirmPasswordBox.Password))
            {
                PasswordMatchText.Text = "";
                return;
            }

            if (PasswordBox.Password == ConfirmPasswordBox.Password)
            {
                PasswordMatchText.Text = "✓ Passwords match";
                PasswordMatchText.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(80, 250, 123));
            }
            else
            {
                PasswordMatchText.Text = "✗ Passwords do not match";
                PasswordMatchText.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 85, 85));
            }
        }

        private async void TextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                PasswordBox.Focus();
            }
        }

        private async void PasswordBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ConfirmPasswordBox.Focus();
            }
        }

        private async void ConfirmPasswordBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                await PerformRegister();
            }
        }
    }
}