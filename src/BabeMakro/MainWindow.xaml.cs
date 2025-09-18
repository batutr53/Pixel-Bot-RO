using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Threading;
using System.Windows.Media;
using System.ComponentModel;
using Microsoft.Win32;
using BabeMakro.Services;
using PixelAutomation.Tool.Overlay.WPF.Controls;
using PixelAutomation.Core.Models;
using PixelAutomation.Tool.Overlay.WPF.Services;
using PixelAutomation.Tool.Overlay.WPF.ViewModels;
using PixelAutomation.Tool.Overlay.WPF.Models;

namespace PixelAutomation.Tool.Overlay.WPF
{
    public partial class MainWindow : Window
    {
        private readonly ApiService _apiService;
        private readonly List<ClientCard> _clientCards = new();
        private readonly MainViewModel _viewModel;
        private readonly ConfigurationManager _configManager;
        private readonly DispatcherTimer _updateTimer;
        private bool _isOverlayMode = false;

        // Master Control System
        private bool _masterAttackRunning = false;
        private bool _masterHealRunning = false;
        private bool _panicModeActive = false;
        private bool _visualIndicatorsActive = false;

        // Public access to overlay canvas for client cards
        public System.Windows.Controls.Canvas GetOverlayCanvas() => OverlayCanvas;

        public MainWindow()
        {
            InitializeComponent();
            _apiService = new ApiService();

            _viewModel = new MainViewModel();
            DataContext = _viewModel;
            _configManager = new ConfigurationManager();

            _updateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(200)
            };
            _updateTimer.Tick += UpdateTimer_Tick;
            _updateTimer.Start();

            // Auto-save configuration on window closing
            this.Closing += MainWindow_Closing;

            StatusText.Text = "BabeMakro Control Panel Ready";
            InitializeClientCards();
            LoadProfiles();

            // Auto-assign MuMu windows after UI is fully loaded
            Loaded += MainWindow_Loaded;
        }

        private void InitializeClientCards()
        {
            try
            {
                // Get predefined tab items
                var tabItems = new[] { Client1Tab, Client2Tab, Client3Tab, Client4Tab, Client5Tab, Client6Tab, Client7Tab, Client8Tab };

                for (int i = 1; i <= 8; i++)
                {
                    var clientCard = new ClientCard();
                    clientCard.Initialize(i, $"Client {i}");
                    _clientCards.Add(clientCard);

                    // Set the ClientCard as the content of the corresponding tab
                    var tabItem = tabItems[i - 1];
                    tabItem.Content = new ScrollViewer
                    {
                        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                        Content = clientCard,
                        Padding = new Thickness(10)
                    };
                }

                StatusText.Text = "Client tabs initialized successfully";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Error initializing client cards: {ex.Message}";
            }
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // UI fully loaded, now auto-assign MuMu windows
            // AutoAssignMuMuWindows(); // Removed for now
        }

        private void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            // Auto-save on closing
            try
            {
                SaveConfig_Click(this, new RoutedEventArgs());
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Auto-save failed: {ex.Message}";
            }
        }

        private void LoadProfiles()
        {
            try
            {
                var config = _configManager.LoadConfiguration("config.json");
                if (config?.Profiles != null)
                {
                    if (config.Profiles.Any())
                    {
                        _viewModel.ActiveProfile = config.Profiles.Keys.First();
                        LoadSelectedProfile();
                    }
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Config load failed: {ex.Message}";
            }
        }

        private void LoadSelectedProfile()
        {
            var profileName = _viewModel.ActiveProfile ?? "Default";
            if (!string.IsNullOrEmpty(profileName))
            {
                try
                {
                    var config = _configManager.LoadConfiguration("config.json");
                    if (config?.Profiles.TryGetValue(profileName, out var profile) == true)
                    {
                        _viewModel.ActiveProfile = profileName;

                        // Load window configurations to client cards
                        for (int i = 0; i < Math.Min(profile.Windows.Count, _clientCards.Count); i++)
                        {
                            LoadWindowConfigToCard(_clientCards[i], profile.Windows[i]);
                            _clientCards[i].UpdateUI(); // Update UI after loading config
                        }

                        StatusText.Text = $"Loaded profile: {profileName}";
                    }
                }
                catch (Exception ex)
                {
                    StatusText.Text = $"Profile load failed: {ex.Message}";
                }
            }
        }

        private void LoadWindowConfigToCard(ClientCard card, WindowTarget windowConfig)
        {
            // Load HP probe
            var hpProbe = windowConfig.Probes.FirstOrDefault(p => p.Name.Contains("R") || p.Name.Contains("HP"));
            if (hpProbe != null)
            {
                card.ViewModel.HpProbe.X = hpProbe.X;
                card.ViewModel.HpProbe.Y = hpProbe.Y;
                if (hpProbe.RefColor.Length >= 3)
                {
                    var color = System.Drawing.Color.FromArgb(hpProbe.RefColor[0], hpProbe.RefColor[1], hpProbe.RefColor[2]);
                    card.ViewModel.HpProbe.ExpectedColor = color;
                }
                card.ViewModel.HpProbe.Tolerance = hpProbe.Tolerance;
            }

            // Load MP probe
            var mpProbe = windowConfig.Probes.FirstOrDefault(p => p.Name.Contains("B") || p.Name.Contains("MP"));
            if (mpProbe != null)
            {
                card.ViewModel.MpProbe.X = mpProbe.X;
                card.ViewModel.MpProbe.Y = mpProbe.Y;
                if (mpProbe.RefColor.Length >= 3)
                {
                    var color = System.Drawing.Color.FromArgb(mpProbe.RefColor[0], mpProbe.RefColor[1], mpProbe.RefColor[2]);
                    card.ViewModel.MpProbe.ExpectedColor = color;
                }
                card.ViewModel.MpProbe.Tolerance = mpProbe.Tolerance;
            }

            // Load events (HP/MP trigger clicks)
            var hpEvent = windowConfig.Events.FirstOrDefault(e => e.When.Contains("R") || e.When.Contains("HP"));
            if (hpEvent != null)
            {
                card.ViewModel.HpTrigger.X = hpEvent.Click.X;
                card.ViewModel.HpTrigger.Y = hpEvent.Click.Y;
                card.ViewModel.HpTrigger.CooldownMs = hpEvent.CooldownMs ?? 120;
                card.ViewModel.HpTrigger.Enabled = true;
                card.ViewModel.HpTrigger.UseCoordinate = hpEvent.Click.UseCoordinate;
                card.ViewModel.HpTrigger.UseKeyPress = hpEvent.Click.UseKeyPress;
                card.ViewModel.HpTrigger.KeyToPress = hpEvent.Click.KeyToPress ?? "F1";
            }

            var mpEvent = windowConfig.Events.FirstOrDefault(e => e.When.Contains("B") || e.When.Contains("MP"));
            if (mpEvent != null)
            {
                card.ViewModel.MpTrigger.X = mpEvent.Click.X;
                card.ViewModel.MpTrigger.Y = mpEvent.Click.Y;
                card.ViewModel.MpTrigger.CooldownMs = mpEvent.CooldownMs ?? 120;
                card.ViewModel.MpTrigger.Enabled = true;
                card.ViewModel.MpTrigger.UseCoordinate = mpEvent.Click.UseCoordinate;
                card.ViewModel.MpTrigger.UseKeyPress = mpEvent.Click.UseKeyPress;
                card.ViewModel.MpTrigger.KeyToPress = mpEvent.Click.KeyToPress ?? "F2";
            }

            // Load periodic clicks
            var yClick = windowConfig.PeriodicClicks.FirstOrDefault(p => p.Name == "Y");
            if (yClick != null)
            {
                card.ViewModel.YClick.X = yClick.X;
                card.ViewModel.YClick.Y = yClick.Y;
                card.ViewModel.YClick.PeriodMs = yClick.PeriodMs ?? (int)(yClick.PeriodSec * 1000 ?? 1000);
                card.ViewModel.YClick.Enabled = yClick.Enabled;
                card.ViewModel.YClick.UseCoordinate = yClick.UseCoordinate;
                card.ViewModel.YClick.UseKeyPress = yClick.UseKeyPress;
                card.ViewModel.YClick.KeyToPress = yClick.KeyToPress ?? "Y";
            }

            // Load all UI settings from saved configuration
            if (windowConfig.UISettings?.Values != null && windowConfig.UISettings.Values.Any())
            {
                card.SetAllUIValues(windowConfig.UISettings.Values);
            }
        }

        private void UpdateTimer_Tick(object? sender, EventArgs e)
        {
            // Update connected clients count
            var connectedCount = _clientCards.Count(c => c.ViewModel.TargetHwnd != IntPtr.Zero);
            ConnectedClientsText.Text = $"{connectedCount}/8";

            // Update total stats
            var totalClicks = _clientCards.Sum(c => c.ViewModel.ClickCount);
            var runningClients = _clientCards.Where(c => c.ViewModel.IsRunning).ToList();
            var averageFps = runningClients.Any() ? runningClients.Average(c => c.ViewModel.Fps) : 0.0;
            var activeWorkers = _clientCards.Count(c => c.ViewModel.IsRunning);

            TotalClicksText.Text = totalClicks.ToString();
            FpsText.Text = double.IsNaN(averageFps) ? "0" : averageFps.ToString("F1");
            ActiveWorkersText.Text = activeWorkers.ToString();
        }

        private void SaveConfig_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var config = BuildConfigFromClientCards();
                _configManager.SaveConfiguration("config.json", config);
                StatusText.Text = "Configuration saved successfully";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Save failed: {ex.Message}";
                MessageBox.Show($"Error saving configuration: {ex.Message}", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private Configuration BuildConfigFromClientCards()
        {
            var config = new Configuration();
            var profileName = _viewModel.ActiveProfile ?? "Default";

            var profile = new ProfileConfig
            {
                Global = new GlobalConfig
                {
                    CaptureMode = "WGC",
                    ClickMode = "message",
                    DefaultHz = 80
                },
                Windows = new List<WindowTarget>()
            };

            foreach (var card in _clientCards)
            {
                var window = new WindowTarget
                {
                    TitleRegex = card.ViewModel.ClientName,
                    HwndString = card.ViewModel.TargetHwnd != IntPtr.Zero ? card.ViewModel.TargetHwnd.ToInt64().ToString() : null,
                    Probes = new List<ProbeConfig>
                    {
                        new ProbeConfig
                        {
                            Name = $"HP{card.ClientId}",
                            Kind = "point",
                            X = card.ViewModel.HpProbe.X,
                            Y = card.ViewModel.HpProbe.Y,
                            Box = 5,
                            Mode = "edge",
                            Metric = "rgb",
                            RefColor = new[] { (int)card.ViewModel.HpProbe.ExpectedColor.R, (int)card.ViewModel.HpProbe.ExpectedColor.G, (int)card.ViewModel.HpProbe.ExpectedColor.B },
                            ToColor = new[] { (int)card.ViewModel.HpProbe.TriggerColor.R, (int)card.ViewModel.HpProbe.TriggerColor.G, (int)card.ViewModel.HpProbe.TriggerColor.B },
                            Tolerance = card.ViewModel.HpProbe.Tolerance,
                            DebounceMs = 30
                        },
                        new ProbeConfig
                        {
                            Name = $"MP{card.ClientId}",
                            Kind = "point",
                            X = card.ViewModel.MpProbe.X,
                            Y = card.ViewModel.MpProbe.Y,
                            Box = 5,
                            Mode = "edge",
                            Metric = "rgb",
                            RefColor = new[] { (int)card.ViewModel.MpProbe.ExpectedColor.R, (int)card.ViewModel.MpProbe.ExpectedColor.G, (int)card.ViewModel.MpProbe.ExpectedColor.B },
                            ToColor = new[] { (int)card.ViewModel.MpProbe.TriggerColor.R, (int)card.ViewModel.MpProbe.TriggerColor.G, (int)card.ViewModel.MpProbe.TriggerColor.B },
                            Tolerance = card.ViewModel.MpProbe.Tolerance,
                            DebounceMs = 30
                        }
                    },
                    Events = new List<EventConfig>
                    {
                        new EventConfig
                        {
                            When = $"HP{card.ClientId}:edge-down",
                            Click = new ClickTarget {
                                X = card.ViewModel.HpTrigger.X,
                                Y = card.ViewModel.HpTrigger.Y,
                                UseCoordinate = card.ViewModel.HpTrigger.UseCoordinate,
                                UseKeyPress = card.ViewModel.HpTrigger.UseKeyPress,
                                KeyToPress = card.ViewModel.HpTrigger.KeyToPress
                            },
                            CooldownMs = card.ViewModel.HpTrigger.CooldownMs,
                            Priority = 1
                        },
                        new EventConfig
                        {
                            When = $"MP{card.ClientId}:edge-down",
                            Click = new ClickTarget {
                                X = card.ViewModel.MpTrigger.X,
                                Y = card.ViewModel.MpTrigger.Y,
                                UseCoordinate = card.ViewModel.MpTrigger.UseCoordinate,
                                UseKeyPress = card.ViewModel.MpTrigger.UseKeyPress,
                                KeyToPress = card.ViewModel.MpTrigger.KeyToPress
                            },
                            CooldownMs = card.ViewModel.MpTrigger.CooldownMs,
                            Priority = 1
                        }
                    },
                    PeriodicClicks = new List<PeriodicClickConfig>
                    {
                        new PeriodicClickConfig
                        {
                            Name = "Y",
                            X = card.ViewModel.YClick.X,
                            Y = card.ViewModel.YClick.Y,
                            PeriodMs = card.ViewModel.YClick.PeriodMs,
                            Enabled = card.ViewModel.YClick.Enabled,
                            UseCoordinate = card.ViewModel.YClick.UseCoordinate,
                            UseKeyPress = card.ViewModel.YClick.UseKeyPress,
                            KeyToPress = card.ViewModel.YClick.KeyToPress
                        }
                    },
                    UISettings = new UISettings
                    {
                        Values = card.GetAllUIValues()
                    }
                };
                profile.Windows.Add(window);
            }

            config.Profiles = new Dictionary<string, ProfileConfig> { { profileName, profile } };
            return config;
        }

        private void LoadConfig_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                LoadSelectedProfile();
                StatusText.Text = "Configuration loaded successfully!";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading configuration: {ex.Message}", "Load Error", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText.Text = "Load failed";
            }
        }

        private void VisualIndicatorsToggle_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _visualIndicatorsActive = !_visualIndicatorsActive;

                if (_visualIndicatorsActive)
                {
                    VisualIndicatorsToggleButton.Content = "👁️ Hide";
                    VisualIndicatorsToggleButton.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(40, 167, 69)); // Green
                    StatusText.Text = "Visual indicators enabled - HP/MP coordinates visible";

                    // Enable visual indicators on all client cards
                    foreach (var clientCard in _clientCards)
                    {
                        clientCard.ShowVisualIndicators(true);
                    }
                }
                else
                {
                    VisualIndicatorsToggleButton.Content = "👁️ Visual";
                    VisualIndicatorsToggleButton.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(108, 117, 125)); // Gray
                    StatusText.Text = "Visual indicators disabled";

                    // Disable visual indicators on all client cards
                    foreach (var clientCard in _clientCards)
                    {
                        clientCard.ShowVisualIndicators(false);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error toggling visual indicators: {ex.Message}", "Visual Error", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText.Text = "Visual toggle failed";
            }
        }

        private void PanicToggle_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (PanicToggleButton.Content.ToString().Contains("START"))
                {
                    PanicToggleButton.Content = "🛑 PANIC STOP";
                    PanicToggleButton.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(220, 53, 69)); // Red
                    StatusText.Text = "PANIC MODE ACTIVE - All automation started";
                }
                else
                {
                    PanicToggleButton.Content = "🚀 PANIC START";
                    PanicToggleButton.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(40, 167, 69)); // Green
                    StatusText.Text = "PANIC MODE STOPPED - All automation halted";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error toggling panic mode: {ex.Message}", "Panic Error", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText.Text = "Panic toggle failed";
            }
        }

        // Window drag functionality
        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        // Close window functionality
        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            // Clean up any resources if needed
        }
    }
}