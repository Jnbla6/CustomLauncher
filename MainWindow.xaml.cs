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
using System.Linq;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.Reflection;
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
        private static readonly string DataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CustomLauncher");
        private static readonly string AppsFile  = Path.Combine(DataDir, "apps.json");
        private static readonly string SettingsFile = Path.Combine(DataDir, "settings.json");

        // ─────────────────────────────────────────────────────────────────
        //  State
        // ─────────────────────────────────────────────────────────────────
        private AppDataRoot _appData = new();
        public ObservableCollection<CategoryModel> Categories { get; set; } = new();
        private CategoryModel? _activeCategory;
        public CategoryModel? ActiveCategory
        {
            get => _activeCategory;
            set
            {
                _activeCategory = value;
                if (PageTitle != null && value != null) PageTitle.Text = value.CategoryName;
                BuildAppTiles();
            }
        }
        private List<string> _watchedFolders = new();
        public ObservableCollection<ProjectModel> RecentProjects { get; set; } = new();

        public class GlobalSettings { 
            public string LauncherIconPath { get; set; } = ""; 
            public string ThemeColor { get; set; } = "#FF9500";
        }
        private GlobalSettings _settings = new();

        // ─────────────────────────────────────────────────────────────────
        //  Constructor
        // ─────────────────────────────────────────────────────────────────
        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            LoadSettings();
            LoadApps();
            BuildAppTiles();
            ShowAppsView();
            _ = LoadRecentProjectsAsync();
        }

        // ═════════════════════════════════════════════════════════════════
        //  Global Settings
        // ═════════════════════════════════════════════════════════════════
        private void LoadSettings()
        {
            try
            {
                if (!Directory.Exists(DataDir))
                {
                    Directory.CreateDirectory(DataDir);
                }

                if (File.Exists(SettingsFile))
                {
                    _settings = JsonSerializer.Deserialize<GlobalSettings>(File.ReadAllText(SettingsFile)) ?? new();
                }
            }
            catch { _settings = new(); }
            ApplyThemeColor(_settings.ThemeColor);
            RefreshLauncherLogo();
        }

        private void ApplyThemeColor(string hex)
        {
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(hex);
                Application.Current.Resources["AccentColor"] = color;
                Application.Current.Resources["AccentBrush"] = new SolidColorBrush(color);
            }
            catch { }
        }

        private void SaveSettings()
        {
            try
            {
                if (!Directory.Exists(DataDir))
                {
                    Directory.CreateDirectory(DataDir);
                }
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
                grad.GradientStops.Add(new GradientStop((Color)Application.Current.Resources["AccentColor"], 1));
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
                Id = Guid.NewGuid().ToString(),
                Name = "Drag & Drop",
                Abbrev = "Hi",
                Description = "Drag your .exe or .lnk here",
                ExePath = @""
            }
        };

        private void LoadApps()
        {
            try
            {
                if (!Directory.Exists(DataDir)) Directory.CreateDirectory(DataDir);

                if (!File.Exists(AppsFile))
                {
                    // Extract from embedded resource
                    var assembly = Assembly.GetExecutingAssembly();
                    var resourceName = "WhiteLabelLauncher.apps.json";
                    using (Stream? stream = assembly.GetManifestResourceStream(resourceName))
                    {
                        if (stream != null)
                        {
                            using (FileStream fileStream = new FileStream(AppsFile, FileMode.Create, FileAccess.Write))
                            {
                                stream.CopyTo(fileStream);
                            }
                        }
                    }
                }

                if (File.Exists(AppsFile))
                {
                    var json = File.ReadAllText(AppsFile);
                    try
                    {
                        var root = JsonSerializer.Deserialize<AppDataRoot>(json);
                        if (root != null && root.Categories != null && root.Categories.Count > 0)
                        {
                            _appData = root;
                            _watchedFolders = root.WatchedFolders ?? new();
                            SyncCategoriesToUI();
                            return;
                        }
                    }
                    catch { }

                    // Fallback for old format
                    var legacyApps = new List<AppEntry>();
                    try
                    {
                        // Some old jsons might be AppDataRoot with Apps
                        using var doc = JsonDocument.Parse(json);
                        if (doc.RootElement.TryGetProperty("Apps", out var appsProp))
                            legacyApps = JsonSerializer.Deserialize<List<AppEntry>>(appsProp.GetRawText()) ?? new();
                        else
                            legacyApps = JsonSerializer.Deserialize<List<AppEntry>>(json) ?? DefaultApps();
                    }
                    catch
                    {
                        legacyApps = DefaultApps();
                    }
                    
                    _appData = new AppDataRoot();
                    _appData.Categories.Add(new CategoryModel { CategoryName = "General", Apps = legacyApps });
                    _watchedFolders = new List<string> {
                        Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                    };
                    SyncCategoriesToUI();
                    SaveApps(); // Migrate to new format
                    return;
                }
            }
            catch { /* fall through to defaults */ }

            _appData = new AppDataRoot();
            _appData.Categories.Add(new CategoryModel { CategoryName = "General", Apps = DefaultApps() });
            _watchedFolders = new List<string> {
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            };
            SyncCategoriesToUI();
            SaveApps(); // persist defaults on first run
        }

        public void SyncCategoriesToUI()
        {
            Categories.Clear();
            foreach (var cat in _appData.Categories) Categories.Add(cat);
            if (Categories.Count > 0 && ActiveCategory == null)
            {
                ActiveCategory = Categories[0];
            }
        }

        public void SaveApps()
        {
            try
            {
                if (!Directory.Exists(DataDir)) Directory.CreateDirectory(DataDir);
                _appData.WatchedFolders = _watchedFolders;
                File.WriteAllText(AppsFile, JsonSerializer.Serialize(
                    _appData, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }

        private async Task LoadRecentProjectsAsync()
        {
            var projects = await Task.Run(() =>
            {
                var list = new List<ProjectModel>();
                var validExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".psd", ".ai", ".prproj", ".pdf" };

                if (_appData.ManualProjects != null)
                {
                    list.AddRange(_appData.ManualProjects);
                }

                foreach (var folder in _watchedFolders)
                {
                    if (!Directory.Exists(folder)) continue;

                    try
                    {
                        var files = Directory.EnumerateFiles(folder, "*.*", SearchOption.TopDirectoryOnly);
                        foreach (var file in files)
                        {
                            var ext = Path.GetExtension(file);
                            if (validExtensions.Contains(ext))
                            {
                                var fi = new FileInfo(file);
                                
                                string mappedAppName = "";
                                if (ext.Equals(".psd", StringComparison.OrdinalIgnoreCase)) mappedAppName = "photoshop";
                                else if (ext.Equals(".ai", StringComparison.OrdinalIgnoreCase)) mappedAppName = "illustrator";
                                else if (ext.Equals(".prproj", StringComparison.OrdinalIgnoreCase)) mappedAppName = "premiere";
                                else if (ext.Equals(".pdf", StringComparison.OrdinalIgnoreCase)) mappedAppName = "acrobat";

                                string foundAppId = "";
                                if (!string.IsNullOrEmpty(mappedAppName))
                                {
                                    foreach (var cat in _appData.Categories)
                                    {
                                        var app = cat.Apps.FirstOrDefault(a => a.Name.Contains(mappedAppName, StringComparison.OrdinalIgnoreCase) || a.ExePath.Contains(mappedAppName, StringComparison.OrdinalIgnoreCase));
                                        if (app != null)
                                        {
                                            foundAppId = app.Id;
                                            break;
                                        }
                                    }
                                }

                                if (!list.Any(p => p.FilePath.Equals(file, StringComparison.OrdinalIgnoreCase)))
                                {
                                    list.Add(new ProjectModel
                                    {
                                        FileName = Path.GetFileNameWithoutExtension(file),
                                        FilePath = file,
                                        Extension = ext,
                                        LastModified = fi.LastWriteTime,
                                        AppId = foundAppId
                                    });
                                }
                            }
                        }
                    }
                    catch { /* ignore access issues */ }
                }

                return list.OrderByDescending(p => p.LastModified).Take(15).ToList();
            });

            RecentProjects.Clear();
            foreach (var p in projects)
            {
                if (!string.IsNullOrEmpty(p.AppId))
                {
                    AppEntry? matchingApp = null;
                    foreach (var cat in _appData.Categories)
                    {
                        matchingApp = cat.Apps.FirstOrDefault(a => a.Id == p.AppId);
                        if (matchingApp != null) break;
                    }

                    if (matchingApp != null)
                    {
                        if (matchingApp.AppIcon == null && !string.IsNullOrEmpty(matchingApp.ExePath) && string.IsNullOrEmpty(matchingApp.IconPath))
                        {
                            matchingApp.AppIcon = await NativeIconHelper.GetHighResIconAsync(matchingApp.ExePath);
                        }

                        if (matchingApp.AppIcon != null)
                        {
                            p.DisplayIcon = matchingApp.AppIcon;
                        }
                        else if (!string.IsNullOrEmpty(matchingApp.IconPath) && File.Exists(matchingApp.IconPath))
                        {
                            p.DisplayIcon = new BitmapImage(new Uri(matchingApp.IconPath));
                        }
                    }
                }
                RecentProjects.Add(p);
            }

            if (ProjectsEmptyState != null)
            {
                ProjectsEmptyState.Visibility = RecentProjects.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
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
            if (AppsWrapPanel == null) return;
            AppsWrapPanel.Children.Clear();

            if (ActiveCategory != null)
            {
                for (int i = 0; i < ActiveCategory.Apps.Count; i++)
                    AppsWrapPanel.Children.Add(CreateAppTile(ActiveCategory.Apps[i], i));
            }

            AppsWrapPanel.Children.Add(CreateAddTile());
        }

        // ── Single app tile ──────────────────────────────────────────────
        private UIElement CreateAppTile(AppEntry app, int styleIndex)
        {
            var (g1, g2, accent) = AppStyles[styleIndex % AppStyles.Length];

            var iconBorder = new Border
            {
                Width = 82, Height = 82,
                CornerRadius = new CornerRadius(22),
                ClipToBounds = true,
                HorizontalAlignment = HorizontalAlignment.Center,
            };

            Action applyIcon = () =>
            {
                if (app.AppIcon != null)
                {
                    iconBorder.Background = Brushes.Transparent;
                    iconBorder.Child = new Image 
                    { 
                        Source = app.AppIcon,
                        Stretch = Stretch.Uniform,
                        Margin = new Thickness(12)
                    };
                }
                else if (!string.IsNullOrEmpty(app.IconPath) && File.Exists(app.IconPath))
                {
                    iconBorder.Background = new ImageBrush(
                        new BitmapImage(new Uri(app.IconPath))) { Stretch = Stretch.UniformToFill };
                    iconBorder.Child = null;
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
            };

            applyIcon();

            // Asynchronously fetch high-res icon if needed
            if (app.AppIcon == null && !string.IsNullOrEmpty(app.ExePath) && string.IsNullOrEmpty(app.IconPath))
            {
                _ = Task.Run(async () =>
                {
                    var icon = await NativeIconHelper.GetHighResIconAsync(app.ExePath);
                    if (icon != null)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            app.AppIcon = icon;
                            applyIcon();
                        });
                    }
                });
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

            // Context Menu for Edit & Deletion
            var ctx = new ContextMenu();
            var editMenuItem = new MenuItem { Header = "Edit App" };
            editMenuItem.Click += (_, _) =>
            {
                var dialog = new AddAppWindow { Owner = this };
                dialog.InitForEdit(capturedApp);
                if (dialog.ShowDialog() == true && dialog.Result != null)
                {
                    int idx = ActiveCategory?.Apps.IndexOf(capturedApp) ?? -1;
                    if (idx >= 0 && ActiveCategory != null)
                    {
                        ActiveCategory.Apps[idx] = dialog.Result;
                        SaveApps();
                        BuildAppTiles();
                    }
                }
            };
            ctx.Items.Add(editMenuItem);
            ctx.Items.Add(new Separator());

            var delMenuItem = new MenuItem { Header = "Delete App" };
            delMenuItem.Click += (_, _) =>
            {
                ActiveCategory?.Apps.Remove(capturedApp);
                SaveApps();
                BuildAppTiles();
            };
            ctx.Items.Add(delMenuItem);
            tile.ContextMenu = ctx;

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
                Text       = "Add app or drop it here",
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

            // Hover: stroke and icons turn to accent color
            grid.MouseEnter += (_, _) =>
            {
                var accent = (SolidColorBrush)Application.Current.Resources["AccentBrush"];
                dashRect.Stroke = accent;
                plusIcon.Foreground  = accent;
                plusLabel.Foreground = accent;
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

            // ── THEME COLOR Section ──
            SettingsStackPanel.Children.Add(new TextBlock
            {
                Text       = "THEME COLOR",
                FontFamily = new FontFamily("Inter, Segoe UI"),
                FontSize   = 10, FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3D)),
                Margin     = new Thickness(2, 30, 0, 14)
            });

            var colorsPanel = new WrapPanel { Orientation = Orientation.Horizontal };
            string[] swatches = { "#FF9500", "#31A8FF", "#50FA7B", "#FF79C6", "#BD93F9", "#FF3B30", "#F1FA8C" };
            foreach (var hex in swatches)
            {
                var btn = new Button
                {
                    Width = 32, Height = 32, Margin = new Thickness(0,0,12,12),
                    Cursor = Cursors.Hand,
                    Template = (ControlTemplate)FindResource("ColorSwatchBtnTemplate")
                };
                var brsh = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
                btn.Background = brsh;
                btn.Click += (_, _) => {
                    _settings.ThemeColor = hex;
                    SaveSettings();
                    ApplyThemeColor(hex);
                };
                colorsPanel.Children.Add(btn);
            }
            SettingsStackPanel.Children.Add(colorsPanel);

            // Section header
            SettingsStackPanel.Children.Add(new TextBlock
            {
                Text       = "APP ICONS",
                FontFamily = new FontFamily("Inter, Segoe UI"),
                FontSize   = 10, FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3D)),
                Margin     = new Thickness(2, 20, 0, 14)
            });

            for (int c = 0; c < _appData.Categories.Count; c++)
            {
                var cat = _appData.Categories[c];
                for (int i = 0; i < cat.Apps.Count; i++)
                    SettingsStackPanel.Children.Add(CreateSettingsRow(cat.Apps[i], cat, i));
            }
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
                grad.GradientStops.Add(new GradientStop((Color)Application.Current.Resources["AccentColor"], 1));
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

        private UIElement CreateSettingsRow(AppEntry app, CategoryModel parentCategory, int styleIndex)
        {
            var (g1, g2, accent) = AppStyles[styleIndex % AppStyles.Length];

            // ── Mini icon ────────────────────────────────────────────────
            var miniIcon = new Border
            {
                Width = 36, Height = 36,
                CornerRadius = new CornerRadius(9),
                ClipToBounds = true
            };

            if (app.AppIcon != null)
            {
                miniIcon.Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2E));
                miniIcon.Child = new Image { Source = app.AppIcon, Stretch = Stretch.Uniform, Margin = new Thickness(4) };
            }
            else if (!string.IsNullOrEmpty(app.IconPath) && File.Exists(app.IconPath))
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
                    parentCategory.Apps.Remove(capturedApp);
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
            if (ActiveCategory == null) return;
            var dialog = new AddAppWindow { Owner = this };
            if (dialog.ShowDialog() == true && dialog.Result is not null)
            {
                ActiveCategory.Apps.Add(dialog.Result);
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
            if (ActiveCategory != null) PageTitle.Text = ActiveCategory.CategoryName;
            // Unselect static nav items when viewing apps (since it's a dynamic category now)
            NavProjects.Tag = null;
            NavSettings.Tag = null;
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

        private void AppsView_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effects = DragDropEffects.Copy;
            else
                e.Effects = DragDropEffects.None;
        }

        private async void AppsView_Drop(object sender, DragEventArgs e)
        {
            if (ActiveCategory == null) return;
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                bool added = false;
                foreach (var file in files)
                {
                    string ext = Path.GetExtension(file).ToLower();
                    if (ext == ".exe" || ext == ".lnk")
                    {
                        var entry = new AppEntry
                        {
                            Id = Guid.NewGuid().ToString(),
                            Name = Path.GetFileNameWithoutExtension(file),
                            ExePath = file
                        };
                        
                        entry.AppIcon = await NativeIconHelper.GetHighResIconAsync(file);

                        ActiveCategory.Apps.Add(entry);
                        added = true;
                    }
                }
                if (added)
                {
                    SaveApps();
                    BuildAppTiles();
                }
            }
        }
        
        private void CategoryTab_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is CategoryModel cat)
            {
                ActiveCategory = cat;
                ShowAppsView();
            }
        }

        private void EditCategory_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is CategoryModel cat)
            {
                var dialog = new AddTabWindow { Owner = this };
                dialog.InitForEdit(cat);
                if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.ResultName))
                {
                    cat.CategoryName = dialog.ResultName;
                    cat.IconCode = dialog.ResultIconCode;
                    SaveApps();
                    SyncCategoriesToUI();
                }
            }
        }

        private void DeleteCategory_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is CategoryModel cat)
            {
                if (_appData.Categories.Count <= 1)
                {
                    MessageBox.Show("Cannot delete the last category.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                var result = MessageBox.Show($"Are you sure you want to delete the '{cat.CategoryName}' category?", "Delete Category", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    _appData.Categories.Remove(cat);
                    SaveApps();
                    SyncCategoriesToUI();
                    ActiveCategory = _appData.Categories.FirstOrDefault();
                }
            }
        }

        private void OpenAddTabDialog_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new AddTabWindow { Owner = this };
            if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.ResultName))
            {
                var newCat = new CategoryModel 
                { 
                    CategoryName = dialog.ResultName,
                    IconCode = dialog.ResultIconCode
                };
                _appData.Categories.Add(newCat);
                SaveApps();
                SyncCategoriesToUI();
                ActiveCategory = newCat;
            }
        }

        private void OpenAddProjectDialog_Click(object sender, MouseButtonEventArgs e)
        {
            var allApps = new List<AppEntry>();
            foreach (var cat in _appData.Categories)
            {
                allApps.AddRange(cat.Apps);
            }

            var dialog = new AddProjectWindow(allApps) { Owner = this };
            if (dialog.ShowDialog() == true && dialog.Result != null)
            {
                if (_appData.ManualProjects == null) _appData.ManualProjects = new List<ProjectModel>();
                _appData.ManualProjects.Add(dialog.Result);
                SaveApps();
                _ = LoadRecentProjectsAsync();
            }
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
        private void ProjectCard_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is ProjectModel project)
            {
                if (!File.Exists(project.FilePath))
                {
                    MessageBox.Show(
                        $"Could not find \"{project.FileName}\".\n\nExpected path:\n{project.FilePath}",
                        "Launcher — Launch Error",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                try
                {
                    Process.Start(new ProcessStartInfo { FileName = project.FilePath, UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"Failed to launch \"{project.FileName}\".\n\n{ex.Message}",
                        "Launcher — Launch Error",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }
    }
}
