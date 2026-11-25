using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.ServiceProcess;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;
using WinForms = System.Windows.Forms;

namespace SolsticeCommand
{
    public class SolsticeWindow : Window
    {
        private TextBlock lblApacheState, lblMysqlState, lblMariaState, lblFooter, lblProjCount, lblStackType;
        private Border badgeApache, badgeMysql, badgeMaria;
        private Button btnStart, btnStop, btnRestart, btnOpenWeb, btnOpenPma, btnDeployUi, btnEnvCheck, btnTunnel, btnCopyUrl;
        private TextBox txtSearch;
        private StackPanel projectListPanel;
        private DispatcherTimer pollTimer;

        private WinForms.NotifyIcon trayIcon;
        private System.Drawing.Icon icoOnline, icoPartial, icoOffline;
        private bool isQuitting = false;
        private List<string> allProjects = new List<string>();
        private List<string> recentProjects = new List<string>();
        private Process tunnelProc = null;
        private string activeTunnelUrl = "";

        // Auto-detected Stack State (WAMP or XAMPP)
        private string stackMode = "WAMP"; // "WAMP" or "XAMPP" or "NONE"
        private string stackRoot = @"C:\wamp64";
        private string webRoot = @"C:\wamp64\www";
        private string baseUrl = "http://localhost/";

        // Obsidian & Midnight Electric Blue Palette
        private static System.Windows.Media.Color C(byte r, byte g, byte b) { return System.Windows.Media.Color.FromRgb(r, g, b); }
        private static System.Windows.Media.Color CA(byte a, byte r, byte g, byte b) { return System.Windows.Media.Color.FromArgb(a, r, g, b); }

        private SolidColorBrush BrushObsidian = new SolidColorBrush(C(5, 8, 17));
        private SolidColorBrush BrushSurface = new SolidColorBrush(C(10, 16, 31));
        private SolidColorBrush BrushSurfaceHover = new SolidColorBrush(C(16, 26, 50));
        private SolidColorBrush BrushBorder = new SolidColorBrush(C(26, 41, 74));
        private SolidColorBrush BrushBlue = new SolidColorBrush(C(37, 99, 235));
        private SolidColorBrush BrushCyan = new SolidColorBrush(C(56, 189, 248));
        private SolidColorBrush BrushText = new SolidColorBrush(C(241, 245, 249));
        private SolidColorBrush BrushMuted = new SolidColorBrush(C(100, 116, 139));
        private SolidColorBrush BrushGreen = new SolidColorBrush(C(74, 222, 128));
        private SolidColorBrush BrushGreenBg = new SolidColorBrush(CA(28, 34, 197, 94));
        private SolidColorBrush BrushOffBg = new SolidColorBrush(CA(35, 71, 85, 105));

        public SolsticeWindow()
        {
            Title = "SOLSTICE GRID // Local Server";
            Width = 780;
            Height = 525;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = System.Windows.Media.Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            DetectStack();

            try
            {
                string icoFile = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "solstice.ico");
                if (File.Exists(icoFile))
                    Icon = System.Windows.Media.Imaging.BitmapFrame.Create(new Uri(icoFile));
            }
            catch { }

            InitTray();
            Content = BuildRootUI();
            LoadProjects();
            RefreshStatuses();

            pollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1500) };
            pollTimer.Tick += (s, e) => RefreshStatuses();
            pollTimer.Start();

            Closing += (s, e) =>
            {
                if (!isQuitting)
                {
                    e.Cancel = true;
                    Hide();
                }
            };
        }

        private void DetectStack()
        {
            string[] wampPaths = new string[] { @"C:\wamp64", @"D:\wamp64", @"C:\wamp", @"D:\wamp" };
            foreach (var p in wampPaths)
            {
                if (Directory.Exists(System.IO.Path.Combine(p, "www")))
                {
                    stackMode = "WAMP";
                    stackRoot = p;
                    webRoot = System.IO.Path.Combine(p, "www");
                    break;
                }
            }
            if (stackMode != "WAMP")
            {
                string[] xamppPaths = new string[] { @"C:\xampp", @"D:\xampp" };
                foreach (var p in xamppPaths)
                {
                    if (Directory.Exists(System.IO.Path.Combine(p, "htdocs")))
                    {
                        stackMode = "XAMPP";
                        stackRoot = p;
                        webRoot = System.IO.Path.Combine(p, "htdocs");
                        break;
                    }
                }
            }
            if (!Directory.Exists(webRoot))
            {
                stackMode = "NONE";
            }

            try
            {
                string hostsFile = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"drivers\etc\hosts");
                if (File.Exists(hostsFile) && File.ReadAllText(hostsFile).Contains("solstice.local"))
                    baseUrl = "http://solstice.local/";
                else
                    baseUrl = "http://localhost/";
            }
            catch { baseUrl = "http://localhost/"; }
        }

        private System.Drawing.Icon CreateEclipseIcon(System.Drawing.Color ringColor)
        {
            var bmp = new Bitmap(32, 32);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.Clear(System.Drawing.Color.FromArgb(5, 8, 17));
                string logoFile = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "solstice_logo.png");
                if (File.Exists(logoFile))
                {
                    using (var src = new Bitmap(logoFile))
                    using (var scaled = new Bitmap(src, 28, 28))
                    {
                        for (int y = 0; y < 28; y++)
                        {
                            for (int x = 0; x < 28; x++)
                            {
                                var p = scaled.GetPixel(x, y);
                                if (p.A > 20)
                                    bmp.SetPixel(x + 2, y + 2, System.Drawing.Color.FromArgb(p.A, ringColor.R, ringColor.G, ringColor.B));
                            }
                        }
                    }
                    return System.Drawing.Icon.FromHandle(bmp.GetHicon());
                }
                using (var pen = new System.Drawing.Pen(ringColor, 3f))
                    g.DrawEllipse(pen, 6, 6, 20, 20);
            }
            return System.Drawing.Icon.FromHandle(bmp.GetHicon());
        }

        private void InitTray()
        {
            icoOnline = CreateEclipseIcon(System.Drawing.Color.FromArgb(56, 189, 248));
            icoPartial = CreateEclipseIcon(System.Drawing.Color.FromArgb(245, 158, 11));
            icoOffline = CreateEclipseIcon(System.Drawing.Color.FromArgb(239, 68, 68));

            var menu = new WinForms.ContextMenuStrip();
            menu.Items.Add("Open Solstice Grid", null, (s, e) => RestoreWindow());
            menu.Items.Add("-");
            menu.Items.Add("Initialize Stack (Start)", null, async (s, e) => await RunServiceAction("start"));
            menu.Items.Add("Terminate Stack (Stop)", null, async (s, e) => await RunServiceAction("stop"));
            menu.Items.Add("-");
            menu.Items.Add("Open Dashboard", null, (s, e) => OpenUrl(baseUrl));
            menu.Items.Add("Open phpMyAdmin", null, (s, e) => OpenUrl(baseUrl + "phpmyadmin/"));
            menu.Items.Add("-");
            menu.Items.Add("Quit Solstice Grid", null, (s, e) =>
            {
                StopTunnel();
                isQuitting = true;
                trayIcon.Visible = false;
                trayIcon.Dispose();
                Application.Current.Shutdown();
            });

            trayIcon = new WinForms.NotifyIcon
            {
                Icon = icoOffline,
                Text = "SOLSTICE GRID // Local Server",
                Visible = true,
                ContextMenuStrip = menu
            };
            trayIcon.MouseClick += (s, e) =>
            {
                if (e.Button == WinForms.MouseButtons.Left)
                {
                    if (IsVisible) Hide(); else RestoreWindow();
                }
            };
        }

        private void RestoreWindow()
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        }

        private UIElement BuildRootUI()
        {
            var outer = new Border
            {
                CornerRadius = new CornerRadius(14),
                BorderThickness = new Thickness(1),
                BorderBrush = BrushBorder,
                Background = new System.Windows.Media.LinearGradientBrush(
                    C(5, 8, 17), C(8, 14, 29), new System.Windows.Point(0, 0), new System.Windows.Point(1, 1)),
                Margin = new Thickness(10),
                Effect = new DropShadowEffect
                {
                    Color = C(0, 0, 0),
                    BlurRadius = 18,
                    ShadowDepth = 0,
                    Opacity = 0.7
                }
            };

            var rootGrid = new Grid();
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(46) });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // Top Draggable Window Bar
            var topBar = new Grid { Background = System.Windows.Media.Brushes.Transparent };
            topBar.MouseLeftButtonDown += (s, e) => { if (e.ButtonState == MouseButtonState.Pressed) DragMove(); };

            lblStackType = new TextBlock
            {
                Text = string.Format("SOLSTICE GRID  //  {0} ({1})", stackMode, stackRoot),
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Foreground = BrushCyan
            };
            var pillBadge = new Border
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(22, 0, 0, 0),
                Padding = new Thickness(9, 3, 9, 3),
                CornerRadius = new CornerRadius(4),
                Background = new SolidColorBrush(CA(24, 56, 189, 248)),
                BorderBrush = new SolidColorBrush(CA(70, 56, 189, 248)),
                BorderThickness = new Thickness(1),
                Child = lblStackType
            };

            var winControls = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 14, 0)
            };
            var btnMin = MakeWindowBtn("—", false);
            var btnClose = MakeWindowBtn("×", true);
            btnMin.Click += (s, e) => { Hide(); };
            btnClose.Click += (s, e) => { Hide(); };
            winControls.Children.Add(btnMin);
            winControls.Children.Add(btnClose);

            topBar.Children.Add(pillBadge);
            topBar.Children.Add(winControls);
            Grid.SetRow(topBar, 0);
            rootGrid.Children.Add(topBar);

            // Main 2-Column Content
            var bodyGrid = new Grid { Margin = new Thickness(22, 2, 22, 20) };
            bodyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(370) });
            bodyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) });
            bodyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var leftCol = BuildLeftColumn();
            var rightCol = BuildRightColumn();
            Grid.SetColumn(leftCol, 0);
            Grid.SetColumn(rightCol, 2);
            bodyGrid.Children.Add(leftCol);
            bodyGrid.Children.Add(rightCol);

            Grid.SetRow(bodyGrid, 1);
            rootGrid.Children.Add(bodyGrid);

            outer.Child = rootGrid;
            return outer;
        }

        private UIElement BuildLeftColumn()
        {
            var stack = new StackPanel();

            var logoRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 16) };
            logoRow.Children.Add(BuildEclipseCrest());
            logoRow.Children.Add(BuildSolsticeWordmark());
            stack.Children.Add(logoRow);

            var card = new Border
            {
                Background = BrushSurface,
                BorderBrush = BrushBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(14, 12, 14, 12),
                Margin = new Thickness(0, 0, 0, 12)
            };
            var cardStack = new StackPanel();
            string aLabel = stackMode == "XAMPP" ? "Apache2.4 / httpd.exe" : "wampapache64";
            string mLabel = stackMode == "XAMPP" ? "mysql / mysqld.exe" : "wampmysqld64";
            string dLabel = stackMode == "XAMPP" ? "N/A (Uses MySQL)" : "wampmariadb64";

            cardStack.Children.Add(MakeServiceRow("APACHE ENGINE", aLabel, out lblApacheState, out badgeApache, false));
            cardStack.Children.Add(MakeServiceRow("MYSQL SERVER", mLabel, out lblMysqlState, out badgeMysql, false));
            cardStack.Children.Add(MakeServiceRow("MARIADB SERVER", dLabel, out lblMariaState, out badgeMaria, true));
            card.Child = cardStack;
            stack.Children.Add(card);

            // Row 1: Start / Stop
            var row1 = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            row1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            row1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            btnStart = MakeActionBtn("INITIALIZE STACK", true);
            btnStop = MakeActionBtn("TERMINATE ALL", false);
            btnStart.Click += async (s, e) => await RunServiceAction("start");
            btnStop.Click += async (s, e) => await RunServiceAction("stop");
            Grid.SetColumn(btnStart, 0);
            Grid.SetColumn(btnStop, 2);
            row1.Children.Add(btnStart);
            row1.Children.Add(btnStop);
            stack.Children.Add(row1);

            // Row 2: Restart / Dashboard / phpMyAdmin
            var row2 = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            row2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
            row2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            row2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            row2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(108) });

            btnRestart = MakeActionBtn("RESTART", false);
            btnOpenWeb = MakeActionBtn(baseUrl.Contains("solstice.local") ? "SOLSTICE.LOCAL" : "LOCALHOST", true);
            btnOpenPma = MakeActionBtn("PHPMYADMIN", false);
            btnRestart.Click += async (s, e) => await RunServiceAction("restart");
            btnOpenWeb.Click += (s, e) => OpenUrl(baseUrl);
            btnOpenPma.Click += (s, e) => OpenUrl(baseUrl + "phpmyadmin/");

            Grid.SetColumn(btnRestart, 0);
            Grid.SetColumn(btnOpenWeb, 2);
            Grid.SetColumn(btnOpenPma, 4);
            row2.Children.Add(btnRestart);
            row2.Children.Add(btnOpenWeb);
            row2.Children.Add(btnOpenPma);
            stack.Children.Add(row2);

            // Row 3: Portable Toolkit (Deploy Web UI to Friend's WAMP/XAMPP + Env Check)
            var row3 = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            row3.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row3.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            row3.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });

            btnDeployUi = MakeActionBtn("DEPLOY SOLSTICE WEB UI", false);
            btnEnvCheck = MakeActionBtn("CHECK ENV", false);
            btnDeployUi.Click += (s, e) => DeployDashboardToWebRoot();
            btnEnvCheck.Click += async (s, e) => await RunEnvCheck();
            Grid.SetColumn(btnDeployUi, 0);
            Grid.SetColumn(btnEnvCheck, 2);
            row3.Children.Add(btnDeployUi);
            row3.Children.Add(btnEnvCheck);
            stack.Children.Add(row3);

            // Row 4: Cloudflare Quick Tunnel
            var row4 = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            row4.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row4.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            row4.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });

            btnTunnel = MakeActionBtn("CF TUNNEL: OFF", false);
            btnCopyUrl = MakeActionBtn("COPY URL", false);
            btnCopyUrl.IsEnabled = false;
            btnTunnel.Click += async (s, e) => await ToggleTunnel();
            btnCopyUrl.Click += (s, e) =>
            {
                if (!string.IsNullOrEmpty(activeTunnelUrl))
                {
                    Clipboard.SetText(activeTunnelUrl);
                    lblFooter.Foreground = BrushGreen;
                    lblFooter.Text = "COPIED // " + activeTunnelUrl;
                }
            };
            Grid.SetColumn(btnTunnel, 0);
            Grid.SetColumn(btnCopyUrl, 2);
            row4.Children.Add(btnTunnel);
            row4.Children.Add(btnCopyUrl);
            stack.Children.Add(row4);

            lblFooter = new TextBlock
            {
                Text = "STANDBY  //  PORTABLE SYMBIOTE READY",
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                FontSize = 10,
                Foreground = BrushMuted,
                TextWrapping = TextWrapping.Wrap
            };
            stack.Children.Add(lblFooter);

            return stack;
        }

        private UIElement BuildEclipseCrest()
        {
            string logoFile = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "solstice_logo.png");
            if (File.Exists(logoFile))
            {
                var bi = new System.Windows.Media.Imaging.BitmapImage(new Uri(logoFile));
                return new System.Windows.Controls.Image
                {
                    Source = bi,
                    Width = 44,
                    Height = 44,
                    Margin = new Thickness(0, 0, 12, 0),
                    Stretch = Stretch.Uniform
                };
            }
            return new Canvas { Width = 38, Height = 38, Margin = new Thickness(0, 0, 12, 0) };
        }

        private UIElement BuildSolsticeWordmark()
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            string wmFile = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "solstice_wordmark.png");
            if (File.Exists(wmFile))
            {
                var bi = new System.Windows.Media.Imaging.BitmapImage(new Uri(wmFile));
                row.Children.Add(new System.Windows.Controls.Image
                {
                    Source = bi,
                    Height = 36,
                    Stretch = Stretch.Uniform,
                    VerticalAlignment = VerticalAlignment.Center
                });
            }
            var gridBadge = new Border
            {
                Margin = new Thickness(10, 0, 0, 10),
                Padding = new Thickness(7, 2, 7, 2),
                CornerRadius = new CornerRadius(4),
                VerticalAlignment = VerticalAlignment.Center,
                Background = new SolidColorBrush(CA(28, 56, 189, 248)),
                BorderBrush = new SolidColorBrush(CA(110, 56, 189, 248)),
                BorderThickness = new Thickness(1),
                Child = new TextBlock
                {
                    Text = "GRID",
                    FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                    FontSize = 10.5,
                    FontWeight = FontWeights.Bold,
                    Foreground = BrushCyan
                }
            };
            row.Children.Add(gridBadge);
            return row;
        }

        private UIElement MakeServiceRow(string title, string svc, out TextBlock stateLbl, out Border badge, bool isLast)
        {
            var row = new Grid { Margin = isLast ? new Thickness(0) : new Thickness(0, 0, 0, 10) };
            var left = new StackPanel();
            left.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 11.5,
                FontWeight = FontWeights.Bold,
                Foreground = BrushText
            });
            left.Children.Add(new TextBlock
            {
                Text = svc,
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                FontSize = 9.5,
                Foreground = BrushMuted
            });

            stateLbl = new TextBlock
            {
                Text = "CHECKING",
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Foreground = BrushMuted
            };
            badge = new Border
            {
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Padding = new Thickness(9, 4, 9, 4),
                CornerRadius = new CornerRadius(5),
                Background = BrushOffBg,
                Child = stateLbl
            };

            row.Children.Add(left);
            row.Children.Add(badge);
            return row;
        }

        private UIElement BuildRightColumn()
        {
            var outer = new Border
            {
                Background = BrushSurface,
                BorderBrush = BrushBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(14)
            };

            var g = new Grid();
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var head = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            head.Children.Add(new TextBlock
            {
                Text = stackMode == "XAMPP" ? "PROJECTS  //  HTDOCS" : "PROJECTS  //  WWW",
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = BrushCyan,
                VerticalAlignment = VerticalAlignment.Center
            });

            var rightHead = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            lblProjCount = new TextBlock
            {
                Text = "0 APPS",
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                FontSize = 10,
                Foreground = BrushMuted,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            var btnRefresh = new Button
            {
                Content = "↻",
                Width = 24,
                Height = 22,
                Background = BrushObsidian,
                Foreground = BrushCyan,
                BorderBrush = BrushBorder,
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                FontSize = 12,
                Padding = new Thickness(0)
            };
            btnRefresh.Click += (s, e) => LoadProjects();
            rightHead.Children.Add(lblProjCount);
            rightHead.Children.Add(btnRefresh);
            head.Children.Add(rightHead);
            Grid.SetRow(head, 0);
            g.Children.Add(head);

            var searchBorder = new Border
            {
                Background = BrushObsidian,
                BorderBrush = BrushBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(8, 5, 8, 5),
                Margin = new Thickness(0, 0, 0, 10)
            };
            txtSearch = new TextBox
            {
                Background = System.Windows.Media.Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = BrushText,
                CaretBrush = BrushCyan,
                FontSize = 12
            };
            txtSearch.TextChanged += (s, e) => RenderProjectList(txtSearch.Text);
            searchBorder.Child = txtSearch;
            Grid.SetRow(searchBorder, 1);
            g.Children.Add(searchBorder);

            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            projectListPanel = new StackPanel();
            scroll.Content = projectListPanel;
            Grid.SetRow(scroll, 2);
            g.Children.Add(scroll);

            outer.Child = g;
            return outer;
        }

        private void LoadProjects()
        {
            allProjects.Clear();
            recentProjects.Clear();
            if (Directory.Exists(webRoot))
            {
                var dirInfos = new List<DirectoryInfo>();
                foreach (var dir in Directory.GetDirectories(webRoot))
                {
                    string name = System.IO.Path.GetFileName(dir);
                    if (name.Equals("wamplangues", StringComparison.OrdinalIgnoreCase) ||
                        name.Equals("wampthemes", StringComparison.OrdinalIgnoreCase) ||
                        name.Equals("dashboard", StringComparison.OrdinalIgnoreCase) ||
                        name.Equals("img", StringComparison.OrdinalIgnoreCase) ||
                        name.Equals("webalizer", StringComparison.OrdinalIgnoreCase))
                        continue;
                    allProjects.Add(name);
                    dirInfos.Add(new DirectoryInfo(dir));
                }
                allProjects.Sort(StringComparer.OrdinalIgnoreCase);

                string recentFile = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "recent.txt");
                if (File.Exists(recentFile))
                {
                    foreach (var line in File.ReadAllLines(recentFile))
                    {
                        string r = line.Trim();
                        if (r.Length > 0 && allProjects.Contains(r) && !recentProjects.Contains(r))
                            recentProjects.Add(r);
                    }
                }
                if (recentProjects.Count == 0)
                {
                    dirInfos.Sort((a, b) => b.LastWriteTime.CompareTo(a.LastWriteTime));
                    for (int i = 0; i < Math.Min(4, dirInfos.Count); i++)
                        recentProjects.Add(dirInfos[i].Name);
                }
            }
            RenderProjectList(txtSearch != null ? txtSearch.Text : "");
        }

        private void RecordRecent(string projName)
        {
            try
            {
                recentProjects.RemoveAll(x => x.Equals(projName, StringComparison.OrdinalIgnoreCase));
                recentProjects.Insert(0, projName);
                if (recentProjects.Count > 4) recentProjects.RemoveRange(4, recentProjects.Count - 4);
                string recentFile = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "recent.txt");
                File.WriteAllLines(recentFile, recentProjects.ToArray());
                RenderProjectList(txtSearch != null ? txtSearch.Text : "");
            }
            catch { }
        }

        private void AddProjectButton(string projName, bool isRecent)
        {
            var btn = new Button
            {
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Background = BrushObsidian,
                BorderBrush = isRecent ? BrushBlue : BrushBorder,
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 0, 6),
                Padding = new Thickness(10, 7, 10, 7),
                Cursor = Cursors.Hand
            };
            btn.Style = MakeFlatButtonStyle(BrushObsidian, BrushSurfaceHover, isRecent ? BrushBlue : BrushBorder, BrushCyan);

            var row = new Grid();
            row.Children.Add(new TextBlock
            {
                Text = projName,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = BrushText,
                VerticalAlignment = VerticalAlignment.Center
            });
            row.Children.Add(new TextBlock
            {
                Text = isRecent ? "RECENT ↗" : "OPEN ↗",
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                FontSize = 9.5,
                FontWeight = FontWeights.Bold,
                Foreground = BrushCyan,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            });
            btn.Content = row;
            btn.Click += (s, e) =>
            {
                RecordRecent(projName);
                OpenUrl(baseUrl + Uri.EscapeDataString(projName) + "/");
            };
            projectListPanel.Children.Add(btn);
        }

        private void RenderProjectList(string filter)
        {
            projectListPanel.Children.Clear();
            string q = (filter ?? "").Trim().ToLowerInvariant();
            int shown = 0;

            if (q.Length == 0 && recentProjects.Count > 0)
            {
                projectListPanel.Children.Add(new TextBlock
                {
                    Text = "RECENT",
                    FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                    FontSize = 9.5,
                    FontWeight = FontWeights.Bold,
                    Foreground = BrushCyan,
                    Margin = new Thickness(0, 0, 0, 5)
                });
                foreach (var r in recentProjects)
                    AddProjectButton(r, true);

                projectListPanel.Children.Add(new TextBlock
                {
                    Text = "ALL WORKSPACES",
                    FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                    FontSize = 9.5,
                    FontWeight = FontWeights.Bold,
                    Foreground = BrushMuted,
                    Margin = new Thickness(0, 6, 0, 5)
                });
            }

            foreach (var name in allProjects)
            {
                if (q.Length > 0 && !name.ToLowerInvariant().Contains(q)) continue;
                shown++;
                AddProjectButton(name, false);
            }
            lblProjCount.Text = string.Format("{0} APPS", shown);
        }

        private Button MakeWindowBtn(string text, bool isClose)
        {
            var btn = new Button
            {
                Content = text,
                Width = 28,
                Height = 24,
                Margin = new Thickness(4, 0, 0, 0),
                Foreground = BrushMuted,
                Background = System.Windows.Media.Brushes.Transparent,
                BorderThickness = new Thickness(0),
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Cursor = Cursors.Hand
            };
            btn.Style = MakeFlatButtonStyle(
                System.Windows.Media.Brushes.Transparent,
                isClose ? new SolidColorBrush(CA(50, 239, 68, 68)) : BrushSurfaceHover,
                System.Windows.Media.Brushes.Transparent,
                System.Windows.Media.Brushes.Transparent);
            return btn;
        }

        private Button MakeActionBtn(string text, bool primary)
        {
            var normalBg = primary ? new SolidColorBrush(C(17, 36, 76)) : BrushSurface;
            var hoverBg = primary ? new SolidColorBrush(C(25, 52, 108)) : BrushSurfaceHover;
            var border = primary ? BrushBlue : BrushBorder;
            return new Button
            {
                Content = text,
                Height = 34,
                Foreground = primary ? BrushCyan : BrushText,
                FontSize = 10.5,
                FontWeight = FontWeights.Bold,
                Cursor = Cursors.Hand,
                BorderThickness = new Thickness(1),
                Style = MakeFlatButtonStyle(normalBg, hoverBg, border, BrushCyan)
            };
        }

        private Style MakeFlatButtonStyle(System.Windows.Media.Brush bg, System.Windows.Media.Brush hoverBg, System.Windows.Media.Brush border, System.Windows.Media.Brush hoverBorder)
        {
            var style = new Style(typeof(Button));
            var tpl = new ControlTemplate(typeof(Button));
            var bd = new FrameworkElementFactory(typeof(Border));
            bd.Name = "bd";
            bd.SetValue(Border.BackgroundProperty, bg);
            bd.SetValue(Border.BorderBrushProperty, border);
            bd.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            bd.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
            bd.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));

            var cp = new FrameworkElementFactory(typeof(ContentPresenter));
            cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, new TemplateBindingExtension(Control.HorizontalContentAlignmentProperty));
            cp.SetValue(ContentPresenter.VerticalAlignmentProperty, new TemplateBindingExtension(Control.VerticalContentAlignmentProperty));
            bd.AppendChild(cp);

            tpl.VisualTree = bd;

            var hoverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hoverTrigger.Setters.Add(new Setter(Border.BackgroundProperty, hoverBg, "bd"));
            hoverTrigger.Setters.Add(new Setter(Border.BorderBrushProperty, hoverBorder, "bd"));
            tpl.Triggers.Add(hoverTrigger);

            style.Setters.Add(new Setter(Control.TemplateProperty, tpl));
            return style;
        }

        private bool IsServiceOrProcRunning(string svcName, string procFallback)
        {
            try
            {
                using (var sc = new ServiceController(svcName))
                    if (sc.Status == ServiceControllerStatus.Running) return true;
            }
            catch { }
            if (!string.IsNullOrEmpty(procFallback))
            {
                try { if (Process.GetProcessesByName(procFallback).Length > 0) return true; }
                catch { }
            }
            return false;
        }

        private void UpdateBadge(TextBlock lbl, Border badge, bool running)
        {
            if (running)
            {
                lbl.Text = "● ONLINE";
                lbl.Foreground = BrushGreen;
                badge.Background = BrushGreenBg;
            }
            else
            {
                lbl.Text = "○ OFFLINE";
                lbl.Foreground = BrushMuted;
                badge.Background = BrushOffBg;
            }
        }

        private void RefreshStatuses()
        {
            bool a = stackMode == "XAMPP"
                ? IsServiceOrProcRunning("Apache2.4", "httpd")
                : IsServiceOrProcRunning("wampapache64", "httpd");
            bool m = stackMode == "XAMPP"
                ? IsServiceOrProcRunning("mysql", "mysqld")
                : IsServiceOrProcRunning("wampmysqld64", null);
            bool d = stackMode == "XAMPP"
                ? false
                : IsServiceOrProcRunning("wampmariadb64", null);

            UpdateBadge(lblApacheState, badgeApache, a);
            UpdateBadge(lblMysqlState, badgeMysql, m);
            UpdateBadge(lblMariaState, badgeMaria, d);

            if (a && (m || d))
            {
                trayIcon.Icon = icoOnline;
                trayIcon.Text = "SOLSTICE GRID // ONLINE";
            }
            else if (a || m || d)
            {
                trayIcon.Icon = icoPartial;
                trayIcon.Text = "SOLSTICE GRID // PARTIAL";
            }
            else
            {
                trayIcon.Icon = icoOffline;
                trayIcon.Text = "SOLSTICE GRID // OFFLINE";
            }
        }

        private async Task RunServiceAction(string mode)
        {
            btnStart.IsEnabled = btnStop.IsEnabled = btnRestart.IsEnabled = false;
            lblFooter.Foreground = BrushCyan;
            lblFooter.Text = mode == "start" ? "INITIALIZING APACHE + MYSQL..." :
                             mode == "stop" ? "TERMINATING STACK..." : "RESTARTING STACK...";

            await Task.Run(() =>
            {
                if (stackMode == "WAMP")
                {
                    if (mode == "stop" || mode == "restart")
                    {
                        StopSvc("wampapache64");
                        StopSvc("wampmysqld64");
                        StopSvc("wampmariadb64");
                    }
                    if (mode == "start" || mode == "restart")
                    {
                        StartSvc("wampapache64");
                        StartSvc("wampmysqld64");
                    }
                }
                else if (stackMode == "XAMPP")
                {
                    if (mode == "stop" || mode == "restart")
                    {
                        StopSvc("Apache2.4");
                        StopSvc("mysql");
                        KillProc("httpd");
                        KillProc("mysqld");
                    }
                    if (mode == "start" || mode == "restart")
                    {
                        if (!StartSvc("Apache2.4"))
                            StartHidden(System.IO.Path.Combine(stackRoot, @"apache\bin\httpd.exe"), "");
                        if (!StartSvc("mysql"))
                            StartHidden(System.IO.Path.Combine(stackRoot, @"mysql\bin\mysqld.exe"), "--defaults-file=" + System.IO.Path.Combine(stackRoot, @"mysql\bin\my.ini"));
                    }
                }
            });

            RefreshStatuses();
            lblFooter.Foreground = mode == "stop" ? BrushMuted : BrushGreen;
            lblFooter.Text = mode == "stop" ? "STACK TERMINATED  //  STANDBY" : "STACK ONLINE  //  " + baseUrl;
            btnStart.IsEnabled = btnStop.IsEnabled = btnRestart.IsEnabled = true;
        }

        private void DeployDashboardToWebRoot()
        {
            if (!Directory.Exists(webRoot))
            {
                lblFooter.Foreground = BrushCyan;
                lblFooter.Text = "NO WAMP/XAMPP WEB ROOT FOUND!";
                return;
            }
            try
            {
                string appDir = AppDomain.CurrentDomain.BaseDirectory;
                string srcIdx = System.IO.Path.Combine(appDir, @"dashboard\index.php");
                string dstIdx = System.IO.Path.Combine(webRoot, "index.php");
                string bakIdx = System.IO.Path.Combine(webRoot, "index.php.orig_bak");

                if (File.Exists(dstIdx) && !File.Exists(bakIdx))
                    File.Copy(dstIdx, bakIdx, false);

                if (File.Exists(srcIdx)) File.Copy(srcIdx, dstIdx, true);
                foreach (var img in new string[] { "solstice_logo.png", "solstice_wordmark.png" })
                {
                    string s = System.IO.Path.Combine(appDir, img);
                    if (File.Exists(s)) File.Copy(s, System.IO.Path.Combine(webRoot, img), true);
                }

                lblFooter.Foreground = BrushGreen;
                lblFooter.Text = "SOLSTICE WEB UI DEPLOYED TO " + webRoot;
            }
            catch (Exception ex)
            {
                lblFooter.Foreground = BrushCyan;
                lblFooter.Text = "DEPLOY ERROR: " + ex.Message;
            }
        }

        private async Task RunEnvCheck()
        {
            lblFooter.Foreground = BrushCyan;
            lblFooter.Text = "SCANNING PHP / COMPOSER / PYTHON / GIT / NODE...";
            string report = await Task.Run(() =>
            {
                var found = new List<string>();
                if (CheckCmd("php", "-v")) found.Add("PHP:OK"); else found.Add("PHP:--");
                if (CheckCmd("composer", "--version")) found.Add("Composer:OK"); else found.Add("Composer:--");
                if (CheckCmd("python", "--version")) found.Add("Python:OK"); else found.Add("Python:--");
                if (CheckCmd("git", "--version")) found.Add("Git:OK"); else found.Add("Git:--");
                if (CheckCmd("node", "-v")) found.Add("Node:OK"); else found.Add("Node:--");
                return string.Join(" | ", found.ToArray());
            });
            lblFooter.Foreground = BrushGreen;
            lblFooter.Text = "ENV // " + report;
        }

        private bool CheckCmd(string exe, string args)
        {
            try
            {
                var psi = new ProcessStartInfo(exe, args)
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using (var p = Process.Start(psi))
                {
                    p.WaitForExit(1500);
                    return p.ExitCode == 0;
                }
            }
            catch { return false; }
        }

        private string FindCloudflared()
        {
            string localCf = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cloudflared.exe");
            string[] candidates = new string[]
            {
                localCf,
                @"C:\Program Files (x86)\cloudflared\cloudflared.exe",
                @"C:\Program Files\cloudflared\cloudflared.exe"
            };
            foreach (var c in candidates)
            {
                if (File.Exists(c)) return c;
            }
            if (CheckCmd("cloudflared", "--version")) return "cloudflared";
            return null;
        }

        private async Task ToggleTunnel()
        {
            if (tunnelProc != null && !tunnelProc.HasExited)
            {
                StopTunnel();
                lblFooter.Foreground = BrushMuted;
                lblFooter.Text = "CLOUDFLARE TUNNEL STOPPED.";
                return;
            }

            string cfExe = FindCloudflared();
            if (cfExe == null)
            {
                lblFooter.Foreground = BrushCyan;
                lblFooter.Text = "cloudflared.exe NOT FOUND (Run: winget install Cloudflare.cloudflared)";
                return;
            }

            btnTunnel.IsEnabled = false;
            btnTunnel.Content = "STARTING TUNNEL...";
            lblFooter.Foreground = BrushCyan;
            lblFooter.Text = "OPENING CLOUDFLARE QUICK TUNNEL...";
            activeTunnelUrl = "";
            btnCopyUrl.IsEnabled = false;

            await Task.Run(() =>
            {
                try
                {
                    var psi = new ProcessStartInfo(cfExe, "tunnel --url http://127.0.0.1:80")
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardError = true,
                        RedirectStandardOutput = true
                    };
                    tunnelProc = Process.Start(psi);
                    DateTime deadline = DateTime.Now.AddSeconds(15);
                    var rx = new Regex(@"https://[a-zA-Z0-9\-]+\.trycloudflare\.com");

                    while (tunnelProc != null && !tunnelProc.HasExited && DateTime.Now < deadline)
                    {
                        string line = tunnelProc.StandardError.ReadLine();
                        if (line == null) break;
                        var m = rx.Match(line);
                        if (m.Success)
                        {
                            activeTunnelUrl = m.Value;
                            break;
                        }
                    }
                }
                catch { }
            });

            btnTunnel.IsEnabled = true;
            if (!string.IsNullOrEmpty(activeTunnelUrl))
            {
                try { Clipboard.SetText(activeTunnelUrl); } catch { }
                btnTunnel.Content = "CF TUNNEL: ON (STOP)";
                btnCopyUrl.IsEnabled = true;
                lblFooter.Foreground = BrushGreen;
                lblFooter.Text = "LIVE (COPIED!) // " + activeTunnelUrl;
            }
            else
            {
                StopTunnel();
                lblFooter.Foreground = BrushCyan;
                lblFooter.Text = "TUNNEL FAILED OR TIMED OUT (Check internet)";
            }
        }

        private void StopTunnel()
        {
            try
            {
                if (tunnelProc != null && !tunnelProc.HasExited)
                    tunnelProc.Kill();
            }
            catch { }
            tunnelProc = null;
            activeTunnelUrl = "";
            if (btnTunnel != null) btnTunnel.Content = "CF TUNNEL: OFF";
            if (btnCopyUrl != null) btnCopyUrl.IsEnabled = false;
        }

        private bool StartSvc(string name)
        {
            try
            {
                using (var sc = new ServiceController(name))
                {
                    if (sc.Status == ServiceControllerStatus.Stopped)
                    {
                        sc.Start();
                        sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(15));
                    }
                    return true;
                }
            }
            catch { return false; }
        }

        private void StopSvc(string name)
        {
            try
            {
                using (var sc = new ServiceController(name))
                {
                    if (sc.Status == ServiceControllerStatus.Running)
                    {
                        sc.Stop();
                        sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(15));
                    }
                }
            }
            catch { }
        }

        private void StartHidden(string exe, string args)
        {
            try
            {
                if (!File.Exists(exe)) return;
                Process.Start(new ProcessStartInfo(exe, args) { CreateNoWindow = true, UseShellExecute = false });
            }
            catch { }
        }

        private void KillProc(string name)
        {
            try { foreach (var p in Process.GetProcessesByName(name)) p.Kill(); }
            catch { }
        }

        private void OpenUrl(string url)
        {
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
            catch { }
        }

        [STAThread]
        public static void Main()
        {
            var app = new Application();
            app.Run(new SolsticeWindow());
        }
    }
}
