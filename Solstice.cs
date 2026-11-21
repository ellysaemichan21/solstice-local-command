using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.ServiceProcess;
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
        private TextBlock lblApacheState, lblMysqlState, lblMariaState, lblFooter, lblProjCount;
        private Border badgeApache, badgeMysql, badgeMaria;
        private Button btnStart, btnStop, btnRestart, btnOpenWeb, btnOpenPma;
        private TextBox txtSearch;
        private StackPanel projectListPanel;
        private DispatcherTimer pollTimer;

        private WinForms.NotifyIcon trayIcon;
        private System.Drawing.Icon icoOnline, icoPartial, icoOffline;
        private bool isQuitting = false;
        private List<string> allProjects = new List<string>();

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
        private SolidColorBrush BrushRed = new SolidColorBrush(C(248, 113, 113));

        public SolsticeWindow()
        {
            Title = "SOLSTICE // Local Command";
            Width = 760;
            Height = 470;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = System.Windows.Media.Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

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

        private System.Drawing.Icon CreateEclipseIcon(System.Drawing.Color ringColor)
        {
            var bmp = new Bitmap(32, 32);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(System.Drawing.Color.FromArgb(5, 8, 17));
                using (var border = new System.Drawing.Pen(System.Drawing.Color.FromArgb(30, 58, 138), 2))
                    g.DrawRectangle(border, 1, 1, 29, 29);
                using (var pen = new System.Drawing.Pen(ringColor, 3f))
                    g.DrawEllipse(pen, 6, 6, 20, 20);
                using (var cut = new SolidBrush(System.Drawing.Color.FromArgb(5, 8, 17)))
                    g.FillEllipse(cut, 10, 4, 16, 16);
                using (var line = new System.Drawing.Pen(System.Drawing.Color.FromArgb(170, ringColor), 1.5f))
                    g.DrawLine(line, 3, 16, 29, 16);
            }
            return System.Drawing.Icon.FromHandle(bmp.GetHicon());
        }

        private void InitTray()
        {
            icoOnline = CreateEclipseIcon(System.Drawing.Color.FromArgb(56, 189, 248));
            icoPartial = CreateEclipseIcon(System.Drawing.Color.FromArgb(245, 158, 11));
            icoOffline = CreateEclipseIcon(System.Drawing.Color.FromArgb(239, 68, 68));

            var menu = new WinForms.ContextMenuStrip();
            menu.Items.Add("Open Solstice Command", null, (s, e) => RestoreWindow());
            menu.Items.Add("-");
            menu.Items.Add("Initialize Stack (Start)", null, async (s, e) => await RunServiceAction("start"));
            menu.Items.Add("Terminate Stack (Stop)", null, async (s, e) => await RunServiceAction("stop"));
            menu.Items.Add("-");
            menu.Items.Add("Open http://solstice.local/", null, (s, e) => OpenUrl("http://solstice.local/"));
            menu.Items.Add("Open phpMyAdmin", null, (s, e) => OpenUrl("http://solstice.local/phpmyadmin/"));
            menu.Items.Add("-");
            menu.Items.Add("Quit Solstice", null, (s, e) =>
            {
                isQuitting = true;
                trayIcon.Visible = false;
                trayIcon.Dispose();
                Application.Current.Shutdown();
            });

            trayIcon = new WinForms.NotifyIcon
            {
                Icon = icoOffline,
                Text = "SOLSTICE // Local Command",
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
                Child = new TextBlock
                {
                    Text = "LOCAL COMMAND  //  SOLSTICE.LOCAL",
                    FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                    FontSize = 10,
                    FontWeight = FontWeights.Bold,
                    Foreground = BrushCyan
                }
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
            bodyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(360) });
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

            // Custom Vector Geometric SOLSTICE Wordmark + Eclipse Crest
            var logoRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 18) };
            logoRow.Children.Add(BuildEclipseCrest());
            logoRow.Children.Add(BuildSolsticeVectorWordmark());
            stack.Children.Add(logoRow);

            // Service Status Card
            var card = new Border
            {
                Background = BrushSurface,
                BorderBrush = BrushBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(14, 12, 14, 12),
                Margin = new Thickness(0, 0, 0, 14)
            };
            var cardStack = new StackPanel();
            cardStack.Children.Add(MakeServiceRow("APACHE ENGINE", "wampapache64", out lblApacheState, out badgeApache, false));
            cardStack.Children.Add(MakeServiceRow("MYSQL SERVER", "wampmysqld64", out lblMysqlState, out badgeMysql, false));
            cardStack.Children.Add(MakeServiceRow("MARIADB SERVER", "wampmariadb64", out lblMariaState, out badgeMaria, true));
            card.Child = cardStack;
            stack.Children.Add(card);

            // Primary Controls Row
            var row1 = new Grid { Margin = new Thickness(0, 0, 0, 10) };
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

            // Secondary Controls Row
            var row2 = new Grid { Margin = new Thickness(0, 0, 0, 14) };
            row2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(96) });
            row2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            row2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            row2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(108) });

            btnRestart = MakeActionBtn("RESTART", false);
            btnOpenWeb = MakeActionBtn("SOLSTICE.LOCAL", true);
            btnOpenPma = MakeActionBtn("PHPMYADMIN", false);
            btnRestart.Click += async (s, e) => await RunServiceAction("restart");
            btnOpenWeb.Click += (s, e) => OpenUrl("http://solstice.local/");
            btnOpenPma.Click += (s, e) => OpenUrl("http://solstice.local/phpmyadmin/");

            Grid.SetColumn(btnRestart, 0);
            Grid.SetColumn(btnOpenWeb, 2);
            Grid.SetColumn(btnOpenPma, 4);
            row2.Children.Add(btnRestart);
            row2.Children.Add(btnOpenWeb);
            row2.Children.Add(btnOpenPma);
            stack.Children.Add(row2);

            lblFooter = new TextBlock
            {
                Text = "STANDBY  //  ZERO TERMINAL MODE",
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                FontSize = 10.5,
                Foreground = BrushMuted
            };
            stack.Children.Add(lblFooter);

            return stack;
        }

        private UIElement BuildEclipseCrest()
        {
            var canvas = new Canvas { Width = 38, Height = 38, Margin = new Thickness(0, 0, 12, 0) };
            var ring = new Ellipse
            {
                Width = 32,
                Height = 32,
                Stroke = BrushCyan,
                StrokeThickness = 2.6
            };
            Canvas.SetLeft(ring, 3);
            Canvas.SetTop(ring, 3);

            var cut = new Ellipse
            {
                Width = 24,
                Height = 24,
                Fill = BrushObsidian
            };
            Canvas.SetLeft(cut, 10);
            Canvas.SetTop(cut, 1);

            var slash = new Line
            {
                X1 = 1, Y1 = 19, X2 = 37, Y2 = 19,
                Stroke = new SolidColorBrush(CA(160, 56, 189, 248)),
                StrokeThickness = 1.4
            };

            canvas.Children.Add(ring);
            canvas.Children.Add(cut);
            canvas.Children.Add(slash);
            return canvas;
        }

        private UIElement BuildSolsticeVectorWordmark()
        {
            // Sharp cyber-minimalist geometric vector glyphs for "SOLSTICE"
            // Coordinate space: height 24, sharp 2.6px stroke
            string glyphData =
                // S
                "M 2,20 L 14,20 L 17,17 L 17,13 L 14,11 L 4,11 L 2,9 L 2,5 L 5,2 L 17,2 " +
                // O
                "M 27,2 L 38,2 L 41,5 L 41,17 L 38,20 L 27,20 L 24,17 L 24,5 Z " +
                // L
                "M 49,2 L 49,20 L 63,20 " +
                // S
                "M 70,20 L 82,20 L 85,17 L 85,13 L 82,11 L 72,11 L 70,9 L 70,5 L 73,2 L 85,2 " +
                // T
                "M 91,2 L 107,2 M 99,2 L 99,20 " +
                // I
                "M 115,2 L 115,20 " +
                // C
                "M 138,5 L 135,2 L 126,2 L 123,5 L 123,17 L 126,20 L 135,20 L 138,17 " +
                // E
                "M 161,2 L 146,2 L 146,20 L 161,20 M 146,11 L 158,11";

            var path = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse(glyphData),
                Stroke = BrushText,
                StrokeThickness = 2.6,
                StrokeStartLineCap = PenLineCap.Square,
                StrokeEndLineCap = PenLineCap.Square,
                StrokeLineJoin = PenLineJoin.Miter,
                VerticalAlignment = VerticalAlignment.Center
            };

            var wrap = new Viewbox
            {
                Height = 24,
                Stretch = Stretch.Uniform,
                VerticalAlignment = VerticalAlignment.Center,
                Child = path,
                Effect = new DropShadowEffect
                {
                    Color = C(56, 189, 248),
                    BlurRadius = 10,
                    ShadowDepth = 0,
                    Opacity = 0.35
                }
            };
            return wrap;
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

            // Header
            var head = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            head.Children.Add(new TextBlock
            {
                Text = "PROJECTS  //  WWW",
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

            // Search Input
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

            // Scrollable Project Buttons
            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };
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
            string www = @"C:\wamp64\www";
            if (Directory.Exists(www))
            {
                foreach (var dir in Directory.GetDirectories(www))
                {
                    string name = System.IO.Path.GetFileName(dir);
                    if (name.Equals("wamplangues", StringComparison.OrdinalIgnoreCase) ||
                        name.Equals("wampthemes", StringComparison.OrdinalIgnoreCase))
                        continue;
                    allProjects.Add(name);
                }
                allProjects.Sort(StringComparer.OrdinalIgnoreCase);
            }
            RenderProjectList(txtSearch != null ? txtSearch.Text : "");
        }

        private void RenderProjectList(string filter)
        {
            projectListPanel.Children.Clear();
            string q = (filter ?? "").Trim().ToLowerInvariant();
            int shown = 0;

            foreach (var name in allProjects)
            {
                if (q.Length > 0 && !name.ToLowerInvariant().Contains(q)) continue;
                shown++;

                string projName = name;
                var btn = new Button
                {
                    HorizontalContentAlignment = HorizontalAlignment.Stretch,
                    Background = BrushObsidian,
                    BorderBrush = BrushBorder,
                    BorderThickness = new Thickness(1),
                    Margin = new Thickness(0, 0, 0, 6),
                    Padding = new Thickness(10, 7, 10, 7),
                    Cursor = Cursors.Hand
                };
                btn.Style = MakeFlatButtonStyle(BrushObsidian, BrushSurfaceHover, BrushBorder, BrushBlue);

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
                    Text = "OPEN ↗",
                    FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                    FontSize = 9.5,
                    FontWeight = FontWeights.Bold,
                    Foreground = BrushCyan,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Center
                });
                btn.Content = row;
                btn.Click += (s, e) => OpenUrl("http://solstice.local/" + Uri.EscapeDataString(projName) + "/");
                projectListPanel.Children.Add(btn);
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
            var btn = new Button
            {
                Content = text,
                Height = 36,
                Foreground = primary ? BrushCyan : BrushText,
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Cursor = Cursors.Hand,
                BorderThickness = new Thickness(1),
                Style = MakeFlatButtonStyle(normalBg, hoverBg, border, BrushCyan)
            };
            return btn;
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

        private bool IsServiceRunning(string svcName)
        {
            try
            {
                using (var sc = new ServiceController(svcName))
                    return sc.Status == ServiceControllerStatus.Running;
            }
            catch { return false; }
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
            bool a = IsServiceRunning("wampapache64");
            bool m = IsServiceRunning("wampmysqld64");
            bool d = IsServiceRunning("wampmariadb64");

            UpdateBadge(lblApacheState, badgeApache, a);
            UpdateBadge(lblMysqlState, badgeMysql, m);
            UpdateBadge(lblMariaState, badgeMaria, d);

            if (a && (m || d))
            {
                trayIcon.Icon = icoOnline;
                trayIcon.Text = "SOLSTICE // ONLINE";
            }
            else if (a || m || d)
            {
                trayIcon.Icon = icoPartial;
                trayIcon.Text = "SOLSTICE // PARTIAL";
            }
            else
            {
                trayIcon.Icon = icoOffline;
                trayIcon.Text = "SOLSTICE // OFFLINE";
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
            });

            RefreshStatuses();
            lblFooter.Foreground = mode == "stop" ? BrushMuted : BrushGreen;
            lblFooter.Text = mode == "stop" ? "STACK TERMINATED  //  STANDBY" : "STACK ONLINE  //  http://solstice.local/";
            btnStart.IsEnabled = btnStop.IsEnabled = btnRestart.IsEnabled = true;
        }

        private void StartSvc(string name)
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
                }
            }
            catch { }
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
