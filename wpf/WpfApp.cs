// ============================================================
//  DeepSeek Harness 启动器 - WPF 重构版 (代码式 WPF, 无 XAML 编译链)
//  v0.1 骨架: 深色主题 + WindowChrome 无边框窗(原生缩放/吸附) + 侧栏导航 + 切页淡入动画
//  编译: build.bat (仅用系统自带 csc + GAC WPF 程序集)
// ============================================================
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shell;
using System.Windows.Threading;

namespace DeepSeekHarness
{
    // ---------- 调色板 (与旧版一致的深色主题) ----------
    static class Palette
    {
        public static readonly Color BgDeep = Color.FromRgb(12, 18, 32);
        public static readonly Color Bg = Color.FromRgb(18, 23, 34);
        public static readonly Color BgCard = Color.FromRgb(23, 30, 48);
        public static readonly Color BgInput = Color.FromRgb(26, 33, 64);
        public static readonly Color Blue = Color.FromRgb(77, 107, 254);
        public static readonly Color BlueLight = Color.FromRgb(122, 152, 255);
        public static readonly Color Text = Color.FromRgb(230, 233, 240);
        public static readonly Color TextDim = Color.FromRgb(154, 164, 189);
        public static readonly Color TextFaint = Color.FromRgb(95, 106, 140);
        public static readonly Color Success = Color.FromRgb(52, 199, 123);
        public static readonly Color Warn = Color.FromRgb(255, 179, 71);
        public static readonly Color Error = Color.FromRgb(255, 107, 107);
        public static readonly Color Border = Color.FromArgb(23, 255, 255, 255);
        public static readonly Color BorderSoft = Color.FromArgb(15, 255, 255, 255);

        public static Brush Brush(Color c) { return new SolidColorBrush(c); }
        public static Brush BrushA(Color c, byte a) { return new SolidColorBrush(Color.FromArgb(a, c.R, c.G, c.B)); }
    }

    // ---------- 程序入口 ----------
    static class App
    {
        static Mutex singletonMutex;

        [STAThread]
        static void Main()
        {
            // 单实例: 已在运行则发信号让旧窗口弹出, 自己退出
            bool createdNew;
            singletonMutex = new Mutex(true, "DeepSeekHarness.Launcher.WPF.v1", out createdNew);
            if (!createdNew)
            {
                try { File.WriteAllText(Proc.ReopenFlagPath(), "1"); } catch { }
                return;
            }
            try { Proc.DebugMode = Environment.GetEnvironmentVariable("DSH_LAUNCHER_DEBUG") == "1"; } catch { }
            // 全局未捕获异常 → crash.log
            AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e)
            {
                try { File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log"), DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + e.ExceptionObject + "\r\n"); } catch { }
            };
            var app = new Application();
            app.ShutdownMode = ShutdownMode.OnExplicitShutdown;   // 托盘常驻, 退出走托盘菜单
            var win = new MainWindow();
            app.Run(win);
            GC.KeepAlive(singletonMutex);
        }
    }

    // ---------- 侧栏导航项 (自绘圆角, 悬停/选中动画) ----------
    class NavItem : Border
    {
        public int Index;
        public string Label;
        bool active;
        bool hover;
        TextBlock text;

        public bool Active
        {
            get { return active; }
            set { active = value; ApplyState(); }
        }

        public NavItem(int index, string label)
        {
            Index = index;
            Label = label;
            Height = 44;
            Margin = new Thickness(12, 4, 12, 4);
            CornerRadius = new CornerRadius(10);
            text = new TextBlock
            {
                Text = label,
                Foreground = Palette.Brush(Palette.TextDim),
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            Child = text;
            MouseEnter += delegate { hover = true; ApplyState(); };
            MouseLeave += delegate { hover = false; ApplyState(); };
        }

        void ApplyState()
        {
            if (active)
            {
                Background = Palette.Brush(Palette.Blue);
                text.Foreground = Palette.Brush(Palette.Text);
            }
            else if (hover)
            {
                Background = Palette.BrushA(Palette.Text, 18);
                text.Foreground = Palette.Brush(Palette.Text);
            }
            else
            {
                Background = Brushes.Transparent;
                text.Foreground = Palette.Brush(Palette.TextDim);
            }
        }
    }

    // ---------- 主窗口 ----------
    class MainWindow : Window
    {
        Grid host;                      // 内容页容器 (切页淡入)
        List<NavItem> navs = new List<NavItem>();
        List<Grid> pages = new List<Grid>();
        TextBlock sbText, sbRight;
        Dsh dsh = new Dsh();
        TextBlock ovStatus, ovSub;
        Button ovPrimary;
        TextBlock[] envName = new TextBlock[4], envVer = new TextBlock[4];

        // 环境页
        StackPanel envRows;
        Button envRedetect, envInstall;
        // 插件页
        StackPanel pluginRows;
        TextBlock pluginSummary;
        // 更新页
        TextBlock upLupCur, upLupLatest, upLupNote;
        TextBlock upDshCur, upDshLatest, upDshNote;
        TextBlock upPluginNote;
        // 日志页
        System.Windows.Controls.ComboBox logKind;
        System.Windows.Controls.TextBox logBox;
        System.Windows.Controls.CheckBox logAuto;
        DispatcherTimer logTimer;
        // 设置页
        Dictionary<string, System.Windows.Controls.TextBox> setBoxes = new Dictionary<string, System.Windows.Controls.TextBox>();
        // 托盘 / 单实例
        System.Windows.Forms.NotifyIcon tray;
        bool quitting;

        public MainWindow()
        {
            Title = "DeepSeek Harness 启动器";
            Width = 1080;
            Height = 720;
            MinWidth = 860;
            MinHeight = 600;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.CanResize;
            Background = Palette.Brush(Palette.Bg);
            dsh.Cfg = LauncherConfig.Load();   // 页面构建就要读配置, 提前加载
            Lang.Set(dsh.Cfg.Language);
            try { Environment.SetEnvironmentVariable("DSH_HOME", dsh.Cfg.DshHome); } catch { }
            // WindowChrome: 原生无边框体验 —— 四边缩放指针、最大化、Aero 吸附全是系统级, GPU 合成丝滑
            var chrome = new WindowChrome();
            chrome.CaptionHeight = 44;
            chrome.ResizeBorderThickness = new Thickness(6);
            chrome.GlassFrameThickness = new Thickness(0, 0, 0, 1);
            chrome.CornerRadius = new CornerRadius(0);
            chrome.UseAeroCaptionButtons = false;
            WindowChrome.SetWindowChrome(this, chrome);

            BuildUi();
            // 启动过渡动画: 窗口淡入 (WPF 合成器 GPU 播放)
            Opacity = 0.0;
            var fade = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(220));
            fade.EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut };
            BeginAnimation(UIElement.OpacityProperty, fade);
            Loaded += delegate
            {
                SwitchPage(0);
                InitTray();
                StartDetect();
                StartReopenWatch();
                // 测试钩子: --page N 启动后自动切到指定页 (非侵入式验证用)
                try
                {
                    string[] args = Environment.GetCommandLineArgs();
                    for (int i = 1; i < args.Length; i++)
                    {
                        if (args[i] == "--page" && i + 1 < args.Length)
                        {
                            int n;
                            if (int.TryParse(args[i + 1], out n) && n >= 0 && n < pages.Count)
                                SwitchPage(n);
                        }
                        if (args[i] == "--store") OpenStore();
                        if (args[i] == "--selftest") Selftest();
                    }
                }
                catch { }
            };
        }

        // ---------- 全链路自检 (非侵入回归: 输出 selftest.txt) ----------
        void Selftest()
        {
            var t = new Thread(delegate()
            {
                var sb = new System.Text.StringBuilder();
                try
                {
                    sb.AppendLine("cfg: port=" + dsh.Cfg.Port + " plugins=" + dsh.Cfg.PluginsRoot + " home=" + dsh.Cfg.DshHome);
                    string proxy = null;
                    try { proxy = dsh.ResolveProxy(); } catch { }
                    sb.AppendLine("proxy: " + (proxy ?? "none"));
                    var env = dsh.DetectEnvironment();
                    sb.AppendLine("env: node=" + (env.NodePath == "" ? "MISSING" : env.NodePath + " v" + env.NodeVersion)
                        + " | npm=" + (env.NpmPath == "" ? "MISSING" : env.NpmVersion)
                        + " | git=" + (env.GitPath == "" ? "MISSING" : env.GitVersion)
                        + " | dsh=" + (env.DshPath == "" ? "MISSING" : env.DshPath + " v" + env.DshVersion));
                    var plugins = dsh.ScanPlugins();
                    sb.AppendLine("plugins scanned: " + plugins.Count + (plugins.Count > 0 ? " (first: " + plugins[0].Name + " git=" + plugins[0].IsGit + ")" : ""));
                    var info = dsh.CheckUpdates(env);
                    sb.AppendLine("updates: dshUpdate=" + info.DshUpdate + " cur=" + info.DshCurrent + " latest=" + info.DshLatest + " plugins=" + info.PluginCount);
                    string lup = dsh.CheckLauncherUpdate();
                    sb.AppendLine("launcher latest: " + (lup ?? "none"));
                    var store = Dsh.FetchStore(proxy);
                    sb.AppendLine("store fetched: " + store.Count + " items" + (store.Count > 0 ? " (first: " + store[0].FullName + " stars=" + store[0].Stars + ")" : ""));
                    if (store.Count > 0) StoreCache.SaveList(store);
                    long age;
                    var cached = StoreCache.LoadList(out age);
                    sb.AppendLine("cache roundtrip: " + (cached == null ? 0 : cached.Count) + " items, age=" + age);
                    sb.AppendLine("port8099: " + Dsh.IsPortOpen(8099));
                    string err;
                    string inst = dsh.InstallNpmPlugin("dsh-does-not-exist-zz");
                    sb.AppendLine("npm bad-pkg handled: " + (inst.Length > 0 ? "rejected(" + inst + ")" : "???"));
                    bool ok = dsh.Cfg.Save();
                    sb.AppendLine("cfg save: " + ok);
                    sb.AppendLine("SELFTEST PASS");
                }
                catch (Exception ex) { sb.AppendLine("EXCEPTION: " + ex); }
                try { File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "selftest.txt"), sb.ToString()); } catch { }
                Dispatcher.BeginInvoke(new Action(delegate { sbText.Text = "自检完成，详见 selftest.txt"; }));
            });
            t.IsBackground = true;
            t.Start();
        }

        void BuildUi()
        {
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(44) });   // 标题栏
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) });   // 状态栏

            var titleBar = BuildTitleBar();
            Grid.SetRow(titleBar, 0);
            root.Children.Add(titleBar);
            var body = BuildBody();
            Grid.SetRow(body, 1);
            root.Children.Add(body);
            var statusBar = BuildStatusBar();
            Grid.SetRow(statusBar, 2);
            root.Children.Add(statusBar);

            Content = root;
        }

        // ---------- 标题栏 (WindowChrome 拖拽区) ----------
        Grid BuildTitleBar()
        {
            var bar = new Grid { Background = Palette.Brush(Palette.BgDeep) };
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });

            var brand = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(14, 0, 0, 0) };
            var dot = new Border { Width = 22, Height = 22, CornerRadius = new CornerRadius(6), Background = Palette.Brush(Palette.Blue), VerticalAlignment = VerticalAlignment.Center };
            dot.Child = new TextBlock { Text = "🐋", FontSize = 13, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            var title = new TextBlock { Text = "DeepSeek Harness " + Lang.T("启动器"), Foreground = Palette.Brush(Palette.Text), FontSize = 14, FontWeight = FontWeights.SemiBold, Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            var sub = new TextBlock { Text = Lang.T("WPF 重构版"), Foreground = Palette.Brush(Palette.TextFaint), FontSize = 11, Margin = new Thickness(10, 2, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            brand.Children.Add(dot);
            brand.Children.Add(title);
            brand.Children.Add(sub);

            var btns = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            btns.Children.Add(TitleBtn("─", delegate { WindowState = WindowState.Minimized; }));
            btns.Children.Add(TitleBtn("▢", delegate
            {
                WindowState = (WindowState == WindowState.Maximized) ? WindowState.Normal : WindowState.Maximized;
            }));
            btns.Children.Add(TitleBtn("✕", delegate { Close(); }, true));

            Grid.SetColumn(btns, 1);
            bar.Children.Add(brand);
            bar.Children.Add(btns);
            return bar;
        }

        Button TitleBtn(string glyph, Action onClick, bool danger = false)
        {
            var b = new Button
            {
                Content = glyph,
                Width = 48,
                FontSize = 13,
                Foreground = Palette.Brush(Palette.TextDim),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            WindowChrome.SetIsHitTestVisibleInChrome(b, true);
            b.MouseEnter += delegate { b.Background = danger ? Palette.BrushA(Palette.Error, 60) : Palette.BrushA(Palette.Text, 24); };
            b.MouseLeave += delegate { b.Background = Brushes.Transparent; };
            b.Click += delegate { onClick(); };
            return b;
        }

        // ---------- 主体: 侧栏 + 内容 ----------
        Grid BuildBody()
        {
            var body = new Grid();
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(210) });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // 侧栏
            var sidebar = new Grid { Background = Palette.Brush(Palette.BgDeep) };
            sidebar.RowDefinitions.Add(new RowDefinition { Height = new GridLength(96) });  // 品牌块
            sidebar.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            sidebar.RowDefinitions.Add(new RowDefinition { Height = new GridLength(34) });  // 版本脚注

            var brandRow = new StackPanel { Margin = new Thickness(16, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            brandRow.Children.Add(new TextBlock { Text = "DeepSeek Harness", Foreground = Palette.Brush(Palette.Text), FontSize = 16, FontWeight = FontWeights.Bold });
            brandRow.Children.Add(new TextBlock { Text = Lang.T("DSH 启动器 · WPF"), Foreground = Palette.Brush(Palette.TextDim), FontSize = 11, Margin = new Thickness(1, 4, 0, 0) });

            var navHost = new StackPanel { Margin = new Thickness(0, 6, 0, 0) };
            string[] names = { Lang.T("概览"), Lang.T("环境"), Lang.T("插件"), Lang.T("更新"), Lang.T("日志"), Lang.T("设置") };
            for (int i = 0; i < names.Length; i++)
            {
                int idx = i;
                var nav = new NavItem(i, names[i]);
                nav.MouseLeftButtonUp += delegate { SwitchPage(idx); };
                navs.Add(nav);
                navHost.Children.Add(nav);
            }

            var foot = new TextBlock
            {
                Text = "v1.5.0 · by loudMore",
                Foreground = Palette.Brush(Palette.TextFaint),
                FontSize = 11,
                Margin = new Thickness(16, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            Grid.SetRow(navHost, 1);
            Grid.SetRow(foot, 2);
            sidebar.Children.Add(brandRow);
            sidebar.Children.Add(navHost);
            sidebar.Children.Add(foot);

            // 内容页容器
            var content = new Grid { Background = Palette.Brush(Palette.Bg) };
            host = new Grid { Margin = new Thickness(24, 20, 24, 20) };
            content.Children.Add(host);

            // 6 个页面: 全部真实内容
            pages.Add(BuildOverviewPage());
            pages.Add(BuildEnvPage());
            pages.Add(BuildPluginsPage());
            pages.Add(BuildUpdatePage());
            pages.Add(BuildLogsPage());
            pages.Add(BuildSettingsPage());
            Grid.SetColumn(sidebar, 0);
            Grid.SetColumn(content, 1);
            body.Children.Add(sidebar);
            body.Children.Add(content);
            return body;
        }

        // ---------- 状态栏 ----------
        Grid BuildStatusBar()
        {
            var bar = new Grid { Background = Palette.Brush(Palette.BgDeep) };
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });

            sbText = new TextBlock
            {
                Text = Lang.T("准备就绪"),
                Foreground = Palette.Brush(Palette.TextDim),
                FontSize = 12,
                Margin = new Thickness(14, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            sbRight = new TextBlock
            {
                Text = "端口 8099 · 启动器 v1.5.0 (WPF)",
                Foreground = Palette.Brush(Palette.TextFaint),
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 0, 14, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            sbRight.Text = "端口 8099 · 启动器 v1.5.0 (WPF)";
            Grid.SetColumn(sbRight, 1);
            bar.Children.Add(sbText);
            bar.Children.Add(sbRight);
            return bar;
        }

        // ---------- 概览页 (真实内容: 服务状态 + 运行环境) ----------
        Grid BuildOverviewPage()
        {
            var pg = new Grid();
            pg.RowDefinitions.Add(new RowDefinition { Height = new GridLength(36) });
            pg.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            var head = new TextBlock
            {
                Text = "概览",
                Foreground = Palette.Brush(Palette.Text),
                FontSize = 20,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            };

            var stack = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };

            // 状态主卡
            var hero = new Border { Background = Palette.Brush(Palette.BgCard), CornerRadius = new CornerRadius(14), Padding = new Thickness(20, 16, 20, 16) };
            var heroGrid = new Grid();
            heroGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            heroGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
            var statusCol = new StackPanel();
            ovStatus = new TextBlock { Text = "● " + Lang.T("检测中…"), Foreground = Palette.Brush(Palette.TextDim), FontSize = 22, FontWeight = FontWeights.SemiBold };
            ovSub = new TextBlock { Text = Lang.T("正在检测环境与服务状态…"), Foreground = Palette.Brush(Palette.TextFaint), FontSize = 13, Margin = new Thickness(0, 6, 0, 0) };
            statusCol.Children.Add(ovStatus);
            statusCol.Children.Add(ovSub);
            ovPrimary = new Button
            {
                Content = Lang.T("一键启动"),
                Height = 38,
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                Background = Palette.Brush(Palette.Blue),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center
            };
            ovPrimary.MouseEnter += delegate { ovPrimary.Background = Palette.Brush(Palette.BlueLight); };
            ovPrimary.MouseLeave += delegate { ovPrimary.Background = Palette.Brush(Palette.Blue); };
            ovPrimary.Click += delegate { ToggleService(); };
            Grid.SetColumn(ovPrimary, 1);
            heroGrid.Children.Add(statusCol);
            heroGrid.Children.Add(ovPrimary);
            hero.Child = heroGrid;
            stack.Children.Add(hero);

            // 运行环境卡
            var envCard = new Border { Background = Palette.Brush(Palette.BgCard), CornerRadius = new CornerRadius(14), Padding = new Thickness(20, 14, 20, 14), Margin = new Thickness(0, 14, 0, 0) };
            var envStack = new StackPanel();
            envStack.Children.Add(new TextBlock { Text = Lang.T("运行环境"), Foreground = Palette.Brush(Palette.Text), FontSize = 15, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 8) });
            string[] tools = { "Node.js", "npm", "Git", "dsh" };
            for (int i = 0; i < 4; i++)
            {
                var row = new Grid { Margin = new Thickness(0, 4, 0, 4) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
                envName[i] = new TextBlock { Text = tools[i], Foreground = Palette.Brush(Palette.TextDim), FontSize = 13 };
                envVer[i] = new TextBlock { Text = "检测中…", Foreground = Palette.Brush(Palette.TextFaint), FontSize = 13, HorizontalAlignment = HorizontalAlignment.Right, TextTrimming = TextTrimming.CharacterEllipsis };
                Grid.SetColumn(envVer[i], 2);
                row.Children.Add(envName[i]);
                row.Children.Add(envVer[i]);
                envStack.Children.Add(row);
            }
            envCard.Child = envStack;
            stack.Children.Add(envCard);

            var scroll = new ScrollViewer { Content = stack, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            Grid.SetRow(head, 0);
            Grid.SetRow(scroll, 1);
            pg.Children.Add(head);
            pg.Children.Add(scroll);
            return pg;
        }

        // ---------- 启动流程: 代理并行检测 + 环境检测 + 更新检查 ----------
        void StartDetect()
        {
            if (dsh.Cfg == null) dsh.Cfg = LauncherConfig.Load();
            try { Environment.SetEnvironmentVariable("DSH_HOME", dsh.Cfg.DshHome); } catch { }
            dsh.OnStatus = delegate(string s) { Dispatcher.BeginInvoke(new Action(delegate { sbText.Text = s; })); };
            dsh.OnLog = delegate(string s) { };
            var t = new Thread(delegate()
            {
                try { dsh.ResolveProxy(); } catch { }          // 后台: 端口扫描可能耗时
                var env = dsh.DetectEnvironment();
                dsh.Env = env;
                Dispatcher.BeginInvoke(new Action(delegate { RenderOverview(); RenderEnv(); }));
                if (dsh.Cfg.CheckUpdatesOnStart)
                {
                    var info = dsh.CheckUpdates(env);
                    dsh.Update = info;
                    Dispatcher.BeginInvoke(new Action(delegate { RenderOverview(); RenderUpdate(); }));
                    // 启动器自更新状态也自动检查
                    string lupLatest = dsh.CheckLauncherUpdate();
                    if (lupLatest != null)
                    {
                        Dispatcher.BeginInvoke(new Action(delegate
                        {
                            upLupLatest.Text = "最新 " + lupLatest;
                            bool newer = lupLatest != "1.5.0";
                            upLupNote.Text = newer ? "发现新版本，可前往 GitHub 下载" : "已是最新版本";
                            upLupNote.Foreground = Palette.Brush(newer ? Palette.Warn : Palette.TextFaint);
                        }));
                    }
                }
            });
            t.IsBackground = true;
            t.Start();
        }

        void RenderOverview()
        {
            var env = dsh.Env;
            bool running = Dsh.IsPortOpen(dsh.Cfg.Port);
            bool nodeOk = !string.IsNullOrEmpty(env.NodePath);
            bool dshOk = !string.IsNullOrEmpty(env.DshPath);
            if (!nodeOk || !dshOk)
            {
                ovStatus.Text = !nodeOk ? "● 未检测到 Node.js" : "● 未检测到 dsh";
                ovStatus.Foreground = Palette.Brush(Palette.Warn);
                ovSub.Text = Lang.T("首次使用请点击「一键安装」");
                ovPrimary.Content = Lang.T("一键安装");
            }
            else if (running)
            {
                ovStatus.Text = "● " + Lang.T("服务运行中");
                ovStatus.Foreground = Palette.Brush(Palette.Success);
                ovSub.Text = string.Format("http://127.0.0.1:{0}", dsh.Cfg.Port);
                ovPrimary.Content = Lang.T("停止服务");
            }
            else
            {
                ovStatus.Text = "● " + Lang.T("服务未启动");
                ovStatus.Foreground = Palette.Brush(Palette.TextDim);
                ovSub.Text = Lang.T("环境已就绪，点击「一键启动」开始使用");
                ovPrimary.Content = Lang.T("一键启动");
            }
            string[] vers = { env.NodeVersion, env.NpmVersion, env.GitVersion, env.DshVersion };
            string[] paths = { env.NodePath, env.NpmPath, env.GitPath, env.DshPath };
            for (int i = 0; i < 4; i++)
            {
                if (string.IsNullOrEmpty(paths[i]))
                    envVer[i].Text = "未检测到";
                else
                    envVer[i].Text = (string.IsNullOrEmpty(vers[i]) ? "" : vers[i] + "  ·  ") + paths[i];
                envVer[i].Foreground = Palette.Brush(string.IsNullOrEmpty(paths[i]) ? Palette.Warn : Palette.TextFaint);
            }
            sbRight.Text = string.Format(Lang.T("端口 {0} · 启动器 v1.5.0 (WPF)"), dsh.Cfg.Port);
        }

        void ToggleService()
        {
            if (Dsh.IsPortOpen(dsh.Cfg.Port)) dsh.StopServiceAsync();
            else dsh.StartServiceAsync();
            // 轮询状态刷新
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
            timer.Tick += delegate
            {
                RenderOverview();
                if (sbText.Text.IndexOf("就绪", StringComparison.Ordinal) >= 0 || sbText.Text.IndexOf("失败", StringComparison.Ordinal) >= 0
                    || sbText.Text.IndexOf("停止", StringComparison.Ordinal) >= 0)
                    timer.Stop();
            };
            timer.Start();
        }

        // ---------- 通用构建辅助 ----------
        static Border Card(UIElement child)
        {
            return new Border
            {
                Background = Palette.Brush(Palette.BgCard),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(18, 14, 18, 14),
                Margin = new Thickness(0, 12, 0, 0),
                Child = child
            };
        }

        static TextBlock PageHead(string t)
        {
            return new TextBlock
            {
                Text = t,
                Foreground = Palette.Brush(Palette.Text),
                FontSize = 20,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        static Grid PageShell(string title, out ScrollViewer scroll)
        {
            var pg = new Grid();
            pg.RowDefinitions.Add(new RowDefinition { Height = new GridLength(36) });
            pg.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            Grid.SetRow(scroll, 1);
            pg.Children.Add(PageHead(Lang.T(title)));
            pg.Children.Add(scroll);
            return pg;
        }

        Button Btn(string text, Action onClick, bool primary)
        {
            var b = new Button
            {
                Content = text,
                Height = 34,
                FontSize = 13,
                Foreground = Brushes.White,
                Background = Palette.Brush(primary ? Palette.Blue : Palette.BgInput),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 10, 0),
                Padding = new Thickness(14, 0, 14, 0)
            };
            b.MouseEnter += delegate { b.Background = Palette.Brush(primary ? Palette.BlueLight : Palette.TextDim); };
            b.MouseLeave += delegate { b.Background = Palette.Brush(primary ? Palette.Blue : Palette.BgInput); };
            b.Click += delegate { onClick(); };
            return b;
        }

        // ---------- 环境页 ----------
        Grid BuildEnvPage()
        {
            Grid pg;
            ScrollViewer scroll;
            pg = PageShell("环境", out scroll);
            var stack = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };
            var toolbar = new StackPanel { Orientation = Orientation.Horizontal };
            envRedetect = Btn("↻ " + Lang.T("重新检测"), delegate { RunDetect(); }, false);
            envInstall = Btn("↓ " + Lang.T("一键安装 / 修复环境"), delegate { RunInstall(); }, true);
            toolbar.Children.Add(envRedetect);
            toolbar.Children.Add(envInstall);
            stack.Children.Add(toolbar);
            envRows = new StackPanel();
            stack.Children.Add(Card(envRows));
            scroll.Content = stack;
            return pg;
        }

        void RenderEnv()
        {
            if (envRows == null) return;
            envRows.Children.Clear();
            var env = dsh.Env;
            string[] names = { "Node.js", "npm", "Git", "dsh" };
            string[] vers = { env.NodeVersion, env.NpmVersion, env.GitVersion, env.DshVersion };
            string[] paths = { env.NodePath, env.NpmPath, env.GitPath, env.DshPath };
            for (int i = 0; i < 4; i++)
            {
                bool ok = !string.IsNullOrEmpty(paths[i]);
                var row = new Grid { Margin = new Thickness(0, 6, 0, 6) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.Children.Add(new TextBlock { Text = ok ? "●" : "○", Foreground = Palette.Brush(ok ? Palette.Success : Palette.Warn), FontSize = 12, VerticalAlignment = VerticalAlignment.Center });
                var nameTb = new TextBlock { Text = names[i], Foreground = Palette.Brush(Palette.Text), FontSize = 14, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(nameTb, 1);
                row.Children.Add(nameTb);
                var info = new TextBlock
                {
                    Text = ok ? ((string.IsNullOrEmpty(vers[i]) ? "" : vers[i] + "   ") + paths[i]) : Lang.T("未检测到（可点击上方「一键安装」）"),
                    Foreground = Palette.Brush(ok ? Palette.TextFaint : Palette.Warn),
                    FontSize = 12,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                Grid.SetColumn(info, 2);
                row.Children.Add(info);
                envRows.Children.Add(row);
            }
            envRows.Children.Add(new TextBlock
            {
                Text = Lang.T("数据目录") + ": " + dsh.Cfg.DshHome + (env.DshHomeExists ? "" : "  (不存在)"),
                Foreground = Palette.Brush(env.DshHomeExists ? Palette.TextFaint : Palette.Warn),
                FontSize = 12,
                Margin = new Thickness(0, 8, 0, 0)
            });
        }

        void RunDetect()
        {
            sbText.Text = "正在检测环境…";
            var t = new Thread(delegate()
            {
                try { dsh.ResolveProxy(); } catch { }
                var env = dsh.DetectEnvironment();
                dsh.Env = env;
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    RenderEnv();
                    RenderOverview();
                    sbText.Text = "环境检测完成";
                }));
            });
            t.IsBackground = true;
            t.Start();
        }

        void RunInstall()
        {
            sbText.Text = "正在安装…";
            envInstall.IsEnabled = false;
            var t = new Thread(delegate()
            {
                string error;
                bool ok = dsh.InstallDshNow(out error);
                var env = dsh.DetectEnvironment();
                dsh.Env = env;
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    envInstall.IsEnabled = true;
                    RenderEnv();
                    RenderOverview();
                    if (ok) { sbText.Text = "环境安装完成"; MessageBox.Show(this, "环境安装完成，点击「一键启动」开始使用。", "一键安装", MessageBoxButton.OK, MessageBoxImage.Information); }
                    else { sbText.Text = "安装未完成"; MessageBox.Show(this, error + "\n\n详细信息见 launcher.log。", "一键安装", MessageBoxButton.OK, MessageBoxImage.Warning); }
                }));
            });
            t.IsBackground = true;
            t.Start();
        }

        // ---------- 插件页 ----------
        Grid BuildPluginsPage()
        {
            Grid pg;
            ScrollViewer scroll;
            pg = PageShell("插件管理", out scroll);
            var stack = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };
            var toolbar = new StackPanel { Orientation = Orientation.Horizontal };
            toolbar.Children.Add(Btn("↻ " + Lang.T("刷新列表"), delegate { RenderPlugins(); }, false));
            toolbar.Children.Add(Btn("↓ " + Lang.T("安装插件"), delegate { InstallPluginPrompt(); }, false));
            toolbar.Children.Add(Btn("🛍 " + Lang.T("插件商城"), delegate { OpenStore(); }, true));
            toolbar.Children.Add(Btn("↻ " + Lang.T("全部更新"), delegate { UpdateAllPlugins(); }, false));
            toolbar.Children.Add(Btn(Lang.T("一键维护"), delegate { MaintainPlugins(); }, false));
            toolbar.Children.Add(Btn(Lang.T("打开插件目录"), delegate { OpenPluginsDir(); }, false));
            stack.Children.Add(toolbar);
            pluginSummary = new TextBlock { Foreground = Palette.Brush(Palette.TextFaint), FontSize = 12, Margin = new Thickness(2, 8, 0, 0) };
            stack.Children.Add(pluginSummary);
            pluginRows = new StackPanel();
            stack.Children.Add(Card(pluginRows));
            scroll.Content = stack;
            return pg;
        }

        void OpenStore()
        {
            var w = new StoreWindow(dsh) { Owner = this };
            w.Show();
        }

        void OpenPluginsDir()
        {
            try { Process.Start("explorer.exe", "\"" + dsh.Cfg.PluginsRoot + "\""); } catch { }
        }

        void RenderPlugins()
        {
            if (pluginRows == null) return;
            pluginRows.Children.Clear();
            var plugins = dsh.ScanPlugins();
            pluginSummary.Text = string.Format(Lang.T("共 {0} 个目录 · {1} 个 git 仓库"), plugins.Count, CountGit(plugins));
            if (plugins.Count == 0)
            {
                pluginRows.Children.Add(new TextBlock { Text = "未发现任何插件。\n插件目录: " + dsh.Cfg.PluginsRoot, Foreground = Palette.Brush(Palette.TextDim), FontSize = 13, Margin = new Thickness(2, 6, 0, 6) });
                return;
            }
            foreach (var p in plugins)
            {
                var row = new Grid { Margin = new Thickness(0, 5, 0, 5) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(330) });
                var nameCol = new StackPanel();
                nameCol.Children.Add(new TextBlock { Text = p.Name, Foreground = Palette.Brush(Palette.Text), FontSize = 14, FontWeight = FontWeights.SemiBold });
                nameCol.Children.Add(new TextBlock
                {
                    Text = p.Disabled ? "● 已禁用" : (p.IsGit ? "● git" : "● 普通目录"),
                    Foreground = Palette.Brush(p.Disabled ? Palette.Warn : (p.IsGit ? Palette.Success : Palette.TextDim)),
                    FontSize = 11
                });
                row.Children.Add(nameCol);
                var urlLbl = new TextBlock
                {
                    Text = string.IsNullOrEmpty(p.RemoteUrl) ? p.Path : p.RemoteUrl,
                    Foreground = Palette.Brush(string.IsNullOrEmpty(p.RemoteUrl) ? Palette.TextFaint : Palette.BlueLight),
                    FontSize = 11,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    Cursor = string.IsNullOrEmpty(p.RemoteUrl) ? Cursors.Arrow : Cursors.Hand
                };
                if (!string.IsNullOrEmpty(p.RemoteUrl))
                    urlLbl.MouseLeftButtonUp += delegate { try { Process.Start(p.RemoteUrl); } catch { } };
                Grid.SetColumn(urlLbl, 1);
                row.Children.Add(urlLbl);
                var btns = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
                PluginItem pp = p;
                if (p.IsGit) btns.Children.Add(Btn(Lang.T("更新"), delegate { Op(pp, dsh.PullPlugin); }, false));
                btns.Children.Add(Btn(Lang.T("目录"), delegate { try { Process.Start("explorer.exe", "\"" + pp.Path + "\""); } catch { } }, false));
                btns.Children.Add(Btn(Lang.T("卸载"), delegate { ConfirmUninstall(pp); }, false));
                btns.Children.Add(Btn(p.Disabled ? Lang.T("启用") : Lang.T("禁用"), delegate { Op(pp, dsh.TogglePlugin); }, false));
                Grid.SetColumn(btns, 2);
                row.Children.Add(btns);
                pluginRows.Children.Add(row);
            }
        }

        static int CountGit(List<PluginItem> list)
        {
            int n = 0;
            foreach (var p in list) if (p.IsGit) n++;
            return n;
        }

        void Op(PluginItem p, Func<PluginItem, string> op)
        {
            var t = new Thread(delegate()
            {
                string err = "";
                try { err = op(p); } catch (Exception ex) { err = ex.Message; }
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    RenderPlugins();
                    if (err.Length > 0) MessageBox.Show(this, err, "操作失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                    else { RenderPlugins(); RunDetect(); }
                }));
            });
            t.IsBackground = true;
            t.Start();
        }

        void ConfirmUninstall(PluginItem p)
        {
            var r = MessageBox.Show(this, "确定卸载插件「" + p.Name + "」？\n\n将删除目录:\n" + p.Path + "\n\n（此操作不可撤销）", "卸载插件", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (r == MessageBoxResult.Yes) Op(p, dsh.UninstallPlugin);
        }

        void InstallPluginPrompt()
        {
            string input = Prompt("安装插件", "支持两种方式安装:\n\n  1. git 仓库地址 (克隆到插件目录)\n  2. npm 包名 (全局安装)\n\n示例: https://github.com/user/plugin.git", "");
            if (string.IsNullOrEmpty(input)) return;
            bool isUrl = input.IndexOf("://") >= 0 || input.StartsWith("git@") || input.StartsWith("http");
            var t = new Thread(delegate()
            {
                string err = isUrl ? dsh.InstallPluginFromUrl(input) : dsh.InstallNpmPlugin(input);
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    RenderPlugins();
                    if (err.Length > 0) MessageBox.Show(this, err, "插件安装失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                    else { sbText.Text = "插件已安装"; RenderPlugins(); RunDetect(); }
                }));
            });
            t.IsBackground = true;
            t.Start();
        }

        void UpdateAllPlugins()
        {
            sbText.Text = "正在更新所有插件…";
            var t = new Thread(delegate()
            {
                string[] results = dsh.PullAllPlugins();
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    sbText.Text = "插件更新完成";
                    MessageBox.Show(this, string.Join("\n", results), "全部更新", MessageBoxButton.OK, MessageBoxImage.Information);
                    RenderPlugins();
                }));
            });
            t.IsBackground = true;
            t.Start();
        }

        void MaintainPlugins()
        {
            sbText.Text = "一键维护中…";
            var t = new Thread(delegate()
            {
                var lines = new List<string>();
                lines.AddRange(dsh.PullAllPlugins());
                lines.AddRange(dsh.RepairPlugins());
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    sbText.Text = "一键维护完成";
                    MessageBox.Show(this, string.Join("\n", lines.ToArray()), "一键维护", MessageBoxButton.OK, MessageBoxImage.Information);
                    RenderPlugins();
                }));
            });
            t.IsBackground = true;
            t.Start();
        }

        // ---------- 更新页 ----------
        Grid BuildUpdatePage()
        {
            Grid pg;
            ScrollViewer scroll;
            pg = PageShell("更新与升级", out scroll);
            var stack = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };
            stack.Children.Add(Btn("↻ " + Lang.T("检查更新"), delegate { RunUpdateCheck(); }, true));

            // 启动器
            var lup = new Grid();
            lup.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
            lup.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            lup.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
            var lupCol = new StackPanel();
            upLupCur = new TextBlock { Text = "当前 v1.5.0", Foreground = Palette.Brush(Palette.Text), FontSize = 14, FontWeight = FontWeights.SemiBold };
            upLupLatest = new TextBlock { Text = Lang.T("未检查"), Foreground = Palette.Brush(Palette.TextFaint), FontSize = 12 };
            upLupNote = new TextBlock { Text = "", Foreground = Palette.Brush(Palette.TextFaint), FontSize = 12, TextWrapping = TextWrapping.Wrap };
            lupCol.Children.Add(upLupCur);
            lupCol.Children.Add(upLupLatest);
            lupCol.Children.Add(upLupNote);
            lup.Children.Add(lupCol);
            var lupBtn = Btn(Lang.T("前往 GitHub"), delegate { try { Process.Start("https://github.com/loudMore/dsh-launcher/releases"); } catch { } }, false);
            Grid.SetColumn(lupBtn, 2);
            lup.Children.Add(lupBtn);
            stack.Children.Add(Card(lup));

            // dsh
            var dshG = new Grid();
            dshG.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
            dshG.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            dshG.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });
            var dshCol = new StackPanel();
            upDshCur = new TextBlock { Text = Lang.T("当前") + " -", Foreground = Palette.Brush(Palette.Text), FontSize = 14, FontWeight = FontWeights.SemiBold };
            upDshLatest = new TextBlock { Text = Lang.T("未检查"), Foreground = Palette.Brush(Palette.TextFaint), FontSize = 12 };
            upDshNote = new TextBlock { Text = "", Foreground = Palette.Brush(Palette.TextFaint), FontSize = 12, TextWrapping = TextWrapping.Wrap };
            dshCol.Children.Add(upDshCur);
            dshCol.Children.Add(upDshLatest);
            dshCol.Children.Add(upDshNote);
            dshG.Children.Add(dshCol);
            var dshBtn = Btn(Lang.T("立即升级 dsh"), delegate { UpgradeDsh(); }, true);
            Grid.SetColumn(dshBtn, 2);
            dshG.Children.Add(dshBtn);
            stack.Children.Add(Card(dshG));

            // 插件
            var plg = new StackPanel();
            upPluginNote = new TextBlock { Text = "插件更新: 未检查", Foreground = Palette.Brush(Palette.Text), FontSize = 14, FontWeight = FontWeights.SemiBold };
            plg.Children.Add(upPluginNote);
            var plgBtn = Btn(Lang.T("全部更新插件"), delegate { UpdateAllPlugins(); }, false);
            plgBtn.Margin = new Thickness(0, 8, 0, 0);
            plg.Children.Add(plgBtn);
            stack.Children.Add(Card(plg));

            scroll.Content = stack;
            return pg;
        }

        void RenderUpdate()
        {
            if (upDshCur == null) return;
            var u = dsh.Update;
            upDshCur.Text = "当前 " + (string.IsNullOrEmpty(u.DshCurrent) ? "-" : u.DshCurrent);
            upDshLatest.Text = "最新 " + (string.IsNullOrEmpty(u.DshLatest) ? "-" : u.DshLatest);
            upDshNote.Text = u.DshUpdate ? "发现新版本！" : "已是最新版本";
            upDshNote.Foreground = Palette.Brush(u.DshUpdate ? Palette.Warn : Palette.TextFaint);
            upPluginNote.Text = u.PluginCount > 0
                ? "插件更新: " + u.PluginCount + " 个可更新（" + u.PluginNames + "）"
                : "插件更新: 全部最新";
            upPluginNote.Foreground = Palette.Brush(u.PluginCount > 0 ? Palette.Warn : Palette.Text);
        }

        void RunUpdateCheck()
        {
            sbText.Text = "正在检查更新…";
            var t = new Thread(delegate()
            {
                string lupLatest = dsh.CheckLauncherUpdate();
                var info = dsh.CheckUpdates(dsh.Env);
                dsh.Update = info;
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    bool newer = false;
                    if (lupLatest != null)
                    {
                        upLupLatest.Text = "最新 " + lupLatest;
                        newer = lupLatest != "1.5.0";
                    }
                    upLupNote.Text = newer ? "发现新版本，可前往 GitHub 下载" : "已是最新版本";
                    upLupNote.Foreground = Palette.Brush(newer ? Palette.Warn : Palette.TextFaint);
                    RenderUpdate();
                    sbText.Text = "检查更新完成";
                }));
            });
            t.IsBackground = true;
            t.Start();
        }

        void UpgradeDsh()
        {
            sbText.Text = "正在升级 dsh…";
            var t = new Thread(delegate()
            {
                string r = dsh.NpmInstallGlobal(dsh.Cfg.NpmPackage, 360000);
                var env = dsh.DetectEnvironment();
                dsh.Env = env;
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    RenderUpdate();
                    RenderOverview();
                    sbText.Text = r == null ? "升级失败" : "dsh 升级完成";
                    MessageBox.Show(this, r == null ? "升级失败（网络或超时），详见日志。" : "dsh 已升级到最新版。", "升级 dsh", MessageBoxButton.OK, r == null ? MessageBoxImage.Warning : MessageBoxImage.Information);
                }));
            });
            t.IsBackground = true;
            t.Start();
        }

        // ---------- 日志页 ----------
        Grid BuildLogsPage()
        {
            Grid pg;
            ScrollViewer scroll;
            pg = PageShell("日志查看", out scroll);
            var stack = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };
            var toolbar = new StackPanel { Orientation = Orientation.Horizontal };
            logKind = new System.Windows.Controls.ComboBox
            {
                Width = 160, Height = 32, VerticalAlignment = VerticalAlignment.Center,
                Background = Palette.Brush(Palette.BgInput), Foreground = Palette.Brush(Palette.Text),
                FontSize = 13, Margin = new Thickness(0, 0, 10, 0)
            };
            logKind.Items.Add("launcher.log");
            logKind.Items.Add("dsh.log");
            logKind.SelectedIndex = 0;
            toolbar.Children.Add(logKind);
            toolbar.Children.Add(Btn("↻ " + Lang.T("刷新列表"), delegate { RefreshLog(); }, false));
            toolbar.Children.Add(Btn(Lang.T("打开日志目录"), delegate { try { Process.Start("explorer.exe", "\"" + dsh.Cfg.LogDir + "\""); } catch { } }, false));
            logAuto = new System.Windows.Controls.CheckBox
            {
                Content = "自动刷新", Foreground = Palette.Brush(Palette.TextDim), FontSize = 13,
                VerticalAlignment = VerticalAlignment.Center, IsChecked = true, Margin = new Thickness(6, 0, 0, 0)
            };
            toolbar.Children.Add(logAuto);
            stack.Children.Add(toolbar);
            logBox = new System.Windows.Controls.TextBox
            {
                IsReadOnly = true, TextWrapping = TextWrapping.NoWrap, FontFamily = new FontFamily("Consolas"),
                FontSize = 12, Background = Palette.Brush(Palette.BgInput), Foreground = Palette.Brush(Palette.TextDim),
                BorderThickness = new Thickness(0), Padding = new Thickness(10), VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Height = 460
            };
            stack.Children.Add(Card(logBox));
            scroll.Content = stack;
            logTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            logTimer.Tick += delegate { if (logAuto.IsChecked == true) RefreshLog(); };
            logTimer.Start();
            return pg;
        }

        void RefreshLog()
        {
            string name = logKind.SelectedIndex == 1 ? "dsh.log" : "launcher.log";
            logBox.Text = Dsh.ReadTail(Path.Combine(dsh.Cfg.LogDir, name), 500);
            logBox.ScrollToEnd();
        }

        // ---------- 设置页 ----------
        Grid BuildSettingsPage()
        {
            Grid pg;
            ScrollViewer scroll;
            pg = PageShell("设置", out scroll);
            var stack = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };

            var grid = new Grid { Margin = new Thickness(0, 0, 0, 4) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            int rr = 0;
            AddSettingRow(grid, rr++, "服务端口", dsh.Cfg.Port.ToString(), "port");
            AddSettingRow(grid, rr++, "DSH_HOME", dsh.Cfg.DshHome, "home");
            AddSettingRow(grid, rr++, "插件目录", dsh.Cfg.PluginsRoot, "plugins");
            AddSettingRow(grid, rr++, "日志目录", dsh.Cfg.LogDir, "log");
            AddSettingRow(grid, rr++, "npm 包名", dsh.Cfg.NpmPackage, "npm");
            AddSettingRow(grid, rr++, "启动器更新源", dsh.Cfg.LauncherUpdateUrl, "lup");
            AddSettingRow(grid, rr++, "代理地址", dsh.Cfg.Proxy, "proxy");

            // 语言
            var langLbl = new TextBlock { Text = "界面语言", Foreground = Palette.Brush(Palette.Text), FontSize = 13, Margin = new Thickness(0, 10, 0, 0) };
            setLang = new System.Windows.Controls.ComboBox
            {
                Width = 200, Height = 32, HorizontalAlignment = HorizontalAlignment.Left,
                Background = Palette.Brush(Palette.BgInput), Foreground = Palette.Brush(Palette.Text), FontSize = 13,
                Margin = new Thickness(0, 10, 0, 0)
            };
            setLang.Items.Add("跟随系统 (Auto)");
            setLang.Items.Add("简体中文");
            setLang.Items.Add("English");
            setLang.SelectedIndex = dsh.Cfg.Language == "zh" ? 1 : (dsh.Cfg.Language == "en" ? 2 : 0);
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetRow(langLbl, rr);
            Grid.SetRow(setLang, rr);
            grid.Children.Add(langLbl);
            grid.Children.Add(setLang);
            Grid.SetColumn(setLang, 1);
            rr++;

            stack.Children.Add(Card(grid));
            var btnRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
            btnRow.Children.Add(Btn(Lang.T("保存设置"), delegate { SaveSettings(); }, true));
            btnRow.Children.Add(Btn(Lang.T("自动检测回填"), delegate { AutoFillSettings(); }, false));
            btnRow.Children.Add(Btn(Lang.T("打开配置文件"), delegate { try { Process.Start("notepad.exe", "\"" + LauncherConfig.ConfigPath + "\""); } catch { } }, false));
            stack.Children.Add(btnRow);
            var gh = new TextBlock
            {
                Text = "⭐ " + Lang.T("GitHub 项目主页") + ": github.com/loudMore/dsh-launcher",
                Foreground = Palette.Brush(Palette.BlueLight),
                FontSize = 12,
                Cursor = Cursors.Hand,
                Margin = new Thickness(2, 14, 0, 0),
                TextDecorations = TextDecorations.Underline
            };
            gh.MouseLeftButtonUp += delegate { try { Process.Start("https://github.com/loudMore/dsh-launcher"); } catch { } };
            stack.Children.Add(gh);
            scroll.Content = stack;
            return pg;
        }

        System.Windows.Controls.ComboBox setLang;

        void AddSettingRow(Grid grid, int row, string label, string value, string key)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var lbl = new TextBlock { Text = label, Foreground = Palette.Brush(Palette.Text), FontSize = 13, Margin = new Thickness(0, 9, 0, 0) };
            var box = new System.Windows.Controls.TextBox
            {
                Text = value, FontSize = 13, Height = 30, Margin = new Thickness(0, 6, 0, 0),
                Background = Palette.Brush(Palette.BgInput), Foreground = Palette.Brush(Palette.Text),
                BorderThickness = new Thickness(0), Padding = new Thickness(8, 0, 8, 0)
            };
            Grid.SetRow(lbl, row);
            Grid.SetRow(box, row);
            Grid.SetColumn(box, 1);
            grid.Children.Add(lbl);
            grid.Children.Add(box);
            setBoxes[key] = box;
        }

        void SaveSettings()
        {
            int port;
            if (int.TryParse(setBoxes["port"].Text.Trim(), out port)) dsh.Cfg.Port = port;
            dsh.Cfg.DshHome = setBoxes["home"].Text.Trim();
            dsh.Cfg.PluginsRoot = setBoxes["plugins"].Text.Trim();
            dsh.Cfg.LogDir = setBoxes["log"].Text.Trim();
            dsh.Cfg.NpmPackage = setBoxes["npm"].Text.Trim();
            dsh.Cfg.LauncherUpdateUrl = setBoxes["lup"].Text.Trim();
            dsh.Cfg.Proxy = setBoxes["proxy"].Text.Trim();
            dsh.Cfg.Language = setLang.SelectedIndex == 1 ? "zh" : (setLang.SelectedIndex == 2 ? "en" : "");
            dsh.Cfg.ApplyDefaults();
            if (dsh.Cfg.Save())
            {
                sbText.Text = "设置已保存";
                MessageBox.Show(this, "设置已保存。\n\n端口/路径等改动在下次启动服务时生效。", "设置", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else MessageBox.Show(this, "设置保存失败（配置文件可能被占用或无权限）。", "设置", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        void AutoFillSettings()
        {
            sbText.Text = "正在检测…";
            var t = new Thread(delegate()
            {
                var env = dsh.DetectEnvironment();
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    dsh.Env = env;
                    if (!string.IsNullOrEmpty(env.NodePath)) setBoxes["home"].Text = dsh.Cfg.DshHome;
                    if (Directory.Exists(dsh.Cfg.PluginsRoot)) setBoxes["plugins"].Text = dsh.Cfg.PluginsRoot;
                    RenderEnv();
                    RenderOverview();
                    sbText.Text = "检测完成";
                }));
            });
            t.IsBackground = true;
            t.Start();
        }

        // ---------- 输入对话框 ----------
        string Prompt(string title, string message, string def)
        {
            var w = new Window
            {
                Title = title, Width = 440, Height = 190, WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this, WindowStyle = WindowStyle.ToolWindow, ResizeMode = ResizeMode.NoResize,
                Background = Palette.Brush(Palette.Bg)
            };
            var g = new Grid { Margin = new Thickness(16) };
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var msg = new TextBlock { Text = message, Foreground = Palette.Brush(Palette.TextDim), FontSize = 13, TextWrapping = TextWrapping.Wrap };
            var box = new System.Windows.Controls.TextBox { Text = def, FontSize = 13, VerticalContentAlignment = VerticalAlignment.Center, Background = Palette.Brush(Palette.BgInput), Foreground = Palette.Brush(Palette.Text), BorderThickness = new Thickness(0), Padding = new Thickness(8, 0, 8, 0) };
            Grid.SetRow(msg, 0);
            Grid.SetRow(box, 1);
            var ok = new Button { Content = "确定", Width = 90, Height = 32, Background = Palette.Brush(Palette.Blue), Foreground = Brushes.White, BorderThickness = new Thickness(0), HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 0, 10, 0) };
            var cancel = new Button { Content = "取消", Width = 90, Height = 32, HorizontalAlignment = HorizontalAlignment.Right, Background = Palette.Brush(Palette.BgInput), Foreground = Brushes.White, BorderThickness = new Thickness(0) };
            var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            row.Children.Add(ok);
            row.Children.Add(cancel);
            Grid.SetRow(row, 2);
            g.Children.Add(msg);
            g.Children.Add(box);
            g.Children.Add(row);
            w.Content = g;
            ok.Click += delegate { w.DialogResult = true; };
            cancel.Click += delegate { w.DialogResult = false; };
            w.Loaded += delegate { box.Focus(); box.SelectAll(); };
            return w.ShowDialog() == true ? box.Text : null;
        }

        // ---------- 托盘 / 单实例 / 关闭 ----------
        void InitTray()
        {
            try
            {
                tray = new System.Windows.Forms.NotifyIcon();
                tray.Icon = System.Drawing.Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location);
                tray.Text = "DeepSeek Harness " + Lang.T("启动器");
                tray.Visible = true;
                tray.DoubleClick += delegate { ShowMain(); };
                var menu = new System.Windows.Forms.ContextMenuStrip();
                menu.Items.Add("打开启动器", null, delegate { ShowMain(); });
                menu.Items.Add("退出", null, delegate { quitting = true; Close(); Application.Current.Shutdown(); });
                tray.ContextMenuStrip = menu;
            }
            catch { }
        }

        void ShowMain()
        {
            Show();
            Activate();
            if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        }

        void StartReopenWatch()
        {
            var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            t.Tick += delegate
            {
                try
                {
                    if (File.Exists(Proc.ReopenFlagPath()))
                    {
                        File.Delete(Proc.ReopenFlagPath());
                        ShowMain();
                    }
                }
                catch { }
            };
            t.Start();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            base.OnClosing(e);
            if (!quitting)
            {
                e.Cancel = true;   // 关闭按钮 → 最小化到托盘
                Hide();
            }
        }

        // ---------- 切页: 淡入过渡 (WPF 合成器播放, GPU 平滑) ----------
        void SwitchPage(int idx)
        {
            for (int i = 0; i < navs.Count; i++) navs[i].Active = (i == idx);
            if (host.Children.Count > 0 && host.Children[0] == pages[idx]) return;
            var page = pages[idx];
            host.Children.Clear();
            host.Children.Add(page);
            if (idx == 0) RenderOverview();
            else if (idx == 1) RenderEnv();
            else if (idx == 2) RenderPlugins();
            else if (idx == 3) RenderUpdate();
            else if (idx == 4) RefreshLog();
            if (IsLoaded)
            {
                // 仅窗口加载完成后播放动画; 未加载时直接整帧呈现, 避免停在透明
                page.Opacity = 0.0;
                var anim = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(170));
                anim.EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut };
                page.BeginAnimation(UIElement.OpacityProperty, anim);
            }
            else
            {
                page.Opacity = 1.0;
            }
        }
    }
}
