using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
// System.Windows.Shapes used via full qualification below to avoid Path ambiguity
using Microsoft.Win32;

namespace WhiteLabelLauncher
{
    public partial class MainWindow : Window
    {
        // ─────────────────────────────────────────────────────────────────
        //  Style presets  (gradient dark → medium, accent colour)
        //  First 4 match brand colours; extras cycle for user apps.
        // ─────────────────────────────────────────────────────────────────
        private static readonly (string G1, string G2, string Accent)[] AppStyles =
        {
            ("#2A1A00", "#4A2E00", "#FF9A00"), // 0  orange  – Illustrator
            ("#001830", "#002A50", "#31A8FF"), // 1  blue    – Photoshop
            ("#0D0020", "#1E0040", "#9999FF"), // 2  purple  – Premiere Pro
            ("#300000", "#500000", "#FF4040"), // 3  red     – Acrobat DC
            ("#003020", "#005030", "#50FA7B"), // 4  green
            ("#201500", "#3A2600", "#FFB86C"), // 5  amber
            ("#001820", "#002830", "#8BE9FD"), // 6  cyan
            ("#220015", "#3A0025", "#FF79C6"), // 7  pink
            ("#160020", "#240035", "#BD93F9"), // 8  lavender
            ("#1C1A00", "#302C00", "#F1FA8C"), // 9  yellow
        };

        // ─────────────────────────────────────────────────────────────────
        //  Paths
        // ─────────────────────────────────────────────────────────────────
        private static readonly string DataDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WhiteLabelLauncher");
        private static readonly string AppsFile  = Path.Combine(DataDir, "apps.json");
        private static readonly string SettingsFile = Path.Combine(DataDir, "settings.json");

        // ─────────────────────────────────────────────────────────────────
        //  State
        // ─────────────────────────────────────────────────────────────────
        private List<AppEntry> _apps = new();

        public class GlobalSettings { public string LauncherIconPath { get; set; } = ""; }
        private GlobalSettings _settings = new();

        // ─────────────────────────────────────────────────────────────────
        //  Constructor
        // ─────────────────────────────────────────────────────────────────
        public MainWindow()
        {
            InitializeComponent();
            LoadSettings();
            LoadApps();
            BuildAppTiles();
            ShowAppsView();
        }

        // ═════════════════════════════════════════════════════════════════
        //  Global Settings
        // ═════════════════════════════════════════════════════════════════
        private void LoadSettings()
        {
            try
            {
                if (File.Exists(SettingsFile))
                {
                    _settings = JsonSerializer.Deserialize<GlobalSettings>(File.ReadAllText(SettingsFile)) ?? new();
                }
            }
            catch { _settings = new(); }
            RefreshLauncherLogo();
        }

        private void SaveSettings()
        {
            try
            {
                Directory.CreateDirectory(DataDir);
                File.WriteAllText(SettingsFile, JsonSerializer.Serialize(
                    _settings, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }

        private void RefreshLauncherLogo()
        {
            if (!string.IsNullOrEmpty(_settings.LauncherIconPath) && File.Exists(_settings.LauncherIconPath))
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = new Uri(_settings.LauncherIconPath);
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                bmp.EndInit();
                LauncherLogoBorder.Background = new ImageBrush(bmp) { Stretch = Stretch.UniformToFill };
                LauncherLogoText.Visibility = Visibility.Collapsed;
            }
            else
            {
                var grad = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0), EndPoint = new Point(1, 1)
                };
                grad.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#FF3B30"), 0));
                grad.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#FF9500"), 1));
                LauncherLogoBorder.Background = grad;
                LauncherLogoText.Visibility = Visibility.Visible;
            }
        }

        // ═════════════════════════════════════════════════════════════════
        //  JSON persistence
        // ═════════════════════════════════════════════════════════════════
        private static List<AppEntry> DefaultApps() => new()
        {
            new AppEntry
            {
                Id = "sample-app-1", Name = "Sample App 1",
                Abbrev = "S1", Description = "Sample description 1",
                ExePath = @"C:\Program Files\SampleApp1\SampleApp.exe"
            },
            new AppEntry
            {
                Id = "sample-app-2", Name = "Sample App 2",
                Abbrev = "S2", Description = "Sample description 2",
                ExePath = @"C:\Program Files\SampleApp2\SampleApp.exe"
            },
            new AppEntry
            {
                Id = "sample-app-3", Name = "Sample App 3",
                Abbrev = "S3", Description = "Sample description 3",
                ExePath = @"C:\Program Files\SampleApp3\SampleApp.exe"
            },
            new AppEntry
            {
                Id = "sample-app-4", Name = "Sample App 4",
                Abbrev = "S4", Description = "Sample description 4",
                ExePath = @"C:\Program Files\SampleApp4\SampleApp.exe"
            },
        };

        private void LoadApps()
        {
            try
            {
                if (File.Exists(AppsFile))
                {
                    _apps = JsonSerializer.Deserialize<List<AppEntry>>(File.ReadAllText(AppsFile)) ?? DefaultApps();
                    return;
                }
            }
            catch { /* fall through to defaults */ }

            _apps = DefaultApps();
            SaveApps(); // persist defaults on first run
        }

        private void SaveApps()
        {
            try
            {
                Directory.CreateDirectory(DataDir);
                File.WriteAllText(AppsFile, JsonSerializer.Serialize(
                    _apps, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }

        // ═════════════════════════════════════════════════════════════════
        //  Dynamic tile builders
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// Clears and rebuilds every tile in the WrapPanel (app tiles + "+" tile).
        /// Safe to call at any time.
        /// </summary>
        private void BuildAppTiles()
        {
            AppsWrapPanel.Children.Clear();

            for (int i = 0; i < _apps.Count; i++)
                AppsWrapPanel.Children.Add(CreateAppTile(_apps[i], i));

            AppsWrapPanel.Children.Add(CreateAddTile());
        }

        // ── Single app tile ──────────────────────────────────────────────
        private UIElement CreateAppTile(AppEntry app, int styleIndex)
        {
            var (g1, g2, accent) = AppStyles[styleIndex % AppStyles.Length];

            // ── Icon border ──────────────────────────────────────────────
            var iconBorder = new Border
            {
                Width = 82, Height = 82,
                CornerRadius = new CornerRadius(22),
                ClipToBounds = true,
                HorizontalAlignment = HorizontalAlignment.Center,
            };

            if (!string.IsNullOrEmpty(app.IconPath) && File.Exists(app.IconPath))
            {
                iconBorder.Background = new ImageBrush(
                    new BitmapImage(new Uri(app.IconPath))) { Stretch = Stretch.UniformToFill };
            }
            else
            {
                var grad = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0), EndPoint = new Point(1, 1)
                };
                grad.GradientStops.Add(new GradientStop(
                    (Color)ColorConverter.ConvertFromString(g1), 0));
                grad.GradientStops.Add(new GradientStop(
                    (Color)ColorConverter.ConvertFromString(g2), 1));
                iconBorder.Background = grad;
                iconBorder.Child = new TextBlock
                {
                    Text = string.IsNullOrEmpty(app.Abbrev) ? GetAbbrev(app.Name) : app.Abbrev,
                    FontSize = 30, FontWeight = FontWeights.Black,
                    Foreground = new SolidColorBrush(
                        (Color)ColorConverter.ConvertFromString(accent)),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment   = VerticalAlignment.Center
                };
            }

            // ── Text labels ───────────────────────────────────────────────
            var nameLabel = new TextBlock
            {
                Text            = app.Name,
                FontFamily      = new FontFamily("Inter, Segoe UI"),
                FontSize        = 12,
                FontWeight      = FontWeights.SemiBold,
                Foreground      = new SolidColorBrush(Color.FromRgb(0xF2, 0xF2, 0xF7)),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextTrimming    = TextTrimming.CharacterEllipsis,
                MaxWidth        = 155,
                Margin          = new Thickness(0, 11, 0, 0)
            };

            var subtitleText = string.IsNullOrEmpty(app.Description)
                ? Path.GetFileName(app.ExePath)
                : app.Description;
            var subtitle = new TextBlock
            {
                Text            = subtitleText,
                FontFamily      = new FontFamily("Inter, Segoe UI"),
                FontSize        = 10,
                Foreground      = new SolidColorBrush(Color.FromRgb(0x6E, 0x6E, 0x73)),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextTrimming    = TextTrimming.CharacterEllipsis,
                MaxWidth        = 155,
                Margin          = new Thickness(0, 3, 0, 0)
            };

            var stack = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center
            };
            stack.Children.Add(iconBorder);
            stack.Children.Add(nameLabel);
            stack.Children.Add(subtitle);

            // ── Outer container with hover scale ──────────────────────────
            var tile = new Border
            {
                Width  = 178, Height = 168,
                Margin = new Thickness(0, 0, 10, 10),
                Background = Brushes.Transparent,
                Cursor = Cursors.Hand,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform       = new ScaleTransform(1, 1),
                Child = stack
            };

            var st = (ScaleTransform)tile.RenderTransform;
            tile.MouseEnter += (_, _) =>
            {
                st.BeginAnimation(ScaleTransform.ScaleXProperty, Anim(1.1));
                st.BeginAnimation(ScaleTransform.ScaleYProperty, Anim(1.1));
            };
            tile.MouseLeave += (_, _) =>
            {
                st.BeginAnimation(ScaleTransform.ScaleXProperty, Anim(1.0));
                st.BeginAnimation(ScaleTransform.ScaleYProperty, Anim(1.0));
            };

            // Capture for lambda
            var capturedApp = app;
            tile.MouseLeftButtonUp += (_, _) => LaunchApp(capturedApp.ExePath, capturedApp.Name);

            return tile;
        }

        // ── "+" Add App tile ─────────────────────────────────────────────
        private UIElement CreateAddTile()
        {
            var dashRect = new System.Windows.Shapes.Rectangle
            {
                RadiusX         = 18, RadiusY = 18,
                Stroke          = new SolidColorBrush(Color.FromRgb(0x28, 0x28, 0x2C)),
                StrokeThickness = 1.5,
                StrokeDashArray = new DoubleCollection { 4, 3 },
                Fill            = new SolidColorBrush(Color.FromRgb(0x10, 0x10, 0x13))
            };

            var plusIcon = new TextBlock
            {
                Text       = "\uE710",
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize   = 28,
                Foreground = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3E)),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            var plusLabel = new TextBlock
            {
                Text       = "Add App",
                FontFamily = new FontFamily("Inter, Segoe UI"),
                FontSize   = 11, FontWeight = FontWeights.Medium,
                Foreground = new SolidColorBrush(Color.FromRgb(0x4A, 0x4A, 0x4E)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin     = new Thickness(0, 8, 0, 0)
            };

            var content = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center
            };
            content.Children.Add(plusIcon);
            content.Children.Add(plusLabel);

            var grid = new Grid
            {
                Width  = 178, Height = 168,
                Margin = new Thickness(0, 0, 10, 10),
                Cursor = Cursors.Hand
            };
            grid.Children.Add(dashRect);
            grid.Children.Add(content);

            // Hover: stroke and icons turn orange
            grid.MouseEnter += (_, _) =>
            {
                dashRect.Stroke = new SolidColorBrush(Color.FromRgb(0xFF, 0x95, 0x00));
                plusIcon.Foreground  = new SolidColorBrush(Color.FromRgb(0xFF, 0x95, 0x00));
                plusLabel.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x95, 0x00));
            };
            grid.MouseLeave += (_, _) =>
            {
                dashRect.Stroke = new SolidColorBrush(Color.FromRgb(0x28, 0x28, 0x2C));
                plusIcon.Foreground  = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3E));
                plusLabel.Foreground = new SolidColorBrush(Color.FromRgb(0x4A, 0x4A, 0x4E));
            };
            grid.MouseLeftButtonUp += OpenAddAppDialog;

            return grid;
        }

        // ═════════════════════════════════════════════════════════════════
        //  Settings tiles (dynamic, rebuilt on every navigate)
        // ═════════════════════════════════════════════════════════════════
        private void BuildSettingsTiles()
        {
            SettingsStackPanel.Children.Clear();

            // ── LAUNCHER ICON Section ──
            SettingsStackPanel.Children.Add(new TextBlock
            {
                Text       = "LAUNCHER ICON",
                FontFamily = new FontFamily("Inter, Segoe UI"),
                FontSize   = 10, FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3D)),
                Margin     = new Thickness(2, 0, 0, 14)
            });

            SettingsStackPanel.Children.Add(CreateLauncherSettingsRow());

            // Section header
            SettingsStackPanel.Children.Add(new TextBlock
            {
                Text       = "APP ICONS",
                FontFamily = new FontFamily("Inter, Segoe UI"),
                FontSize   = 10, FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3D)),
                Margin     = new Thickness(2, 30, 0, 14)
            });

            for (int i = 0; i < _apps.Count; i++)
                SettingsStackPanel.Children.Add(CreateSettingsRow(_apps[i], i));
        }

        private UIElement CreateLauncherSettingsRow()
        {
            var miniIcon = new Border
            {
                Width = 36, Height = 36,
                CornerRadius = new CornerRadius(9),
                ClipToBounds = true
            };

            if (!string.IsNullOrEmpty(_settings.LauncherIconPath) && File.Exists(_settings.LauncherIconPath))
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = new Uri(_settings.LauncherIconPath);
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                bmp.EndInit();
                miniIcon.Background = new ImageBrush(bmp) { Stretch = Stretch.UniformToFill };
            }
            else
            {
                var grad = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0), EndPoint = new Point(1, 1)
                };
                grad.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#FF3B30"), 0));
                grad.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#FF9500"), 1));
                miniIcon.Background = grad;
                miniIcon.Child = new TextBlock
                {
                    Text       = "A",
                    FontSize   = 15, FontWeight = FontWeights.Black,
                    Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment   = VerticalAlignment.Center
                };
            }

            var pathLabel = new TextBlock
            {
                Text       = string.IsNullOrEmpty(_settings.LauncherIconPath) ? "Default" : Path.GetFileName(_settings.LauncherIconPath),
                FontFamily = new FontFamily("Inter, Segoe UI"),
                FontSize   = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(0x6E, 0x6E, 0x73))
            };

            var nameStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
            nameStack.Children.Add(new TextBlock
            {
                Text       = "Main Logo",
                FontFamily = new FontFamily("Inter, Segoe UI"),
                FontSize   = 13, FontWeight = FontWeights.Medium,
                Foreground = new SolidColorBrush(Color.FromRgb(0xF2, 0xF2, 0xF7))
            });
            nameStack.Children.Add(pathLabel);

            var browseBtn = new Button { Content = "Browse", Style = (Style)FindResource("SettingsBrowseBtn") };
            var resetBtn = new Button { Content = "Reset", Style = (Style)FindResource("SettingsResetBtn"), Margin = new Thickness(6, 0, 0, 0) };

            browseBtn.Click += (_, _) =>
            {
                var dlg = new OpenFileDialog
                {
                    Title  = "Select launcher icon image",
                    Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp;*.ico;*.gif;*.webp|All Files|*.*"
                };
                if (dlg.ShowDialog() == true)
                {
                    _settings.LauncherIconPath = dlg.FileName;
                    SaveSettings();
                    RefreshLauncherLogo();
                    BuildSettingsTiles();
                }
            };
            resetBtn.Click += (_, _) =>
            {
                _settings.LauncherIconPath = "";
                SaveSettings();
                RefreshLauncherLogo();
                BuildSettingsTiles();
            };

            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            btnPanel.Children.Add(browseBtn);
            btnPanel.Children.Add(resetBtn);

            var rowGrid = new Grid();
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            Grid.SetColumn(miniIcon, 0);
            Grid.SetColumn(nameStack, 1);
            Grid.SetColumn(btnPanel, 2);

            rowGrid.Children.Add(miniIcon);
            rowGrid.Children.Add(nameStack);
            rowGrid.Children.Add(btnPanel);

            return new Border
            {
                Background    = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1D)),
                CornerRadius  = new CornerRadius(12),
                Padding       = new Thickness(14, 12, 14, 12),
                Margin        = new Thickness(0, 0, 0, 8),
                Child         = rowGrid
            };
        }

        private UIElement CreateSettingsRow(AppEntry app, int styleIndex)
        {
            var (g1, g2, accent) = AppStyles[styleIndex % AppStyles.Length];

            // ── Mini icon ────────────────────────────────────────────────
            var miniIcon = new Border
            {
                Width = 36, Height = 36,
                CornerRadius = new CornerRadius(9),
                ClipToBounds = true
            };

            if (!string.IsNullOrEmpty(app.IconPath) && File.Exists(app.IconPath))
            {
                miniIcon.Background = new ImageBrush(
                    new BitmapImage(new Uri(app.IconPath))) { Stretch = Stretch.UniformToFill };
            }
            else
            {
                var grad = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0), EndPoint = new Point(1, 1)
                };
                grad.GradientStops.Add(new GradientStop(
                    (Color)ColorConverter.ConvertFromString(g1), 0));
                grad.GradientStops.Add(new GradientStop(
                    (Color)ColorConverter.ConvertFromString(g2), 1));
                miniIcon.Background = grad;
                miniIcon.Child = new TextBlock
                {
                    Text       = string.IsNullOrEmpty(app.Abbrev) ? GetAbbrev(app.Name) : app.Abbrev,
                    FontSize   = 13, FontWeight = FontWeights.Black,
                    Foreground = new SolidColorBrush(
                        (Color)ColorConverter.ConvertFromString(accent)),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment   = VerticalAlignment.Center
                };
            }

            // ── Name + path label ────────────────────────────────────────
            var pathLabel = new TextBlock
            {
                Text       = string.IsNullOrEmpty(app.IconPath) ? "Default" : Path.GetFileName(app.IconPath),
                FontFamily = new FontFamily("Inter, Segoe UI"),
                FontSize   = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(0x6E, 0x6E, 0x73))
            };

            var nameStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
            nameStack.Children.Add(new TextBlock
            {
                Text       = app.Name,
                FontFamily = new FontFamily("Inter, Segoe UI"),
                FontSize   = 13, FontWeight = FontWeights.Medium,
                Foreground = new SolidColorBrush(Color.FromRgb(0xF2, 0xF2, 0xF7))
            });
            nameStack.Children.Add(pathLabel);

            // ── Buttons ──────────────────────────────────────────────────
            var browseBtn = new Button
            {
                Content = "Browse",
                Style   = (Style)FindResource("SettingsBrowseBtn")
            };
            var resetBtn = new Button
            {
                Content = "Reset",
                Style   = (Style)FindResource("SettingsResetBtn"),
                Margin  = new Thickness(6, 0, 0, 0)
            };
            var deleteBtn = new Button
            {
                Content = "Delete",
                Style   = (Style)FindResource("SettingsDeleteBtn"),
                Margin  = new Thickness(6, 0, 0, 0)
            };

            var capturedApp = app;
            browseBtn.Click += (_, _) =>
            {
                var dlg = new OpenFileDialog
                {
                    Title  = $"Select icon image for {capturedApp.Name}",
                    Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp;*.ico;*.gif;*.webp|All Files|*.*"
                };
                if (dlg.ShowDialog() == true)
                {
                    capturedApp.IconPath = dlg.FileName;
                    SaveApps();
                    BuildAppTiles();
                    BuildSettingsTiles();
                }
            };
            resetBtn.Click += (_, _) =>
            {
                capturedApp.IconPath = "";
                SaveApps();
                BuildAppTiles();
                BuildSettingsTiles();
            };
            deleteBtn.Click += (_, _) =>
            {
                if (MessageBox.Show(
                    $"Remove \"{capturedApp.Name}\" from the launcher?",
                    "Launcher", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    _apps.Remove(capturedApp);
                    SaveApps();
                    BuildAppTiles();
                    BuildSettingsTiles();
                }
            };

            // ── Row layout ───────────────────────────────────────────────
            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            btnPanel.Children.Add(browseBtn);
            btnPanel.Children.Add(resetBtn);
            btnPanel.Children.Add(deleteBtn);

            var rowGrid = new Grid();
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            Grid.SetColumn(miniIcon,  0);
            Grid.SetColumn(nameStack, 1);
            Grid.SetColumn(btnPanel,  2);

            rowGrid.Children.Add(miniIcon);
            rowGrid.Children.Add(nameStack);
            rowGrid.Children.Add(btnPanel);

            return new Border
            {
                Background    = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1D)),
                CornerRadius  = new CornerRadius(12),
                Padding       = new Thickness(14, 12, 14, 12),
                Margin        = new Thickness(0, 0, 0, 8),
                Child         = rowGrid
            };
        }

        // ═════════════════════════════════════════════════════════════════
        //  Add App dialog
        // ═════════════════════════════════════════════════════════════════
        private void OpenAddAppDialog(object sender, MouseButtonEventArgs e)
        {
            var dialog = new AddAppWindow { Owner = this };
            if (dialog.ShowDialog() == true && dialog.Result is not null)
            {
                _apps.Add(dialog.Result);
                SaveApps();
                BuildAppTiles();
            }
        }

        // ═════════════════════════════════════════════════════════════════
        //  Navigation
        // ═════════════════════════════════════════════════════════════════
        private void NavApps_Click(object sender, RoutedEventArgs e)     => ShowAppsView();
        private void NavProjects_Click(object sender, RoutedEventArgs e) => ShowProjectsView();
        private void NavSettings_Click(object sender, RoutedEventArgs e) => ShowSettingsView();

        private void ShowAppsView()
        {
            AppsView.Visibility     = Visibility.Visible;
            ProjectsView.Visibility = Visibility.Collapsed;
            SettingsView.Visibility = Visibility.Collapsed;
            PageTitle.Text = "Apps";
            SetNavActive(NavApps);
        }
        private void ShowProjectsView()
        {
            AppsView.Visibility     = Visibility.Collapsed;
            ProjectsView.Visibility = Visibility.Visible;
            SettingsView.Visibility = Visibility.Collapsed;
            PageTitle.Text = "Projects";
            SetNavActive(NavProjects);
        }
        private void ShowSettingsView()
        {
            AppsView.Visibility     = Visibility.Collapsed;
            ProjectsView.Visibility = Visibility.Collapsed;
            SettingsView.Visibility = Visibility.Visible;
            PageTitle.Text = "Settings";
            SetNavActive(NavSettings);
            BuildSettingsTiles(); // always rebuild so icon updates are reflected
        }

        private void SetNavActive(Button active)
        {
            NavApps.Tag     = NavApps     == active ? "active" : null;
            NavProjects.Tag = NavProjects == active ? "active" : null;
            NavSettings.Tag = NavSettings == active ? "active" : null;
        }

        // ═════════════════════════════════════════════════════════════════
        //  Window chrome
        // ═════════════════════════════════════════════════════════════════
        private void WindowDrag_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) DragMove();
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e)
            => WindowState = WindowState.Minimized;

        private void BtnClose_Click(object sender, RoutedEventArgs e) => ExitApp();

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            ExitApp();
        }

        private static void ExitApp()
        {
            Application.Current.Shutdown();
            Environment.Exit(0); // ensures no background process lingers
        }

        // ═════════════════════════════════════════════════════════════════
        //  App launch
        // ═════════════════════════════════════════════════════════════════
        private static void LaunchApp(string exePath, string displayName)
        {
            if (!File.Exists(exePath))
            {
                MessageBox.Show(
                    $"Could not find \"{displayName}\".\n\nExpected path:\n{exePath}",
                    "Launcher — Launch Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            try
            {
                Process.Start(new ProcessStartInfo { FileName = exePath, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to launch \"{displayName}\".\n\n{ex.Message}",
                    "Launcher — Launch Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // ═════════════════════════════════════════════════════════════════
        //  Helpers
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// Derives a 2-char abbreviation from an app name.
        /// "Sample Application" → "Sa", "Photoshop" → "Ph"
        /// </summary>
        private static string GetAbbrev(string name)
        {
            var words = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return words.Length >= 2
                ? $"{char.ToUpper(words[0][0])}{char.ToLower(words[1][0])}"
                : name.Length >= 2
                    ? $"{char.ToUpper(name[0])}{char.ToLower(name[1])}"
                    : name.ToUpper();
        }

        /// <summary>Short 140 ms ease-out animation used for the hover scale.</summary>
        private static DoubleAnimation Anim(double to)
            => new(to, TimeSpan.FromMilliseconds(140)) { EasingFunction = new QuadraticEase() };
    }
}
