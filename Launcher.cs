// DeepSeek Harness Launcher (v1.4.5)
// 傻瓜式启动器: 左侧导航 + 卡片式内容 + 底部状态栏
//   概览(一键启动/停止/重启) / 环境 / 插件 / 更新 / 日志 / 设置
// 响应式: 运行时读取系统 DPI, 全部尺寸等比适配; 窗口按屏幕工作区自动计算
// 附加: 品牌过渡动画、托盘、单实例、无边框拖拽缩放、一键安装/升级/修复、中英切换、镜像兜底、GitHub 自更新
// 配置: <exe目录>/launcher.json (不存在则用默认值)
// 编译: csc /target:winexe /platform:anycpu /optimize+
//   /resource:deepseek_logo.png,DeepSeekHarness.logo.png /win32icon:deepseek.ico /win32manifest:app.manifest
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;

namespace DeepSeekHarness
{
    static class Program
    {
        static Mutex singletonMutex;

        // ---- 调试日志接口: 设置环境变量 DSH_LAUNCHER_DEBUG=1 后, 关键流程/命令调用全部写入
        //      <exe目录>/launcher-debug.log (带时间戳与耗时), 供排障与自动化测试使用
        public static bool DebugMode = false;

        public static void DLog(string tag, string msg)
        {
            try
            {
                File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "launcher-debug.log"),
                    DateTime.Now.ToString("HH:mm:ss.fff") + " [" + tag + "] " + msg + "\r\n");
            }
            catch { }
        }

        [DllImport("shell32.dll")]
        static extern int SetCurrentProcessExplicitAppUserModelID([MarshalAs(UnmanagedType.LPWStr)] string AppID);

        [STAThread]
        static void Main()
        {
            // 全局异常兜底: 未捕获异常写入 crash.log, 便于排查
            AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e)
            {
                try { File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log"), DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + e.ExceptionObject + "\r\n"); } catch { }
            };
            Application.ThreadException += delegate(object s, ThreadExceptionEventArgs e)
            {
                try { File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log"), DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + e.Exception + "\r\n"); } catch { }
            };

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            // TLS 1.2: GitHub/npm 等站点最低要求 (老 .NET 默认仅 SSL3/TLS1.0, 会导致 api.github.com 握手失败)
            try { ServicePointManager.SecurityProtocol = (SecurityProtocolType)(3072 | 768 | 192); } catch { }
            // 固定任务栏分组 ID, 保证任务栏/固定图标显示 exe 图标(DeepSeek 鲸鱼)
            try { SetCurrentProcessExplicitAppUserModelID("DeepSeekHarness.Launcher.1"); } catch { }
            try { DebugMode = Environment.GetEnvironmentVariable("DSH_LAUNCHER_DEBUG") == "1"; } catch { }
            if (DebugMode)
                DLog("boot", "debug mode on; cwd=" + Environment.CurrentDirectory + "; PATH=" + Environment.GetEnvironmentVariable("PATH"));

            // 单实例: 已在运行则发信号让旧窗口弹出, 自己退出
            bool createdNew;
            singletonMutex = new Mutex(true, "DeepSeekHarness.Launcher.Singleton.v1", out createdNew);
            if (!createdNew)
            {
                try { File.WriteAllText(ReopenFlagPath(), "1"); } catch { }
                return;
            }

            using (var splash = new SplashForm())
                Application.Run(splash);

            Application.Run(new LauncherForm());

            GC.KeepAlive(singletonMutex);
        }

        public static string ReopenFlagPath()
        {
            return Path.Combine(Path.GetTempPath(), "dsh-launcher-reopen.flag");
        }

        public static string RunCaptureStatic(string program, string args, int timeoutMs)
        {
            // 并发排空 stdout/stderr, 避免管道缓冲写满导致子进程挂死
            try
            {
                var psi = new ProcessStartInfo(program, args)
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                using (var p = Process.Start(psi))
                {
                    var sb = new StringBuilder();
                    p.OutputDataReceived += delegate(object o, DataReceivedEventArgs e) { if (e.Data != null) sb.AppendLine(e.Data); };
                    p.ErrorDataReceived += delegate(object o, DataReceivedEventArgs e) { if (e.Data != null) sb.AppendLine(e.Data); };
                    p.BeginOutputReadLine();
                    p.BeginErrorReadLine();
                    if (!p.WaitForExit(timeoutMs)) { try { p.Kill(); } catch { } return null; }
                    p.WaitForExit();
                    if (p.ExitCode != 0) return null;   // 命令失败(如 where 找不到)视为失败, 防止把错误提示当结果
                    return sb.ToString();
                }
            }
            catch { return null; }
        }

        public static GraphicsPath RoundRectPath(Rectangle r, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        // 加载嵌入 PNG 资源, 失败返回 null (调用方决定回退策略)
        public static Bitmap LoadEmbeddedPng(string name)
        {
            try
            {
                var asm = Assembly.GetExecutingAssembly();
                using (var s = asm.GetManifestResourceStream(name))
                    if (s != null) return new Bitmap(s);
            }
            catch { }
            return null;
        }

        // 按位图原始宽高比等比缩放, 居中绘制到 box 内 (高质量插值, 不拉伸变形)
        public static void DrawWhaleFit(Graphics g, Bitmap bmp, Rectangle box)
        {
            if (bmp == null || box.Width < 2 || box.Height < 2) return;
            double aspect = (double)bmp.Width / bmp.Height;
            int w = box.Width, h = (int)Math.Round(w / aspect);
            if (h > box.Height) { h = box.Height; w = (int)Math.Round(h * aspect); }
            int x = box.X + (box.Width - w) / 2, y = box.Y + (box.Height - h) / 2;
            var old = g.InterpolationMode;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(bmp, x, y, w, h);
            g.InterpolationMode = old;
        }

        // 自绘控件的背景擦除: 沿父链找到第一个有效背景 (RoundPanel 垂直渐变或实心 BackColor),
        // 在父控件坐标系中重建该背景切片并平移回本控件 —— 与父背景无缝衔接。
        // 绝不依赖框架的"透明背景合成"(它会从不可靠的父级重绘路径取像素, 产生残影)。
        public static void EraseWithParentBackground(Control c, Graphics g)
        {
            int offX = c.Left, offY = c.Top;
            Control p = c.Parent;
            while (p != null)
            {
                var rr = p as RoundPanel;
                if (rr != null && rr.GradTop != Color.Empty &&
                    rr.ClientRectangle.Width > 0 && rr.ClientRectangle.Height > 0)
                {
                    using (var br = new LinearGradientBrush(p.ClientRectangle, rr.GradTop, rr.GradBottom, 90f))
                    {
                        var old = g.Transform;
                        g.TranslateTransform(-offX, -offY);
                        g.FillRectangle(br, new Rectangle(offX, offY, c.Width, c.Height));
                        g.Transform = old;
                    }
                    return;
                }
                if (p.BackColor.A == 255)
                {
                    using (var b = new SolidBrush(p.BackColor))
                        g.FillRectangle(b, c.ClientRectangle);
                    return;
                }
                offX += p.Left; offY += p.Top;
                p = p.Parent;
            }
            using (var fb = new SolidBrush(DshTheme.Bg))
                g.FillRectangle(fb, c.ClientRectangle);
        }
    }

    // ---------- 品牌过渡画面 (按系统缩放自动等比适配) ----------
    class SplashForm : Form
    {
        Timer animTimer;
        Timer closeTimer;
        Bitmap logo;
        Bitmap whale;
        bool logoOk;
        float S = 1f;
        int startTick;

        public SplashForm()
        {
            // 自动获取系统缩放系数, 不写死任何数值
            try
            {
                using (var g = CreateGraphics())
                {
                    S = g.DpiX / 96f;
                    if (S < 0.8f || S > 4f) S = 1f;
                }
            }
            catch { S = 1f; }
            startTick = Environment.TickCount;

            Text = "DeepSeek Harness";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ClientSize = new Size((int)Math.Round(440 * S), (int)Math.Round(300 * S));
            // 按屏幕工作区手动居中(物理像素), 规避高 DPI 下 CenterScreen 偏移到左上角的问题
            try
            {
                Rectangle wa = Screen.PrimaryScreen.WorkingArea;
                Location = new Point(wa.Left + (wa.Width - Width) / 2, wa.Top + (wa.Height - Height) / 2);
            }
            catch { }
            BackColor = DshTheme.Bg;
            DoubleBuffered = true;
            ShowInTaskbar = false;

            // 显式绑定图标(过渡画面一般不显示任务栏, 保险起见)
            try { Icon = new Icon(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "deepseek.ico")); }
            catch { }

            whale = Program.LoadEmbeddedPng("DeepSeekHarness.whale-white.png");
            try
            {
                var asm = Assembly.GetExecutingAssembly();
                using (var s = asm.GetManifestResourceStream("DeepSeekHarness.logo.png"))
                    if (s != null) { logo = new Bitmap(s); logoOk = true; }
            }
            catch { logoOk = false; }

            animTimer = new Timer { Interval = 16 };
            animTimer.Tick += delegate { Invalidate(); };
            animTimer.Start();

            closeTimer = new Timer { Interval = 1500 };
            closeTimer.Tick += delegate
            {
                closeTimer.Stop();
                animTimer.Stop();
                var step = new Timer { Interval = 20 };
                step.Tick += delegate
                {
                    Opacity -= 0.09;
                    if (Opacity <= 0.03) { step.Stop(); Close(); }
                };
                step.Start();
            };
            closeTimer.Start();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (Width > 4 && Height > 4)
                Region = new Region(Program.RoundRectPath(new Rectangle(0, 0, Width, Height), (int)Math.Round(18 * S)));
        }

        static float EaseOutBack(float x)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float t = x - 1f;
            return 1f + c3 * t * t * t + c1 * t * t;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            int w = ClientSize.Width, h = ClientSize.Height;

            // 背景渐变 (深层海军蓝)
            using (var bg = new LinearGradientBrush(new Rectangle(0, 0, w, h),
                Color.FromArgb(255, 16, 21, 38), Color.FromArgb(255, 11, 14, 22), 90f))
                g.FillRectangle(bg, 0, 0, w, h);
            using (var br = new LinearGradientBrush(new Rectangle(0, 0, w, (int)Math.Round(3 * S)),
                DshTheme.Blue, DshTheme.BlueLight, 0f))
                g.FillRectangle(br, 0, 0, w, (int)Math.Round(3 * S));

            // Logo 卡片: 弹性缩放入场
            float k = Math.Min(1f, (Environment.TickCount - startTick) / 450f);
            float ease = EaseOutBack(k);
            int baseSize = (int)Math.Round(96 * S);
            int size = (int)Math.Round(baseSize * (0.86f + 0.14f * ease));
            int pad = (int)Math.Round(10 * S);

            // 将 (Logo卡 + 标题 + 副标题) 视为一个整体, 在窗口上部 ~78% 区域内垂直居中
            // (底部进度条位置不变), 避免旧版顶重脚轻的构图
            string title = "DeepSeek Harness";
            string sub = "正在启动 · 请稍候";
            SizeF szTitle, szSub;
            using (var f = new Font("Microsoft YaHei UI", 15f, FontStyle.Bold))
                szTitle = g.MeasureString(title, f);
            using (var f2 = new Font("Microsoft YaHei UI", 9f))
                szSub = g.MeasureString(sub, f2);
            int gapLogoTitle = (int)Math.Round(14 * S);
            int gapTitleSub = (int)Math.Round(6 * S);
            int groupH = baseSize + gapLogoTitle + (int)Math.Ceiling(szTitle.Height) + gapTitleSub + (int)Math.Ceiling(szSub.Height);
            int regionH = (int)Math.Round(h * 0.78);
            int groupTop = Math.Max((int)Math.Round(12 * S), (regionH - groupH) / 2);
            int cx = (w - size) / 2, cy = groupTop + (baseSize - size) / 2;

            // 光晕 + 投影
            int glowIn = (int)Math.Round(14 * S);
            for (int i = 3; i >= 1; i--)
            {
                int off = (int)Math.Round((5 + i * 2) * S);
                using (var p = Program.RoundRectPath(new Rectangle(cx - glowIn / i, cy + off, size + glowIn * 2 / i, size + glowIn * 2 / i), (int)Math.Round(24 * S)))
                using (var b = new SolidBrush(Color.FromArgb(14 - i * 3, 60, 90, 220)))
                    g.FillPath(b, p);
            }

            // 渐变圆角卡片 (品牌蓝对角渐变 + 白色鲸鱼剪影, 同色系图标化处理)
            using (var path = Program.RoundRectPath(new Rectangle(cx, cy, size, size), (int)Math.Round(20 * S)))
            {
                using (var card = new LinearGradientBrush(new Rectangle(cx, cy, size, size), DshTheme.Blue, DshTheme.BlueDark, 45f))
                    g.FillPath(card, path);
                using (var pen = new Pen(Color.FromArgb(90, 255, 255, 255), 1f))
                    g.DrawPath(pen, path);
            }
            if (whale != null)
                Program.DrawWhaleFit(g, whale, new Rectangle(cx + pad, cy + pad, size - pad * 2, size - pad * 2));
            else if (logoOk && logo != null)
                g.DrawImage(logo, cx + pad, cy + pad, size - pad * 2, size - pad * 2);

            // 标题 + 副标题 (位置由上方整体居中计算给出)
            int titleY = groupTop + baseSize + gapLogoTitle;
            using (var f = new Font("Microsoft YaHei UI", 15f, FontStyle.Bold))
            using (var b = new SolidBrush(Color.White))
                g.DrawString(title, f, b, (w - szTitle.Width) / 2, titleY);
            using (var f2 = new Font("Microsoft YaHei UI", 9f))
            using (var b2 = new SolidBrush(DshTheme.TextDim))
                g.DrawString(sub, f2, b2, (w - szSub.Width) / 2, titleY + szTitle.Height + gapTitleSub);

            // 底部圆角进度条
            int barW = w - (int)Math.Round(120 * S), barH = Math.Max(3, (int)Math.Round(5 * S)), barY = h - (int)Math.Round(32 * S);
            int x0 = (w - barW) / 2;
            int r2 = barH / 2;
            using (var track = Program.RoundRectPath(new Rectangle(x0, barY, barW, barH), r2))
            using (var tb = new SolidBrush(DshTheme.Track))
                g.FillPath(tb, track);
            int fillW = Math.Max((int)Math.Round(46 * S), barW / 3);
            float t = (Environment.TickCount % 2000) / 2000f;
            float pos = (float)(Math.Sin(t * Math.PI * 2) * 0.5 + 0.5);
            int slide = (int)(pos * (barW - fillW));
            using (var fill = Program.RoundRectPath(new Rectangle(x0 + slide, barY, fillW, barH), r2))
            using (var fb = new LinearGradientBrush(new Rectangle(x0 + slide, barY, fillW, barH), DshTheme.BlueLight, DshTheme.Blue, 0f))
                g.FillPath(fb, fill);
            using (var glow = new SolidBrush(Color.FromArgb(150, 255, 255, 255)))
                g.FillRectangle(glow, x0 + slide + fillW - (int)Math.Round(12 * S), barY, (int)Math.Round(12 * S), barH);
        }
    }

    // ---------- 配置 ----------
    class LauncherConfig
    {
        public int Port = 8099;
        public string DshCommand = "dsh";            // dsh 启动命令(可含参数)
        public string DshHome = "";                  // 数据目录(空=自动)
        public string PluginsRoot = "";              // 插件目录(空=自动)
        public string LogDir = "";                   // 日志目录(空=exe 目录)
        public bool CheckUpdatesOnStart = true;
        public bool AutoStartService = true;
        public bool RestartIfRunning = true;         // 启动时若服务在运行则自动重启
        public bool OpenBrowserOnStart = true;       // 启动成功后自动打开浏览器
        public string NpmPackage = "@deepseek-ai/dsh";
        public string Language = "";                // 界面语言: 空=跟随系统, zh / en
        public string LauncherUpdateUrl = "https://raw.githubusercontent.com/loudMore/dsh-launcher/main/version.txt"; // 启动器自更新检查地址(返回纯版本号)
        public string NpmRegistry = "";              // npm 镜像源(空=官方源, 失败自动回退 npmmirror 国内镜像)
        public string NodePath = "";                 // 手动指定 node.exe(空=自动检测)
        public string NpmPath = "";                  // 手动指定 npm.cmd(空=自动检测)
        public string GitPath = "";                  // 手动指定 git.exe(空=自动检测)
        public string Proxy = "";                    // 代理地址(如 http://127.0.0.1:7890; 空=自动探测本机常见代理端口)

        public static string ConfigPath
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "launcher.json"); }
        }

        public void ApplyDefaults()
        {
            string exeDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
            if (string.IsNullOrEmpty(LogDir)) LogDir = exeDir;
            if (string.IsNullOrEmpty(PluginsRoot)) PluginsRoot = Path.Combine(exeDir, "plugins");
            if (string.IsNullOrEmpty(DshHome))
            {
                string env = Environment.GetEnvironmentVariable("DSH_HOME");
                if (!string.IsNullOrEmpty(env)) DshHome = env;
                else
                {
                    // 标准约定: 优先用户目录 ~/.dsh (与 dsh 官方默认一致), 失败才退回 exe 目录
                    try
                    {
                        string userDsh = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dsh");
                        DshHome = userDsh;
                    }
                    catch { DshHome = Path.Combine(exeDir, "dsh-home"); }
                }
            }
        }

        public static LauncherConfig Load()
        {
            var cfg = new LauncherConfig();
            try
            {
                if (File.Exists(ConfigPath))
                {
                    string json = File.ReadAllText(ConfigPath);
                    cfg.Port = IntOf(Get(json, "port"), cfg.Port);
                    cfg.DshCommand = StrOf(Unesc(Get(json, "dshCommand")), cfg.DshCommand);
                    cfg.DshHome = StrOf(Unesc(Get(json, "dshHome")), cfg.DshHome);
                    cfg.PluginsRoot = StrOf(Unesc(Get(json, "pluginsRoot")), cfg.PluginsRoot);
                    cfg.LogDir = StrOf(Unesc(Get(json, "logDir")), cfg.LogDir);
                    cfg.CheckUpdatesOnStart = BoolOf(Get(json, "checkUpdatesOnStart"), cfg.CheckUpdatesOnStart);
                    cfg.AutoStartService = BoolOf(Get(json, "autoStartService"), cfg.AutoStartService);
                    cfg.RestartIfRunning = BoolOf(Get(json, "restartIfRunning"), cfg.RestartIfRunning);
                    cfg.OpenBrowserOnStart = BoolOf(Get(json, "openBrowserOnStart"), cfg.OpenBrowserOnStart);
                    cfg.NpmPackage = StrOf(Unesc(Get(json, "npmPackage")), cfg.NpmPackage);
                    cfg.Language = StrOf(Unesc(Get(json, "language")), cfg.Language);
                    cfg.LauncherUpdateUrl = StrOf(Unesc(Get(json, "launcherUpdateUrl")), cfg.LauncherUpdateUrl);
                    cfg.NpmRegistry = StrOf(Unesc(Get(json, "npmRegistry")), cfg.NpmRegistry);
                    cfg.NodePath = StrOf(Unesc(Get(json, "nodePath")), cfg.NodePath);
                    cfg.NpmPath = StrOf(Unesc(Get(json, "npmPath")), cfg.NpmPath);
                    cfg.GitPath = StrOf(Unesc(Get(json, "gitPath")), cfg.GitPath);
                    cfg.Proxy = StrOf(Unesc(Get(json, "proxy")), cfg.Proxy);
                }
            }
            catch { }
            cfg.ApplyDefaults();
            return cfg;
        }

        public bool Save()
        {
            try
            {
                string json = string.Format(
                    "{{\"port\":{0},\"dshCommand\":\"{1}\",\"dshHome\":\"{2}\",\"pluginsRoot\":\"{3}\",\"logDir\":\"{4}\",\"checkUpdatesOnStart\":{5},\"autoStartService\":{6},\"restartIfRunning\":{7},\"openBrowserOnStart\":{8},\"npmPackage\":\"{9}\",\"language\":\"{10}\",\"launcherUpdateUrl\":\"{11}\",\"npmRegistry\":\"{12}\",\"nodePath\":\"{13}\",\"npmPath\":\"{14}\",\"gitPath\":\"{15}\",\"proxy\":\"{16}\"}}",
                    Port, JsonEsc(DshCommand), JsonEsc(DshHome), JsonEsc(PluginsRoot), JsonEsc(LogDir),
                    CheckUpdatesOnStart ? "true" : "false", AutoStartService ? "true" : "false",
                    RestartIfRunning ? "true" : "false", OpenBrowserOnStart ? "true" : "false",
                    JsonEsc(NpmPackage), JsonEsc(Language), JsonEsc(LauncherUpdateUrl), JsonEsc(NpmRegistry),
                    JsonEsc(NodePath), JsonEsc(NpmPath), JsonEsc(GitPath), JsonEsc(Proxy));
                File.WriteAllText(ConfigPath, json);
                return true;
            }
            catch { return false; }
        }

        static string Get(string json, string key)
        {
            Match m = Regex.Match(json, "\"" + key + "\"\\s*:\\s*\"?([^\",}]+)\"?");
            return m.Success ? m.Groups[1].Value : null;
        }
        static string StrOf(string v, string def) { return string.IsNullOrEmpty(v) ? def : v; }
        static int IntOf(string v, int def) { int r; return int.TryParse(v, out r) ? r : def; }
        static bool BoolOf(string v, bool def) { bool r; return bool.TryParse(v, out r) ? r : def; }
        static string JsonEsc(string s) { return s.Replace("\\", "\\\\").Replace("\"", "\\\""); }
        // JsonEsc 的逆操作: 左到右扫描, \\ → \, \" → " (修复旧版只转义不反转义导致的反斜杠翻倍)
        static string Unesc(string s)
        {
            if (string.IsNullOrEmpty(s) || s.IndexOf('\\') < 0) return s;
            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] == '\\' && i + 1 < s.Length && (s[i + 1] == '\\' || s[i + 1] == '"'))
                {
                    sb.Append(s[i + 1]);
                    i++;
                }
                else sb.Append(s[i]);
            }
            return sb.ToString();
        }
    }

    // ---------- 环境检测结果 ----------
    class EnvInfo
    {
        public string DshPath = "";
        public string DshVersion = "";
        public string NpmPath = "";
        public string NpmVersion = "";
        public string GitPath = "";
        public string GitVersion = "";
        public string NodePath = "";
        public string NodeVersion = "";
        public bool DshHomeExists;
        public int PluginDirs;
        public int PluginGitRepos;
    }

    // ---------- 更新信息 ----------
    class UpdateInfo
    {
        public bool HasUpdate;
        public bool DshUpdate;
        public string DshCurrent = "";
        public string DshLatest = "";
        public int PluginCount;
        public string PluginNames = "";
        public string Detail = "";
    }

    // ---------- 插件条目 ----------
    class PluginItem
    {
        public string Name = "";
        public string Path = "";
        public bool IsGit;
        public bool Disabled;
        public string RemoteUrl = "";
        public string Branch = "";
    }

    // ---------- 商城条目 ----------
    class StoreItem
    {
        public string Name = "";
        public string FullName = "";
        public string Url = "";
        public string Desc = "";
        public int Stars;
        public string Lang = "";
        public string Branch = "";   // 默认分支 (GitHub API 提供; 精选列表兜底时为空)
        public string Pushed = "";   // 最近推送日期 YYYY-MM-DD (GitHub API 提供)
    }

    // ---------- 矢量图标 (GDI 绘制, 无需图片资源) ----------
    static class Glyphs
    {
        // 0 home 1 sliders 2 box 3 refresh 4 doc 5 gear 6 terminal 7 plug 8 folder 9 cloud 10 globe
        public static void Draw(Graphics g, int id, Rectangle r, Color c)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var pen = new Pen(c, Math.Max(1.2f, r.Width / 14f)))
            using (var b = new SolidBrush(c))
            {
                float x = r.X, y = r.Y, w = r.Width, h = r.Height;
                switch (id)
                {
                    case 0: // home
                        g.DrawLine(pen, x + w * 0.5f, y + h * 0.12f, x + w * 0.12f, y + h * 0.45f);
                        g.DrawLine(pen, x + w * 0.5f, y + h * 0.12f, x + w * 0.88f, y + h * 0.45f);
                        g.DrawRectangle(pen, x + w * 0.22f, y + h * 0.42f, w * 0.56f, h * 0.46f);
                        break;
                    case 1: // sliders (环境)
                        g.DrawLine(pen, x + w * 0.1f, y + h * 0.3f, x + w * 0.9f, y + h * 0.3f);
                        g.FillEllipse(b, x + w * 0.55f, y + h * 0.18f, w * 0.24f, h * 0.24f);
                        g.DrawLine(pen, x + w * 0.1f, y + h * 0.68f, x + w * 0.9f, y + h * 0.68f);
                        g.FillEllipse(b, x + w * 0.25f, y + h * 0.56f, w * 0.24f, h * 0.24f);
                        break;
                    case 2: // box (插件)
                        g.DrawLine(pen, x + w * 0.5f, y + h * 0.08f, x + w * 0.1f, y + h * 0.24f);
                        g.DrawLine(pen, x + w * 0.5f, y + h * 0.08f, x + w * 0.9f, y + h * 0.24f);
                        g.DrawLine(pen, x + w * 0.1f, y + h * 0.24f, x + w * 0.1f, y + h * 0.88f);
                        g.DrawLine(pen, x + w * 0.9f, y + h * 0.24f, x + w * 0.9f, y + h * 0.88f);
                        g.DrawLine(pen, x + w * 0.1f, y + h * 0.88f, x + w * 0.9f, y + h * 0.88f);
                        g.DrawLine(pen, x + w * 0.5f, y + h * 0.08f, x + w * 0.5f, y + h * 0.88f);
                        break;
                    case 3: // refresh (更新)
                        g.DrawArc(pen, x + w * 0.18f, y + h * 0.14f, w * 0.64f, h * 0.72f, -60, 300);
                        g.DrawLine(pen, x + w * 0.74f, y + h * 0.08f, x + w * 0.86f, y + h * 0.2f);
                        g.DrawLine(pen, x + w * 0.74f, y + h * 0.08f, x + w * 0.58f, y + h * 0.2f);
                        break;
                    case 4: // doc (日志)
                        g.DrawRectangle(pen, x + w * 0.2f, y + h * 0.08f, w * 0.6f, h * 0.84f);
                        for (int i = 0; i < 3; i++)
                            g.DrawLine(pen, x + w * 0.32f, y + h * (0.3f + 0.16f * i), x + w * 0.68f, y + h * (0.3f + 0.16f * i));
                        break;
                    case 5: // gear (设置)
                        float cx = x + w / 2f, cy = y + h / 2f, ro = w * 0.42f, ri = w * 0.18f;
                        for (int i = 0; i < 8; i++)
                        {
                            double a = i * Math.PI / 4.0;
                            g.DrawLine(pen,
                                (float)(cx + Math.Cos(a) * ri), (float)(cy + Math.Sin(a) * ri),
                                (float)(cx + Math.Cos(a) * ro), (float)(cy + Math.Sin(a) * ro));
                        }
                        g.DrawEllipse(pen, cx - ro, cy - ro, ro * 2, ro * 2);
                        g.FillEllipse(b, cx - ri, cy - ri, ri * 2, ri * 2);
                        break;
                    case 6: // terminal (dsh 命令)
                        g.DrawRectangle(pen, x + w * 0.1f, y + h * 0.16f, w * 0.8f, h * 0.68f);
                        g.DrawLine(pen, x + w * 0.22f, y + h * 0.38f, x + w * 0.4f, y + h * 0.5f);
                        g.DrawLine(pen, x + w * 0.4f, y + h * 0.5f, x + w * 0.22f, y + h * 0.62f);
                        g.DrawLine(pen, x + w * 0.5f, y + h * 0.62f, x + w * 0.72f, y + h * 0.62f);
                        break;
                    case 7: // plug (端口)
                        g.DrawLine(pen, x + w * 0.28f, y + h * 0.28f, x + w * 0.72f, y + h * 0.72f);
                        g.DrawArc(pen, x + w * 0.08f, y + h * 0.08f, w * 0.44f, h * 0.44f, -60, 220);
                        g.DrawArc(pen, x + w * 0.48f, y + h * 0.48f, w * 0.44f, h * 0.44f, 120, 220);
                        break;
                    case 8: // folder (目录)
                        g.DrawRectangle(pen, x + w * 0.08f, y + h * 0.3f, w * 0.84f, h * 0.6f);
                        g.DrawRectangle(pen, x + w * 0.08f, y + h * 0.24f, w * 0.4f, h * 0.16f);
                        break;
                    case 9: // cloud (更新源)
                        g.DrawArc(pen, x + w * 0.15f, y + h * 0.35f, w * 0.5f, h * 0.42f, 180, 180);
                        g.DrawArc(pen, x + w * 0.4f, y + h * 0.2f, w * 0.45f, h * 0.5f, 160, 200);
                        g.DrawLine(pen, x + w * 0.28f, y + h * 0.85f, x + w * 0.72f, y + h * 0.85f);
                        break;
                    case 10: // globe (语言)
                        g.DrawEllipse(pen, x + w * 0.12f, y + h * 0.12f, w * 0.76f, h * 0.76f);
                        g.DrawEllipse(pen, x + w * 0.28f, y + h * 0.12f, w * 0.44f, h * 0.76f);
                        g.DrawLine(pen, x + w * 0.12f, y + h * 0.5f, x + w * 0.88f, y + h * 0.5f);
                        break;
                    case 11: // store (商城/购物袋)
                        g.DrawArc(pen, x + w * 0.22f, y + h * 0.08f, w * 0.28f, h * 0.28f, 180, 180);
                        g.DrawArc(pen, x + w * 0.5f, y + h * 0.08f, w * 0.28f, h * 0.28f, 180, 180);
                        g.DrawRectangle(pen, x + w * 0.12f, y + h * 0.24f, w * 0.76f, h * 0.68f);
                        break;
                }
            }
        }
    }

    // ---------- 多语言 (跟随系统 / zh / en) ----------
    static class Lang
    {
        public static string Code = "zh";

        static string SystemLang()
        {
            try
            {
                string name = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
                if (name == "zh") return "zh";
            }
            catch { }
            try
            {
                string name = System.Globalization.CultureInfo.InstalledUICulture.TwoLetterISOLanguageName;
                if (name == "zh") return "zh";
            }
            catch { }
            return "en";
        }

        internal static string Resolve(string code)
        {
            if (code == "zh") return "zh";
            if (code == "en") return "en";
            return SystemLang();
        }

        static Dictionary<string, string> en = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "概览", "Overview" }, { "环境", "Environment" }, { "插件", "Plugins" }, { "更新", "Updates" }, { "日志", "Logs" }, { "设置", "Settings" }, { "商城", "Store" },
            { "DSH 启动器", "DSH Launcher" },
            { "刷新列表", "Refresh" }, { "安装插件", "Install Plugin" }, { "全部更新", "Update All" }, { "修复依赖", "Fix Deps" }, { "一键维护", "Maintain" },
            { "重新检测", "Re-detect" }, { "一键安装 / 修复环境", "Install / Repair" },
            { "已安装插件", "Installed Plugins" }, { "运行环境", "Environment" }, { "插件管理", "Plugin Manager" }, { "更新与升级", "Updates" }, { "日志查看", "Logs" },
            { "已装工具", "Installed Tools" }, { "关键目录", "Key Directories" }, { "插件更新", "Plugin Updates" }, { "日志内容", "Log Content" },
            { "服务运行中", "Service Running" }, { "服务未启动", "Service Stopped" }, { "正在初始化…", "Initializing…" },
            { "未检测到 Node.js", "Node.js not found" }, { "未检测到 dsh", "dsh not found" },
            { "一键启动", "Start" }, { "一键安装", "Install" }, { "打开浏览器", "Open Browser" }, { "停止服务", "Stop" }, { "重启服务", "Restart" },
            { "检查更新", "Check Updates" }, { "立即升级", "Upgrade Now" }, { "下载新版本", "Download" },
            { "当前版本", "Current" }, { "最新版本", "Latest" }, { "未检查", "Not checked" },
            { "打开目录", "Open Folder" }, { "目录", "Folder" }, { "卸载", "Remove" },
            { "插件商城", "Plugin Store" }, { "获取列表", "Fetch List" }, { "打开网页", "Open Web" }, { "可安装插件", "Installable Plugins" },
            { "预览", "Preview" }, { "搜索插件…", "Search plugins…" },
            { "缓存", "cache" }, { "按星标排序", "Sort by stars" }, { "按名称排序", "Sort by name" }, { "默认顺序", "Default order" },
            { "全部语言", "All languages" }, { "正在刷新…", "Refreshing…" }, { "浏览", "Browse" },
            { "安装", "Install" }, { "网页", "Web" }, { "数据来自 GitHub 主题 dsh-plugin · 支持搜索筛选", "Data from GitHub topic dsh-plugin" },
            { "正在获取插件列表…", "Fetching plugin list…" }, { "获取失败：需要能访问 GitHub，请检查网络后重试", "Fetch failed: GitHub access required" },
            { "共 {0} 个插件 · 数据来自 GitHub", "{0} plugins from GitHub" }, { "没有匹配的插件，换个关键词试试", "No matching plugins" },
            { "点击「获取列表」加载 GitHub 上的 dsh-plugin 插件", "Click Fetch List to load plugins from GitHub" },
            { "该插件已安装（目录已存在）：\n", "Already installed (directory exists):\n" },
            { "未检测到 Git：请先到「环境」页安装或手动选择 git.exe", "Git not found: install or pick git.exe on the Environment page" },
            { "正在安装插件", "Installing plugin" }, { "克隆失败（网络或地址错误）。\n详见「日志」页。", "Clone failed (network or URL).\nSee the Logs page." },
            { "已安装。\n重启服务后生效；若插件在仓库子目录，请在 DSH 设置中配置挂载。", "Installed.\nRestart to take effect; if the plugin lives in a subdirectory, mount it in DSH settings." },
            { "启用", "Enable" }, { "禁用", "Disable" }, { "已禁用", "Disabled" }, { "已启用插件", "Enabled plugin" }, { "已禁用插件", "Disabled plugin" }, { "操作失败", "Operation failed" },
            { "刷新", "Refresh" }, { "清空当前日志", "Clear Log" }, { "打开日志目录", "Open Log Folder" }, { "自动刷新", "Auto refresh" },
            { "自动检测", "Auto Detect" }, { "保存设置", "Save" }, { "打开配置文件", "Open Config" },
            { "dsh 命令", "dsh Command" }, { "服务端口", "Port" }, { "插件目录", "Plugins Dir" }, { "日志目录", "Log Dir" }, { "npm 包名", "npm Package" },
            { "启动器更新源", "Launcher Update URL" }, { "界面语言", "Language" },
            { "启动时自动检查更新", "Check updates on launch" }, { "启动时自动启动服务", "Auto-start service on launch" },
            { "启动时自动重启旧服务", "Restart old service on launch" }, { "启动成功后自动打开浏览器", "Open browser after launch" },
            { "确定", "OK" }, { "取消", "Cancel" }, { "是", "Yes" }, { "否", "No" },
            { "打开启动器", "Open Launcher" }, { "启动 / 停止服务", "Start / Stop Service" }, { "退出", "Exit" },
            { "正在检测环境…", "Detecting environment…" }, { "正在检查更新…", "Checking updates…" }, { "正在启动服务…", "Starting service…" },
            { "正在停止服务…", "Stopping service…" }, { "正在重启服务…", "Restarting service…" }, { "服务已就绪", "Service ready" },
            { "服务已停止", "Service stopped" }, { "服务已在运行", "Service already running" }, { "启动失败", "Start failed" },
            { "当前已是最新版本", "Already up to date" }, { "更新完成", "Update finished" }, { "环境检测完成", "Environment check done" },
            { "就绪（未自动启动）", "Ready (not auto-started)" }, { "准备就绪", "Ready" },
            { "已是最新版本，无需更新。", "You are up to date." }, { "点击「检查更新」获取最新版本信息。", "Click \"Check Updates\" to fetch the latest version." },
        };

        public static void Set(string code)
        {
            Code = Resolve(code);   // "" = 跟随系统
        }

        public static string T(string zh)
        {
            if (Code == "en")
            {
                string v;
                if (en.TryGetValue(zh, out v)) return v;
            }
            return zh;
        }
    }

    // ---------- DeepSeek 品牌主题 ----------
    static class DshTheme
    {
        public static float S = 1f;                                                   // 系统缩放系数(启动时自动获取)

        public static readonly Color Blue = Color.FromArgb(255, 77, 107, 254);       // #4D6BFE 主品牌色
        public static readonly Color BlueLight = Color.FromArgb(255, 122, 152, 255); // #7A98FF
        public static readonly Color BlueHover = Color.FromArgb(255, 93, 124, 255);  // #5D7CFF 主按钮悬停(提亮)
        public static readonly Color BlueDark = Color.FromArgb(255, 62, 92, 235);    // #3E5CEB 主按钮渐变底
        public static readonly Color BluePressed = Color.FromArgb(255, 53, 80, 212); // #3550D4 主按钮按下
        public static readonly Color Purple = Color.FromArgb(255, 124, 111, 240);    // #7C6FF0 仅品牌时刻少量使用
        public static readonly Color Bg = Color.FromArgb(255, 11, 14, 22);           // #0B0E16 窗口底色
        public static readonly Color BgDeep = Color.FromArgb(255, 13, 17, 32);       // #0D1120 侧栏/标题栏/状态栏
        public static readonly Color BgCard = Color.FromArgb(255, 18, 24, 42);       // #12182A 卡片底部
        public static readonly Color CardTop = Color.FromArgb(255, 25, 32, 56);      // #192038 卡片顶部(微亮渐变)
        public static readonly Color BgInput = Color.FromArgb(255, 26, 33, 64);      // #1A2140 输入区
        public static readonly Color RowHover = Color.FromArgb(255, 31, 40, 71);     // #1F2847 行悬停
        public static readonly Color Border = Color.FromArgb(23, 255, 255, 255);     // 半透明白 9%
        public static readonly Color BorderSoft = Color.FromArgb(15, 255, 255, 255); // 半透明白 6%
        public static readonly Color TextMain = Color.FromArgb(255, 232, 236, 248);  // #E8ECF8
        public static readonly Color TextDim = Color.FromArgb(255, 154, 164, 196);   // #9AA4C4
        public static readonly Color TextFaint = Color.FromArgb(255, 95, 106, 140);  // #5F6A8C 提示/脚注
        public static readonly Color Track = Color.FromArgb(255, 35, 43, 71);        // #232B47 进度条轨道
        public static readonly Color BtnDark = Color.FromArgb(255, 32, 41, 71);      // #202947
        public static readonly Color BtnHover = Color.FromArgb(255, 42, 53, 88);     // #2A3558
        public static readonly Color BtnDown = Color.FromArgb(255, 26, 33, 56);      // #1A2138
        public static readonly Color NavActive = Color.FromArgb(255, 30, 42, 82);    // #1E2A52 导航激活药丸
        public static readonly Color NavHover = Color.FromArgb(255, 22, 29, 56);     // #161D38 导航悬停

        public static readonly Color Success = Color.FromArgb(255, 61, 190, 130);    // #3DBE82
        public static readonly Color Warn = Color.FromArgb(255, 240, 170, 90);       // #F0AA5A
        public static readonly Color Error = Color.FromArgb(255, 240, 96, 96);       // #F06060
        public static readonly Color Info = Color.FromArgb(255, 96, 165, 250);       // #60A5FA
    }

    // ---------- 共享字体工厂 (静态缓存, 避免 ~40 处零散 new Font 的风格漂移与 GDI 句柄泄漏) ----------
    static class DshFonts
    {
        public static readonly Font PageTitle = new Font("Microsoft YaHei UI", 13.5f, FontStyle.Bold); // 页面大标题
        public static readonly Font HeroTitle = new Font("Microsoft YaHei UI", 14f, FontStyle.Bold);   // 概览主状态
        public static readonly Font HeroButton = new Font("Microsoft YaHei UI", 10.5f, FontStyle.Bold);// 概览主按钮
        public static readonly Font CardTitle = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Bold);  // 卡片标题/强调
        public static readonly Font Body = new Font("Microsoft YaHei UI", 9f);                         // 正文
        public static readonly Font BodyBold = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold);     // 正文加粗
        public static readonly Font Small = new Font("Microsoft YaHei UI", 8.5f);                      // 次级说明
        public static readonly Font Caption = new Font("Microsoft YaHei UI", 8f);                      // 说明文字(淡)
        public static readonly Font Mono = new Font("Consolas", 9f);                                   // 路径/日志/版本
        public static readonly Font MonoSmall = new Font("Consolas", 8.5f);
        public static readonly Font Version = new Font("Microsoft YaHei UI", 10f, FontStyle.Bold);     // 版本号
        public static readonly Font VersionPlain = new Font("Microsoft YaHei UI", 10f);
        public static readonly Font Nav = new Font("Microsoft YaHei UI", 9f);                          // 侧栏导航
        public static readonly Font NavActive = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold);
        public static readonly Font TitleBar = new Font("Microsoft YaHei UI", 8.5f);                   // 标题栏文字
        public static readonly Font Glyph = new Font("Segoe UI", 9.5f);                                // 窗口按钮字形
        public static readonly Font Dot = new Font("Segoe UI", 9f);                                    // 状态圆点
        public static readonly Font DotSmall = new Font("Segoe UI", 8f);
        public static readonly Font DotBig = new Font("Segoe UI", 12f);
        public static readonly Font Footer = new Font("Segoe UI", 7.5f);                               // 侧栏脚注版本号
    }

    // ---------- 自绘平滑进度条 (DeepSeek 蓝渐变) ----------
    class DshProgressBar : Panel
    {
        Timer t;
        public DshProgressBar()
        {
            DoubleBuffered = true;
            BackColor = DshTheme.Track;   // 不透明底色, 避免框架用默认浅色擦底
            Height = 4;
            t = new Timer { Interval = 30 };
            t.Tick += delegate { Invalidate(); };
            t.Start();
            Disposed += delegate { t.Dispose(); };
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var track = new SolidBrush(DshTheme.Track))
                g.FillRectangle(track, 0, 0, Width, Height);
            int fillW = Math.Max(40, Width / 3);
            float pos = (float)(Math.Sin(Environment.TickCount / 2000.0 * Math.PI * 2) * 0.5 + 0.5);
            int x = (int)(pos * (Width - fillW));
            using (var br = new LinearGradientBrush(new Rectangle(x, 0, fillW, Height), DshTheme.BlueLight, DshTheme.Blue, 0f))
                g.FillRectangle(br, x, 0, fillW, Height);
            using (var glow = new SolidBrush(Color.FromArgb(140, 255, 255, 255)))
                g.FillRectangle(glow, x + fillW - 12, 0, 12, Height);
        }
    }

    // ---------- 圆角卡片 ----------
    class RoundPanel : Panel
    {
        public int Radius;
        public Color BorderColor = DshTheme.BorderSoft;
        public Color GradTop = Color.Empty;      // 默认使用主题微亮渐变, 可覆盖
        public Color GradBottom = Color.Empty;
        public bool TopAccent;                   // 顶部品牌蓝渐变修饰线 (仅概览主卡)

        public RoundPanel()
        {
            BackColor = DshTheme.BgCard;
            DoubleBuffered = true;
            Radius = (int)Math.Round(12 * DshTheme.S);
            GradTop = DshTheme.CardTop;
            GradBottom = DshTheme.BgCard;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            base.OnPaintBackground(e);
            if (GradTop != Color.Empty)
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var b = new LinearGradientBrush(ClientRectangle, GradTop, GradBottom, 90f))
                using (var p = Program.RoundRectPath(new Rectangle(0, 0, Width - 1, Height - 1), Radius))
                    g.FillPath(b, p);
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (Width > 4 && Height > 4)
            {
                var oldRegion = Region;
                Region = new Region(Program.RoundRectPath(new Rectangle(0, 0, Width, Height), Radius));
                if (oldRegion != null) oldRegion.Dispose();   // 防止 resize 泄漏 GDI 句柄
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var p = Program.RoundRectPath(new Rectangle(0, 0, Width - 1, Height - 1), Radius))
            using (var pen = new Pen(BorderColor, 1f))
                g.DrawPath(pen, p);
            if (TopAccent && Width > Radius * 2 + 8)
            {
                int ah = Math.Max(2, (int)Math.Round(2 * DshTheme.S));
                var r = new Rectangle(Radius, 0, Width - Radius * 2, ah);
                using (var b = new LinearGradientBrush(r, DshTheme.Blue, Color.FromArgb(0, DshTheme.Blue), 0f))
                    g.FillRectangle(b, r);
            }
        }
    }

    // ---------- 圆角按钮 (完全自绘: 主按钮品牌蓝渐变 / 次按钮半透明白, 无方形边框残留) ----------
    class ModernButton : Button
    {
        public int Radius;
        public bool Primary;      // true=品牌蓝渐变主按钮; false=暗色半透次按钮
        bool hover;
        bool pressed;

        public ModernButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            FlatStyle = FlatStyle.Flat;   // 必须 Flat: Standard 会走 comctl32 原生主题绘制, 产生父级内容错位叠印
            Cursor = Cursors.Hand;
            BackColor = Color.Transparent;
            ForeColor = Color.White;
            Radius = (int)Math.Round(10 * DshTheme.S);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (Width > 4 && Height > 4)
            {
                var oldRegion = Region;
                Region = new Region(Program.RoundRectPath(new Rectangle(0, 0, Width, Height), Radius));
                if (oldRegion != null) oldRegion.Dispose();
            }
        }

        // 主按钮悬停提亮: 由 FlatAppearance.MouseOverBackColor 处理, 此 OnPaint 仅为圆角

        // 先用"父背景切片"擦除客户区, 再绘制按钮 —— 不走框架透明合成, 杜绝文字残影
        // 注: 实测本配置下 Button 的 OnPaintBackground 不会被框架调用, 真正的擦除在 OnPaint 开头;
        // 此覆写保留作为兜底(若框架行为变化则双保险, 幂等无害)
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            Program.EraseWithParentBackground(this, e.Graphics);
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hover = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover = false; pressed = false; Invalidate(); }
        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); if (e.Button == MouseButtons.Left) { pressed = true; Invalidate(); } }
        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); pressed = false; Invalidate(); }
        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            // 关键擦除: 实测 Button 的 OnPaintBackground 在本配置下不会被调用(框架跳过背景绘制),
            // 因此擦除必须在 OnPaint 开头进行 —— 先用父背景切片覆盖客户区, 杜绝任何底层残影
            Program.EraseWithParentBackground(this, g);
            using (var path = Program.RoundRectPath(new Rectangle(0, 0, Width - 1, Height - 1), Radius))
            {
                if (Primary)
                {
                    // 垂直渐变: #4D6BFE → #3E5CEB, 悬停提亮至 #5D7CFF, 按下压暗至 #3550D4
                    Color top = pressed ? DshTheme.BlueDark : (hover ? DshTheme.BlueHover : DshTheme.Blue);
                    Color bottom = pressed ? DshTheme.BluePressed : (hover ? DshTheme.Blue : DshTheme.BlueDark);
                    if (!Enabled) { top = Color.FromArgb(102, top); bottom = Color.FromArgb(102, bottom); }
                    using (var br = new LinearGradientBrush(new Rectangle(0, 0, Width, Math.Max(1, Height)), top, bottom, 90f))
                        g.FillPath(br, path);
                    if (Enabled)
                    {
                        // 顶部内侧 1px 高光 (白 ~10%), 增加立体感
                        using (var pen = new Pen(Color.FromArgb(26, 255, 255, 255), 1f))
                            g.DrawLine(pen, Radius, 1, Math.Max(Radius, Width - Radius - 1), 1);
                    }
                }
                else
                {
                    // 暗色: 填充 白5% / 描边 白10%; 悬停 9%/16%; 按下 3%; 禁用统一降透明度
                    int fillA = pressed ? 8 : (hover ? 23 : 13);
                    int borderA = hover ? 41 : 26;
                    if (!Enabled) { fillA = 8; borderA = 15; }
                    using (var br = new SolidBrush(Color.FromArgb(fillA, 255, 255, 255)))
                        g.FillPath(br, path);
                    using (var pen = new Pen(Color.FromArgb(borderA, 255, 255, 255), 1f))
                        g.DrawPath(pen, path);
                }
                if (Focused && Enabled)
                {
                    // 键盘焦点: 白 25% 内侧细环 (无系统虚线框)
                    using (var p2 = Program.RoundRectPath(new Rectangle(2, 2, Math.Max(1, Width - 5), Math.Max(1, Height - 5)), Math.Max(2, Radius - 2)))
                    using (var pen = new Pen(Color.FromArgb(64, 255, 255, 255), 1f))
                        g.DrawPath(pen, p2);
                }
            }
            Color tc = Enabled ? ForeColor : Color.FromArgb(102, ForeColor);
            TextRenderer.DrawText(g, Text, Font, new Rectangle(0, 0, Width, Height), tc,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
        }
    }

    // ---------- 状态芯片 (圆角药丸标签) ----------
    class ChipLabel : Label
    {
        public ChipLabel()
        {
            AutoSize = true;
            BackColor = DshTheme.BgInput;
            ForeColor = DshTheme.TextMain;
            Font = new Font("Microsoft YaHei UI", 8.5f);
            Padding = new Padding((int)Math.Round(10 * DshTheme.S), (int)Math.Round(4 * DshTheme.S), (int)Math.Round(10 * DshTheme.S), (int)Math.Round(4 * DshTheme.S));
            Margin = new Padding(0, 0, (int)Math.Round(8 * DshTheme.S), (int)Math.Round(5 * DshTheme.S));
            TextAlign = ContentAlignment.MiddleCenter;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (Width > 8 && Height > 8)
            {
                var old = Region;
                Region = new Region(Program.RoundRectPath(new Rectangle(0, 0, Width, Height), Height / 2));
                if (old != null) old.Dispose();
            }
        }
    }

    // ---------- 左侧导航按钮 ----------
    class NavButton : Control
    {
        string text;
        int iconId;
        bool active;
        bool hover;

        public NavButton(string text, int iconId)
        {
            this.text = text;
            this.iconId = iconId;
            Height = (int)Math.Round(40 * DshTheme.S);
            Dock = DockStyle.Top;
            Cursor = Cursors.Hand;
            BackColor = DshTheme.BgDeep;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        public bool Active
        {
            get { return active; }
            set { active = value; Invalidate(); }
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hover = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover = false; Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            float s = DshTheme.S;
            int r = (int)Math.Round(10 * s);

            Color bg;
            if (active) bg = DshTheme.NavActive;
            else if (hover) bg = DshTheme.NavHover;
            else bg = Color.Transparent;

            int insetX = (int)Math.Round(3 * s);
            int insetY = (int)Math.Round(3 * s);
            if (bg != Color.Transparent)
            {
                using (var b = new SolidBrush(bg))
                using (var p = Program.RoundRectPath(new Rectangle(insetX, insetY, Width - insetX * 2, Height - insetY * 2), r))
                    g.FillPath(b, p);
            }

            if (active)
            {
                int bx = (int)Math.Round(9 * s);
                int by = (int)Math.Round(12 * s);
                using (var b = new SolidBrush(DshTheme.Blue))
                    g.FillRectangle(b, bx, by, Math.Max(2, (int)Math.Round(3 * s)), Height - 2 * by);
            }

            // 矢量图标
            int isz = (int)Math.Round(16 * s);
            Glyphs.Draw(g, iconId, new Rectangle((int)Math.Round(13 * s), (Height - isz) / 2, isz, isz),
                active ? Color.White : DshTheme.TextDim);

            var f = active ? DshFonts.NavActive : DshFonts.Nav;
            using (var b = new SolidBrush(active ? Color.White : DshTheme.TextDim))
            {
                var sz = g.MeasureString(text, f);
                g.DrawString(text, f, b, (int)Math.Round(38 * s), (Height - sz.Height) / 2f);
            }
        }
    }

    // ---------- 单行省略标签 (自绘, 强制单行, 超出显示省略号; 普通 Label 高度够两行时仍会换行) ----------
    class EllipsisLabel : Control
    {
        public ContentAlignment TextAlign = ContentAlignment.MiddleLeft;

        public EllipsisLabel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }

        // 先用"父背景切片"擦除, 再绘制文字 —— 不走框架透明合成, 杜绝文字残影
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            Program.EraseWithParentBackground(this, e.Graphics);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            var r = new Rectangle(Padding.Left, Padding.Top,
                Math.Max(0, Width - Padding.Left - Padding.Right),
                Math.Max(0, Height - Padding.Top - Padding.Bottom));
            var flags = TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix;
            if (TextAlign == ContentAlignment.MiddleCenter || TextAlign == ContentAlignment.TopCenter || TextAlign == ContentAlignment.BottomCenter)
                flags |= TextFormatFlags.HorizontalCenter;
            else if (TextAlign == ContentAlignment.MiddleRight || TextAlign == ContentAlignment.TopRight || TextAlign == ContentAlignment.BottomRight)
                flags |= TextFormatFlags.Right;
            else
                flags |= TextFormatFlags.Left;
            if (TextAlign == ContentAlignment.TopLeft || TextAlign == ContentAlignment.TopCenter || TextAlign == ContentAlignment.TopRight)
                flags |= TextFormatFlags.Top;
            else if (TextAlign == ContentAlignment.BottomLeft || TextAlign == ContentAlignment.BottomCenter || TextAlign == ContentAlignment.BottomRight)
                flags |= TextFormatFlags.Bottom;
            else
                flags |= TextFormatFlags.VerticalCenter;
            TextRenderer.DrawText(g, Text, Font, r, ForeColor, flags);
        }

        protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Invalidate(); }
        protected override void OnForeColorChanged(EventArgs e) { base.OnForeColorChanged(e); Invalidate(); }
        protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); Invalidate(); }
    }

    // ---------- 状态圆点 (实心点 + 柔和光环, 颜色取自 ForeColor) ----------
    class StatusDot : Control
    {
        public StatusDot()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }

        // 先用"父背景切片"擦除, 再绘制圆点 —— 不走框架透明合成, 杜绝残影
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            Program.EraseWithParentBackground(this, e.Graphics);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int w = Width - 1, h = Height - 1;
            using (var ring = new SolidBrush(Color.FromArgb(56, ForeColor)))
                g.FillEllipse(ring, 0, 0, w, h);
            int inset = Math.Max(2, (int)Math.Round(3 * DshTheme.S));
            using (var core = new SolidBrush(ForeColor))
                g.FillEllipse(core, inset, inset, w - inset * 2, h - inset * 2);
        }

        protected override void OnForeColorChanged(EventArgs e)
        {
            base.OnForeColorChanged(e);
            Invalidate();
        }
    }

    // ---------- 双缓冲不透明面板 (页面/页面宿主专用: 切页与缩放时整页干净重绘, 不留残影) ----------
    class BufPanel : Panel
    {
        public BufPanel()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }
    }

    // ---------- 纵向堆叠容器(自动滚动) ----------
    class StackPanel : Panel
    {
        int y;
        public int PadLeft = 4;
        public int Gap = 8;

        public StackPanel()
        {
            AutoScroll = true;
            DoubleBuffered = true;
            BackColor = Color.Transparent;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        public void BeginAdd() { y = 4; }

        public void Add(Control c, int height)
        {
            c.Top = y;
            c.Left = PadLeft;
            c.Height = height;
            c.Width = Math.Max(10, ClientSize.Width - PadLeft * 2);
            c.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
            Controls.Add(c);
            y += height + Gap;
            AutoScrollMinSize = new Size(0, y + 4);
        }

        public void ClearAll()
        {
            Controls.Clear();
            y = 4;
            AutoScrollMinSize = new Size(0, 4);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            foreach (Control c in Controls)
                c.Width = Math.Max(10, ClientSize.Width - PadLeft * 2);
        }
    }

    // ---------- 深色主题对话框 (替代系统浅色 MessageBox) ----------
    class DarkDialog : Form
    {
        float dlgS = DshTheme.S;

        int P(int v) { return (int)Math.Round(v * dlgS); }

        public DarkDialog(string message, string title, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            Text = title;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = DshTheme.BgCard;
            Font = DshFonts.Body;
            ShowInTaskbar = false;
            DoubleBuffered = true;
            KeyPreview = true;
            ClientSize = new Size(P(420), P(190));
            MaximumSize = ClientSize;
            MinimumSize = ClientSize;
            Region = new Region(Program.RoundRectPath(new Rectangle(0, 0, Width, Height), P(12)));

            // 顶部渐变条
            var top = new Panel { Dock = DockStyle.Top, Height = P(3), BackColor = DshTheme.Blue };
            Controls.Add(top);

            Color iconColor;
            string iconGlyph = "●";
            if (icon == MessageBoxIcon.Error) iconColor = DshTheme.Error;
            else if (icon == MessageBoxIcon.Warning) iconColor = DshTheme.Warn;
            else if (icon == MessageBoxIcon.Question) iconColor = DshTheme.Info;
            else iconColor = DshTheme.Success;

            var dot = new Label { Text = iconGlyph, AutoSize = true, Left = P(20), Top = P(16), ForeColor = iconColor, BackColor = Color.Transparent, Font = DshFonts.DotBig };
            var titleLbl = new Label { Text = title, AutoSize = true, Left = P(44), Top = P(14), ForeColor = Color.White, BackColor = Color.Transparent, Font = DshFonts.HeroButton };
            var msg = new Label
            {
                Text = message,
                AutoSize = false,
                Location = new Point(P(20), P(48)),
                Size = new Size(P(380), P(78)),
                ForeColor = DshTheme.TextDim,
                BackColor = Color.Transparent,
                Font = DshFonts.Body
            };

            var btnRow = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = P(52),
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                BackColor = Color.Transparent,
                Padding = new Padding(0, P(8), P(16), 0)
            };

            var cancel = MakeBtn(Lang.T("取消"), DialogResult.Cancel, false);
            var no = MakeBtn(Lang.T("否"), DialogResult.No, false);
            var yes = MakeBtn(Lang.T("是"), DialogResult.Yes, buttons == MessageBoxButtons.YesNo || buttons == MessageBoxButtons.YesNoCancel);
            var ok = MakeBtn(Lang.T("确定"), DialogResult.OK, true);

            if (buttons == MessageBoxButtons.YesNo) { btnRow.Controls.Add(no); btnRow.Controls.Add(yes); }
            else if (buttons == MessageBoxButtons.YesNoCancel) { btnRow.Controls.Add(cancel); btnRow.Controls.Add(no); btnRow.Controls.Add(yes); }
            else { btnRow.Controls.Add(ok); }

            Controls.Add(dot);
            Controls.Add(titleLbl);
            Controls.Add(msg);
            Controls.Add(btnRow);

            CancelButton = (buttons == MessageBoxButtons.YesNoCancel) ? cancel : null;
            AcceptButton = (buttons == MessageBoxButtons.YesNo || buttons == MessageBoxButtons.YesNoCancel) ? yes : ok;

            KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); }
            };
        }

        ModernButton MakeBtn(string text, DialogResult result, bool primary)
        {
            var b = new ModernButton
            {
                Text = text,
                Width = P(88),
                Height = P(34),
                Margin = new Padding(P(6), 0, P(6), 0),
                Primary = primary,
                ForeColor = primary ? Color.White : DshTheme.TextMain,
                Font = DshFonts.Body,
                DialogResult = result
            };
            b.Click += delegate { DialogResult = result; Close(); };
            return b;
        }

        public static DialogResult Show(IWin32Window owner, string message, string title, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            using (var d = new DarkDialog(message, title, buttons, icon))
            {
                if (owner == null) d.StartPosition = FormStartPosition.CenterScreen;
                return d.ShowDialog(owner);
            }
        }

        // 带输入框的深色对话框
        public static string Input(IWin32Window owner, string title, string prompt, string def)
        {
            using (var d = new DarkInputDialog(title, prompt, def))
            {
                if (owner == null) d.StartPosition = FormStartPosition.CenterScreen;
                return d.ShowDialog(owner) == DialogResult.OK ? d.Value : null;
            }
        }
    }

    // ---------- 深色输入对话框 ----------
    class DarkInputDialog : Form
    {
        float dlgS = DshTheme.S;
        TextBox box;

        int P(int v) { return (int)Math.Round(v * dlgS); }

        public string Value
        {
            get { return box == null ? "" : box.Text.Trim(); }
        }

        public DarkInputDialog(string title, string prompt, string def)
        {
            Text = title;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = DshTheme.BgCard;
            Font = DshFonts.Body;
            ShowInTaskbar = false;
            DoubleBuffered = true;
            KeyPreview = true;
            ClientSize = new Size(P(480), P(200));
            MaximumSize = ClientSize;
            MinimumSize = ClientSize;
            Region = new Region(Program.RoundRectPath(new Rectangle(0, 0, Width, Height), P(12)));

            var top = new Panel { Dock = DockStyle.Top, Height = P(3), BackColor = DshTheme.Blue };
            Controls.Add(top);

            var titleLbl = new Label { Text = title, AutoSize = true, Left = P(20), Top = P(14), ForeColor = Color.White, BackColor = Color.Transparent, Font = DshFonts.CardTitle };
            var promptLbl = new Label
            {
                Text = prompt,
                AutoSize = false,
                Location = new Point(P(20), P(44)),
                Size = new Size(P(440), P(46)),
                ForeColor = DshTheme.TextDim,
                BackColor = Color.Transparent,
                Font = DshFonts.Body
            };
            box = new TextBox
            {
                Location = new Point(P(20), P(96)),
                Width = P(440),
                BackColor = DshTheme.BgInput,
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Text = def,
                Font = DshFonts.MonoSmall
            };

            var ok = new ModernButton
            {
                Text = Lang.T("确定"),
                Width = P(88),
                Height = P(34),
                Left = P(280),
                Top = P(150),
                Primary = true,
                ForeColor = Color.White,
                Font = DshFonts.Body
            };
            var cancel = new ModernButton
            {
                Text = Lang.T("取消"),
                Width = P(88),
                Height = P(34),
                Left = P(376),
                Top = P(150),
                Primary = false,
                ForeColor = DshTheme.TextMain,
                Font = DshFonts.Body
            };
            ok.Click += delegate { DialogResult = DialogResult.OK; Close(); };
            cancel.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };

            Controls.Add(titleLbl);
            Controls.Add(promptLbl);
            Controls.Add(box);
            Controls.Add(ok);
            Controls.Add(cancel);

            AcceptButton = ok;
            CancelButton = cancel;
            KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); }
            };
            Shown += delegate { box.Focus(); box.SelectAll(); };
        }
    }

    // ---------- 主窗口 ----------
    class LauncherForm : Form
    {
        const string LauncherVersion = "1.5.0";

        LauncherConfig cfg;
        Process serverProc;
        bool starting;
        bool stopping;
        bool installing;
        bool updating;
        bool quitting;
        float S = 1f;                                 // 系统缩放系数(自动获取)
        EnvInfo currentEnv = new EnvInfo();
        bool envDetected;                         // 首次环境检测完成前为 false → 概览显示中性"检测中…"占位
        UpdateInfo currentUpdate = new UpdateInfo();

        // 外壳
        Panel sidebar;
        NavButton[] navs;
        Panel[] pages;
        DshProgressBar progress;
        Label sbDot, sbText, sbRight;
        Panel titleBar;
        Label titleTxt;
        bool dragging;
        Point dragStart;

        // 概览页
        RoundPanel heroCard;
        StatusDot ovDot;
        EllipsisLabel ovStatusTitle, ovStatusSub;
        FlowLayoutPanel ovChips;
        ModernButton ovPrimary, ovSecondary, ovTertiary;
        TextBox ovLog;

        // 环境页
        StackPanel envHost;
        ModernButton envRedetect, envInstall;

        // 插件页
        StackPanel pluginList;
        ModernButton pluginRefresh, pluginUpdateAll, pluginRepair, pluginInstall, pluginMaintain;
        Label pluginSummary;

        // 更新页
        Label upCurrent, upLatest;
        EllipsisLabel upNote;
        ModernButton upCheck, upApply;
        TextBox upPluginDetail;
        Label upLauncherLatest, upLauncherNote;
        ModernButton upLauncherCheck, upLauncherGo;
        Dictionary<string, string> pluginLocalHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string> pluginRemoteHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // 日志页
        ComboBox logKind;
        TextBox logText;
        ModernButton logRefresh, logClear, logOpen;
        CheckBox logAuto;

        // 设置页
        TextBox setCmd, setPort, setHome, setPlugins, setLog, setNpm, setLupUrl, setProxy;
        ComboBox setLang;
        ModernButton setDetect, setSave, setOpenCfg;

        // 商城窗口
        StoreForm storeWin;

        // 托盘与定时器
        NotifyIcon tray;
        Timer trayWatch;
        Timer logTimer;
        Timer updateTimer;
        bool checkingAuto;

        Bitmap logo;
        Bitmap whaleWhite;
        Bitmap whaleBlue;
        bool logoOk;

        public LauncherForm()
        {
            cfg = LauncherConfig.Load();
            try { Environment.SetEnvironmentVariable("DSH_HOME", cfg.DshHome); } catch { }
            Lang.Set(cfg.Language);   // 界面语言(zh/en), 所有文案经 Lang.T() 输出

            // ---- 响应式: 运行时自动读取系统 DPI 得到缩放系数, 全部尺寸乘以 S ----
            try
            {
                using (var g = CreateGraphics())
                {
                    S = g.DpiX / 96f;
                    if (S < 0.8f || S > 4f) S = 1f;
                }
            }
            catch { S = 1f; }
            DshTheme.S = S;

            // 初始窗口尺寸按屏幕工作区自动计算: 常规 800x600, 小屏自动收缩(不低于 800x560)
            int wantW = 920, wantH = 640;
            try
            {
                Rectangle wa = Screen.PrimaryScreen.WorkingArea;   // 物理像素
                int availW = (int)(wa.Width / S) - 24;
                int availH = (int)(wa.Height / S) - 24;
                wantW = Math.Min(wantW, Math.Max(800, availW));
                wantH = Math.Min(wantH, Math.Max(560, availH));
            }
            catch { }

            Text = "DeepSeek Harness 启动器";
            StartPosition = FormStartPosition.Manual;
            FormBorderStyle = FormBorderStyle.None;
            ClientSize = new Size(Px(wantW), Px(wantH));
            // 按屏幕工作区手动居中(物理像素), 规避高 DPI 下 CenterScreen 偏移问题
            // (FormBorderStyle 必须先设为 None, 保证 Width/Height == ClientSize, 居中才精确)
            try
            {
                Rectangle wa = Screen.PrimaryScreen.WorkingArea;
                Location = new Point(wa.Left + (wa.Width - Width) / 2, wa.Top + (wa.Height - Height) / 2);
            }
            catch { }
            MinimumSize = new Size(Px(800), Px(560));
            BackColor = DshTheme.Bg;
            Font = DshFonts.Body;
            DoubleBuffered = true;

            // 显式绑定窗口图标: 保证任务栏/Alt-Tab 显示 DeepSeek 鲸鱼图标
            try { Icon = new Icon(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "deepseek.ico")); }
            catch { }

            whaleWhite = Program.LoadEmbeddedPng("DeepSeekHarness.whale-white.png");
            whaleBlue = Program.LoadEmbeddedPng("DeepSeekHarness.whale-blue.png");
            try
            {
                var asm = Assembly.GetExecutingAssembly();
                using (var s = asm.GetManifestResourceStream("DeepSeekHarness.logo.png"))
                    if (s != null) { logo = new Bitmap(s); logoOk = true; }
            }
            catch { logoOk = false; }

            // 注意: WinForms 按 Controls 倒序停靠, Fill 面板必须最先加入集合,
            // 这样它才会最后停靠并获得"剩余空间", 而不是被侧栏/标题栏/状态栏盖住
            BuildPages();     // content (Dock=Fill) 最先加入
            BuildSidebar();   // 其次 sidebar (Dock=Left)
            BuildStatusBar(); // 再次状态栏 (Dock=Bottom)
            BuildTitleBar();  // 最后标题栏 (Dock=Top)
            BuildTray();

            logTimer = new Timer { Interval = 1500 };
            logTimer.Tick += delegate { RefreshLogViews(); };
            logTimer.Start();

            trayWatch = new Timer { Interval = 800 };
            trayWatch.Tick += delegate { CheckReopenFlag(); };
            trayWatch.Start();

            // 版本信息每 3 小时主动后台获取并刷新显示, 无需用户手动点击
            updateTimer = new Timer { Interval = 3 * 60 * 60 * 1000 };
            updateTimer.Tick += delegate { AutoCheckNow(); };
            updateTimer.Start();

            SwitchPage(0);
            Load += delegate { BeginInvoke((MethodInvoker)delegate { RunStartupFlow(); }); };
        }

        int Px(int v) { return (int)Math.Round(v * S); }

        // 统一启用/禁用: 禁用态的降透明度绘制由 ModernButton.OnPaint 负责
        void StyleEnabled(ModernButton b, bool enabled, bool primary)
        {
            b.Enabled = enabled;
            b.Primary = primary;
        }

        // ============ 标题栏 (36px, 底部 1px 分隔线, 可拖拽) ============
        void BuildTitleBar()
        {
            titleBar = new Panel { Dock = DockStyle.Top, Height = Px(36), BackColor = DshTheme.BgDeep, Cursor = Cursors.SizeAll };
            titleBar.Paint += delegate(object s, PaintEventArgs e)
            {
                using (var pen = new Pen(DshTheme.BorderSoft, 1f))
                    e.Graphics.DrawLine(pen, 0, titleBar.Height - 1, titleBar.Width, titleBar.Height - 1);
            };

            // 小号蓝色鲸鱼字形 (透明底无卡片; 加载失败退化为品牌蓝圆点)
            var titleLogo = new Panel { Width = Px(18), Height = Px(18), Left = Px(12), Top = Px(9), BackColor = Color.Transparent };
            titleLogo.Paint += delegate(object s, PaintEventArgs e)
            {
                var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
                if (whaleBlue != null) Program.DrawWhaleFit(g, whaleBlue, new Rectangle(0, 0, titleLogo.Width, titleLogo.Height));
                else using (var b = new SolidBrush(DshTheme.Blue)) g.FillEllipse(b, 0, 0, titleLogo.Width - 1, titleLogo.Height - 1);
            };

            titleTxt = new Label
            {
                Text = "DeepSeek Harness 启动器",
                AutoSize = true,
                Left = Px(38),
                Top = Px(10),
                ForeColor = DshTheme.TextDim,
                BackColor = Color.Transparent,
                Font = DshFonts.TitleBar
            };

            var minBtn = MakeTitleBtn("─");
            var maxBtn = MakeTitleBtn("▢");
            var closeBtn = MakeTitleBtn("✕");
            minBtn.Dock = DockStyle.Right;
            maxBtn.Dock = DockStyle.Right;
            closeBtn.Dock = DockStyle.Right;
            minBtn.Width = Px(46); maxBtn.Width = Px(46); closeBtn.Width = Px(46);
            minBtn.MouseEnter += delegate { minBtn.BackColor = DshTheme.BtnHover; minBtn.ForeColor = Color.White; };
            minBtn.MouseLeave += delegate { minBtn.BackColor = Color.Transparent; minBtn.ForeColor = DshTheme.TextDim; };
            maxBtn.MouseEnter += delegate { maxBtn.BackColor = DshTheme.BtnHover; maxBtn.ForeColor = Color.White; };
            maxBtn.MouseLeave += delegate { maxBtn.BackColor = Color.Transparent; maxBtn.ForeColor = DshTheme.TextDim; };
            closeBtn.MouseEnter += delegate { closeBtn.BackColor = Color.FromArgb(255, 210, 70, 70); closeBtn.ForeColor = Color.White; };
            closeBtn.MouseLeave += delegate { closeBtn.BackColor = Color.Transparent; closeBtn.ForeColor = DshTheme.TextDim; };
            minBtn.Click += delegate { WindowState = FormWindowState.Minimized; };
            maxBtn.Click += delegate { ToggleMaximize(); };
            closeBtn.Click += delegate { HideToTray(); };
            Resize += delegate { maxBtn.Text = (WindowState == FormWindowState.Maximized) ? "❐" : "▢"; };

            titleBar.Controls.Add(titleTxt);
            titleBar.Controls.Add(titleLogo);
            titleBar.Controls.Add(minBtn);
            titleBar.Controls.Add(maxBtn);
            titleBar.Controls.Add(closeBtn);
            titleTxt.BringToFront();
            titleLogo.BringToFront();

            titleBar.MouseDown += delegate(object s, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left) { dragging = true; dragStart = new Point(e.X, e.Y); }
            };
            titleBar.MouseMove += delegate(object s, MouseEventArgs e)
            {
                if (dragging) Location = new Point(Location.X + e.X - dragStart.X, Location.Y + e.Y - dragStart.Y);
            };
            titleBar.MouseUp += delegate { dragging = false; };
            titleBar.DoubleClick += delegate { ToggleMaximize(); };

            Controls.Add(titleBar);
        }

        void ToggleMaximize()
        {
            WindowState = (WindowState == FormWindowState.Maximized) ? FormWindowState.Normal : FormWindowState.Maximized;
        }

        // 无边框窗口: 边缘拖拽缩放 (WM_NCHITTEST)
        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg == 0x84 && WindowState != FormWindowState.Maximized)
            {
                int x = unchecked((short)((long)m.LParam & 0xFFFF));
                int y = unchecked((short)(((long)m.LParam >> 16) & 0xFFFF));
                Point pt = PointToClient(new Point(x, y));
                int b = Px(6);
                bool top = pt.Y <= b, left = pt.X <= b, right = pt.X >= ClientSize.Width - b, bottom = pt.Y >= ClientSize.Height - b;
                if (top && left) m.Result = (IntPtr)13;
                else if (top && right) m.Result = (IntPtr)14;
                else if (bottom && left) m.Result = (IntPtr)16;
                else if (bottom && right) m.Result = (IntPtr)17;
                else if (top) m.Result = (IntPtr)12;
                else if (left) m.Result = (IntPtr)10;
                else if (right) m.Result = (IntPtr)11;
                else if (bottom) m.Result = (IntPtr)15;
            }
        }

        Label MakeTitleBtn(string glyph)
        {
            return new Label
            {
                Text = glyph,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = DshTheme.TextDim,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand,
                Font = DshFonts.Glyph
            };
        }

        // ============ 侧边栏 (200px, 品牌块 + 导航 + 版本脚注) ============
        void BuildSidebar()
        {
            sidebar = new Panel
            {
                Dock = DockStyle.Left,
                Width = Px(200),
                BackColor = DshTheme.BgDeep,
                Padding = new Padding(Px(12), Px(16), Px(12), Px(12))
            };
            sidebar.Paint += delegate(object s, PaintEventArgs e)
            {
                using (var pen = new Pen(DshTheme.BorderSoft, 1f))
                    e.Graphics.DrawLine(pen, sidebar.Width - 1, 0, sidebar.Width - 1, sidebar.Height);
            };

            var brandRow = new Panel { Dock = DockStyle.Top, Height = Px(64), BackColor = Color.Transparent };
            var logoCard = new Panel { Size = new Size(Px(36), Px(36)), Location = new Point(Px(4), Px(14)), BackColor = Color.Transparent };
            logoCard.Paint += delegate(object s, PaintEventArgs e)
            {
                var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
                int sz = logoCard.Width;
                // 品牌蓝对角渐变圆角卡 + 白色鲸鱼剪影 (同色系图标化, 无白底方块)
                using (var p = Program.RoundRectPath(new Rectangle(0, 0, sz - 1, sz - 1), (int)Math.Round(10 * S)))
                {
                    using (var b = new LinearGradientBrush(new Rectangle(0, 0, sz, sz), DshTheme.Blue, DshTheme.BlueDark, 45f))
                        g.FillPath(b, p);
                    using (var pen = new Pen(Color.FromArgb(28, 255, 255, 255), 1f))
                        g.DrawPath(pen, p);
                }
                if (whaleWhite != null) Program.DrawWhaleFit(g, whaleWhite, new Rectangle(Px(5), Px(5), sz - Px(10), sz - Px(10)));
                else if (logoOk && logo != null) g.DrawImage(logo, Px(4), Px(4), sz - Px(8), sz - Px(8));
            };
            var brandTitle = new Label
            {
                Text = "DeepSeek Harness",
                AutoSize = true,
                Location = new Point(Px(48), Px(15)),
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Font = DshFonts.BodyBold
            };
            var brandSub = new Label
            {
                Text = Lang.T("DSH 启动器"),
                AutoSize = true,
                Location = new Point(Px(49), Px(35)),
                ForeColor = DshTheme.TextDim,
                BackColor = Color.Transparent,
                Font = DshFonts.Caption
            };
            brandRow.Controls.Add(logoCard);
            brandRow.Controls.Add(brandTitle);
            brandRow.Controls.Add(brandSub);

            var navHost = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Padding = new Padding(0, Px(4), 0, 0) };
            string[] navNames = { Lang.T("概览"), Lang.T("环境"), Lang.T("插件"), Lang.T("更新"), Lang.T("日志"), Lang.T("设置") };
            int[] navIcons = { 0, 1, 2, 3, 4, 5 };
            navs = new NavButton[navNames.Length];
            for (int i = navNames.Length - 1; i >= 0; i--)
            {
                int idx = i;
                var nb = new NavButton(navNames[i], navIcons[i]);
                nb.Click += delegate { SwitchPage(idx); };
                navs[i] = nb;
                navHost.Controls.Add(nb);
            }

            var foot = new Panel { Dock = DockStyle.Bottom, Height = Px(24), BackColor = Color.Transparent };
            var verLbl = new Label
            {
                Text = "v" + LauncherVersion + " · by loudMore",
                AutoSize = true,
                Location = new Point(Px(4), Px(5)),
                ForeColor = DshTheme.TextFaint,
                BackColor = Color.Transparent,
                Font = DshFonts.Footer
            };
            foot.Controls.Add(verLbl);

            sidebar.Controls.Add(navHost);
            sidebar.Controls.Add(foot);
            sidebar.Controls.Add(brandRow);
            Controls.Add(sidebar);
        }

        // ============ 页面容器 ============
        Panel pageHost;

        void BuildPages()
        {
            var content = new BufPanel { Dock = DockStyle.Fill, BackColor = DshTheme.Bg };

            progress = new DshProgressBar { Dock = DockStyle.Top, Visible = false };
            progress.Height = Px(4);

            pageHost = new BufPanel { Dock = DockStyle.Fill, BackColor = DshTheme.Bg };

            pages = new Panel[6];
            pages[0] = BuildOverviewPage();
            pages[1] = BuildEnvironmentPage();
            pages[2] = BuildPluginsPage();
            pages[3] = BuildUpdatePage();
            pages[4] = BuildLogsPage();
            pages[5] = BuildSettingsPage();
            foreach (var p in pages) { p.Dock = DockStyle.Fill; p.Visible = false; pageHost.Controls.Add(p); }

            content.Controls.Add(pageHost);
            content.Controls.Add(progress);
            Controls.Add(content);
        }

        // ---------- 通用构建辅助 ----------
        // 页面纵向节奏: 页边距 28/20/28/20, 卡片间距统一 12 (均为逻辑像素, 乘 S)
        TableLayoutPanel MakeVStack()
        {
            var t = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(Px(28), Px(20), Px(28), Px(20)),
                ColumnCount = 1,
                BackColor = DshTheme.Bg
            };
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            return t;
        }

        // Absolute 行的 size 语义 = 控件本身高度; 助手自动在行高里追加 12px 间隙,
        // 彻底避免旧版 margin 吃掉控件高度导致按钮/标题被裁切的问题
        void AddRow(TableLayoutPanel t, Control c, SizeType type, float size)
        {
            int row = t.RowCount;
            t.RowCount = row + 1;
            int gap = type == SizeType.Absolute ? Px(12) : 0;
            t.RowStyles.Add(new RowStyle(type, size + gap));
            c.Dock = DockStyle.Fill;
            c.Margin = new Padding(0, 0, 0, gap);
            t.Controls.Add(c, 0, row);
        }

        // 统一页面大标题 (每个页面都有, 13.5pt Bold)
        Label MakeSection(string text)
        {
            return new Label
            {
                Text = text,
                Dock = DockStyle.Fill,
                ForeColor = DshTheme.TextMain,
                BackColor = Color.Transparent,
                Font = DshFonts.PageTitle,
                TextAlign = ContentAlignment.MiddleLeft
            };
        }

        ModernButton MakeBtn(string text, int width, int height, bool primary)
        {
            var b = new ModernButton
            {
                Text = text,
                Width = width,
                Height = height,
                Primary = primary,
                ForeColor = primary ? Color.White : DshTheme.TextMain,
                Font = DshFonts.Body
            };
            return b;
        }

        ModernButton MakeDangerBtn(string text, int width, int height)
        {
            // 危险操作同样保持暗色次按钮风格: 一个界面只有一种主色, 不引入红色面
            return MakeBtn(text, width, height, false);
        }

        Label MakeDot(Color color, int size)
        {
            var d = new Label
            {
                Text = "●",
                AutoSize = true,
                ForeColor = color,
                BackColor = Color.Transparent,
                Font = size > 12 ? DshFonts.DotBig : DshFonts.Dot,
                TextAlign = ContentAlignment.MiddleLeft
            };
            return d;
        }

        TextBox MakeMonoBox()
        {
            return new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                BackColor = DshTheme.BgInput,
                ForeColor = DshTheme.TextDim,
                Font = DshFonts.Mono,
                ScrollBars = ScrollBars.None,
                WordWrap = false
            };
        }

        // 行悬停: 行本体与其子控件任一处悬停都高亮整行
        void WireHover(Control row, Color normal, Color hover)
        {
            EventHandler enter = delegate { row.BackColor = hover; };
            EventHandler leave = delegate { row.BackColor = normal; };
            row.MouseEnter += enter;
            row.MouseLeave += leave;
            foreach (Control c in row.Controls)
            {
                c.MouseEnter += enter;
                c.MouseLeave += leave;
            }
        }

        // ============ 概览页 ============
        Panel BuildOverviewPage()
        {
            var page = new BufPanel { BackColor = DshTheme.Bg };
            var stack = MakeVStack();
            AddRow(stack, MakeSection("概览"), SizeType.Absolute, Px(32));

            // 状态主卡: 顶部品牌蓝修饰线 + 左文右钮的嵌套布局 (无绝对定位重叠)
            heroCard = new RoundPanel
            {
                GradTop = Color.FromArgb(255, 34, 44, 92),
                GradBottom = Color.FromArgb(255, 18, 23, 42),
                TopAccent = true,
                Padding = new Padding(Px(22), Px(18), Px(22), Px(18))
            };

            var heroTlp = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = Color.Transparent, Margin = Padding.Empty };
            heroTlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            heroTlp.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            heroTlp.RowStyles.Add(new RowStyle(SizeType.Absolute, Px(44)));

            var textTlp = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = Color.Transparent, Margin = new Padding(0, 0, 0, Px(6)) };
            textTlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            textTlp.RowStyles.Add(new RowStyle(SizeType.Absolute, Px(30)));
            textTlp.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var titleRow = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Margin = Padding.Empty };
            ovDot = new StatusDot { Size = new Size(Px(16), Px(16)), Location = new Point(Px(1), Px(7)), ForeColor = DshTheme.TextDim };
            ovStatusTitle = new EllipsisLabel
            {
                Dock = DockStyle.Fill,
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Font = DshFonts.HeroTitle,
                Text = "正在初始化…",
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(Px(24), 0, 0, 0)
            };
            titleRow.Controls.Add(ovStatusTitle);
            titleRow.Controls.Add(ovDot);
            ovDot.BringToFront();

            ovStatusSub = new EllipsisLabel
            {
                Dock = DockStyle.Fill,
                ForeColor = DshTheme.TextDim,
                BackColor = Color.Transparent,
                Font = DshFonts.Body,
                Text = "",
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(Px(24), 0, 0, 0),
                Margin = Padding.Empty
            };
            textTlp.Controls.Add(titleRow, 0, 0);
            textTlp.Controls.Add(ovStatusSub, 0, 1);

            ovPrimary = MakeBtn("▶ " + Lang.T("一键启动"), Px(168), Px(44), true);
            ovPrimary.Font = DshFonts.HeroButton;
            ovPrimary.Margin = new Padding(Px(10), 0, 0, 0);
            ovSecondary = MakeBtn("↻ 检查更新", Px(110), Px(40), false);
            ovSecondary.Margin = new Padding(Px(10), Px(2), 0, Px(2));
            ovTertiary = MakeBtn("↻ 重启服务", Px(96), Px(40), false);
            ovTertiary.Margin = new Padding(Px(10), Px(2), 0, Px(2));

            ovPrimary.Click += delegate { PrimaryAction(); };
            ovSecondary.Click += delegate { SecondaryAction(); };
            ovTertiary.Click += delegate { RestartService(); };

            // 右对齐按钮流 (RTL: 先加入者居右, 视觉顺序与旧版一致: 主→次→三)
            var btnFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                BackColor = Color.Transparent,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            btnFlow.Controls.Add(ovTertiary);
            btnFlow.Controls.Add(ovSecondary);
            btnFlow.Controls.Add(ovPrimary);

            heroTlp.Controls.Add(textTlp, 0, 0);
            heroTlp.Controls.Add(btnFlow, 0, 1);
            heroCard.Controls.Add(heroTlp);

            // 状态信息条 (彩色状态芯片, 自动换行)
            var strip = new RoundPanel();
            ovChips = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                BackColor = Color.Transparent,
                Padding = new Padding(Px(12), Px(8), Px(12), Px(2))
            };
            strip.Controls.Add(ovChips);

            // 最近日志卡 (标题行: 标题靠左, 提示靠右, Dock 布局无魔法坐标)
            var log = new RoundPanel { Padding = new Padding(Px(14), Px(6), Px(14), Px(14)) };
            var head = new Panel { Dock = DockStyle.Top, Height = Px(34), BackColor = Color.Transparent };
            var logTitle = new Label { Text = "最近日志 (launcher.log)", AutoSize = true, Dock = DockStyle.Left, ForeColor = DshTheme.TextMain, BackColor = Color.Transparent, Font = DshFonts.CardTitle, TextAlign = ContentAlignment.MiddleLeft };
            var logHint = new Label { Text = "滚轮滚动 · 完整日志在「日志」页", AutoSize = true, Dock = DockStyle.Right, ForeColor = DshTheme.TextFaint, BackColor = Color.Transparent, Font = DshFonts.Caption, TextAlign = ContentAlignment.MiddleRight };
            head.Controls.Add(logTitle);
            head.Controls.Add(logHint);
            ovLog = MakeMonoBox();
            ovLog.Dock = DockStyle.Fill;
            log.Controls.Add(ovLog);
            log.Controls.Add(head);

            AddRow(stack, heroCard, SizeType.Absolute, Px(148));
            AddRow(stack, strip, SizeType.Absolute, Px(64));
            AddRow(stack, log, SizeType.Percent, 100);
            page.Controls.Add(stack);
            return page;
        }

        // ============ 环境页 ============
        Panel BuildEnvironmentPage()
        {
            var page = new BufPanel { BackColor = DshTheme.Bg };
            var stack = MakeVStack();
            AddRow(stack, MakeSection(Lang.T("运行环境")), SizeType.Absolute, Px(32));

            var card = new RoundPanel { Padding = new Padding(Px(14), Px(12), Px(14), Px(12)) };
            envHost = new StackPanel { Dock = DockStyle.Fill };
            envHost.Gap = Px(6);
            envHost.PadLeft = Px(4);
            card.Controls.Add(envHost);

            var actionRow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Color.Transparent,
                Padding = new Padding(0, Px(4), 0, 0)
            };
            envRedetect = MakeBtn("重新检测", Px(96), Px(36), false); envRedetect.Margin = new Padding(0, 0, Px(10), 0); envRedetect.Click += delegate { RedetectNow(); };
            envInstall = MakeBtn("↓ " + Lang.T("一键安装 / 修复环境"), Px(188), Px(36), true); envInstall.Margin = Padding.Empty; envInstall.Click += delegate { InstallDshNow(); };
            actionRow.Controls.AddRange(new Control[] { envRedetect, envInstall });

            AddRow(stack, card, SizeType.Percent, 100);
            AddRow(stack, actionRow, SizeType.Absolute, Px(44));
            page.Controls.Add(stack);
            return page;
        }

        // ============ 插件页 ============
        Panel BuildPluginsPage()
        {
            var page = new BufPanel { BackColor = DshTheme.Bg };
            var stack = MakeVStack();
            AddRow(stack, MakeSection(Lang.T("插件管理")), SizeType.Absolute, Px(32));

            var toolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                WrapContents = false,
                BackColor = Color.Transparent,
                Padding = new Padding(0, Px(4), 0, 0)
            };
            // 工具栏按钮统一宽度、统一暗色风格 (傻瓜式: 一眼整齐, 不分主次打扰)
            pluginRefresh = MakeBtn(Lang.T("刷新列表"), Px(108), Px(36), false); pluginRefresh.Margin = new Padding(0, 0, Px(8), 0); pluginRefresh.Click += delegate { RenderPlugins(); };
            pluginInstall = MakeBtn(Lang.T("安装插件"), Px(108), Px(36), false); pluginInstall.Margin = new Padding(0, 0, Px(8), 0); pluginInstall.Click += delegate { InstallPlugin(); };
            var pluginStoreBtn = MakeBtn("🛍 " + Lang.T("插件商城"), Px(108), Px(36), false); pluginStoreBtn.Margin = new Padding(0, 0, Px(8), 0); pluginStoreBtn.Click += delegate { OpenStoreWindow(); };
            pluginUpdateAll = MakeBtn("↻ " + Lang.T("全部更新"), Px(108), Px(36), false); pluginUpdateAll.Margin = new Padding(0, 0, Px(8), 0); pluginUpdateAll.Click += delegate { UpdateAllPlugins(); };
            pluginRepair = MakeBtn(Lang.T("修复依赖"), Px(108), Px(36), false); pluginRepair.Margin = new Padding(0, 0, Px(8), 0); pluginRepair.Click += delegate { RepairPlugins(); };
            pluginMaintain = MakeBtn(Lang.T("一键维护"), Px(108), Px(36), false); pluginMaintain.Margin = Padding.Empty; pluginMaintain.Click += delegate { MaintainPlugins(); };
            toolbar.Controls.AddRange(new Control[] { pluginRefresh, pluginInstall, pluginStoreBtn, pluginUpdateAll, pluginRepair, pluginMaintain });

            // ---- 已安装插件管理 ----
            var card = new RoundPanel { Padding = new Padding(Px(10), Px(6), Px(10), Px(10)) };
            var head = new Panel { Dock = DockStyle.Top, Height = Px(30), BackColor = Color.Transparent };
            var headTitle = new Label { Text = Lang.T("已安装插件"), AutoSize = true, Dock = DockStyle.Left, ForeColor = DshTheme.TextMain, BackColor = Color.Transparent, Font = DshFonts.CardTitle, TextAlign = ContentAlignment.MiddleLeft };
            pluginSummary = new Label { Text = "", AutoSize = true, Dock = DockStyle.Right, ForeColor = DshTheme.TextFaint, BackColor = Color.Transparent, Font = DshFonts.Small, TextAlign = ContentAlignment.MiddleRight };
            head.Controls.Add(headTitle);
            head.Controls.Add(pluginSummary);
            pluginList = new StackPanel { Dock = DockStyle.Fill };
            pluginList.Gap = Px(8);
            pluginList.PadLeft = Px(4);
            card.Controls.Add(pluginList);
            card.Controls.Add(head);

            AddRow(stack, toolbar, SizeType.Absolute, Px(44));
            AddRow(stack, card, SizeType.Percent, 100);
            page.Controls.Add(stack);
            return page;
        }

        void OpenStoreWindow()
        {
            if (storeWin == null || storeWin.IsDisposed)
            {
                storeWin = new StoreForm(this);
                storeWin.Show(this);
            }
            else
            {
                storeWin.Show();
                storeWin.Activate();
            }
        }

        // ============ 更新页 ============
        Panel BuildUpdatePage()
        {
            var page = new BufPanel { BackColor = DshTheme.Bg };
            var stack = MakeVStack();
            AddRow(stack, MakeSection(Lang.T("更新与升级")), SizeType.Absolute, Px(32));

            // 启动器自更新卡
            var lch = new RoundPanel { Padding = new Padding(Px(18), Px(10), Px(18), Px(10)) };
            var lHead = new Panel { Dock = DockStyle.Top, Height = Px(34), BackColor = Color.Transparent };
            var lTitle = new Label { Text = Lang.T("启动器") + "  v" + LauncherVersion, AutoSize = true, Dock = DockStyle.Left, ForeColor = DshTheme.TextMain, BackColor = Color.Transparent, Font = DshFonts.CardTitle, TextAlign = ContentAlignment.MiddleLeft };
            upLauncherCheck = MakeBtn("↻ " + Lang.T("检查更新"), Px(100), Px(34), false);
            upLauncherCheck.Margin = new Padding(0, 0, Px(8), 0);
            upLauncherGo = MakeBtn("↓ " + Lang.T("下载新版本"), Px(110), Px(34), true);
            upLauncherGo.Margin = Padding.Empty;
            upLauncherCheck.Click += delegate { CheckLauncherUpdateNow(); };
            upLauncherGo.Click += delegate
            {
                // 下载新版本: 打开 GitHub Releases 页面(真实可下载 exe)
                try { Process.Start("https://github.com/loudMore/dsh-launcher/releases/latest"); }
                catch { }
            };
            var lBtnFlow = new FlowLayoutPanel { Dock = DockStyle.Right, AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, BackColor = Color.Transparent, Padding = Padding.Empty };
            lBtnFlow.Controls.Add(upLauncherCheck);
            lBtnFlow.Controls.Add(upLauncherGo);
            lHead.Controls.Add(lTitle);
            lHead.Controls.Add(lBtnFlow);
            var lBody = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
            var lCur = new Label { Text = Lang.T("当前版本") + "  v" + LauncherVersion, AutoSize = true, Left = 0, Top = Px(2), ForeColor = DshTheme.Success, BackColor = Color.Transparent, Font = DshFonts.Small };
            upLauncherLatest = new Label { Text = Lang.T("最新版本") + "  " + Lang.T("未检查"), AutoSize = true, Left = Px(300), Top = Px(2), ForeColor = DshTheme.TextDim, BackColor = Color.Transparent, Font = DshFonts.Small };
            upLauncherNote = new Label { Text = "", AutoSize = true, Left = 0, Top = Px(26), ForeColor = DshTheme.TextFaint, BackColor = Color.Transparent, Font = DshFonts.Small };
            lBody.Controls.Add(lCur);
            lBody.Controls.Add(upLauncherLatest);
            lBody.Controls.Add(upLauncherNote);
            lch.Controls.Add(lBody);
            lch.Controls.Add(lHead);

            // dsh 卡: 标题行(左标题 + 右侧停靠按钮流), 下方版本信息 —— 初始布局即正确
            var dsh = new RoundPanel { Padding = new Padding(Px(18), Px(12), Px(18), Px(12)) };
            var head = new Panel { Dock = DockStyle.Top, Height = Px(36), BackColor = Color.Transparent };
            var title = new Label { Text = "DeepSeek Harness (dsh)", AutoSize = true, Dock = DockStyle.Left, ForeColor = DshTheme.TextMain, BackColor = Color.Transparent, Font = DshFonts.CardTitle, TextAlign = ContentAlignment.MiddleLeft };
            upCheck = MakeBtn("↻ 检查更新", Px(100), Px(36), false);
            upCheck.Margin = new Padding(0, 0, Px(8), 0);
            upApply = MakeBtn("↓ " + Lang.T("立即升级"), Px(100), Px(36), true);
            upApply.Margin = Padding.Empty;
            upCheck.Click += delegate { CheckUpdateNow(); };
            upApply.Click += delegate { ApplyUpdateNow(); };
            var btnFlow = new FlowLayoutPanel { Dock = DockStyle.Right, AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, BackColor = Color.Transparent, Padding = Padding.Empty };
            btnFlow.Controls.Add(upCheck);
            btnFlow.Controls.Add(upApply);
            head.Controls.Add(title);
            head.Controls.Add(btnFlow);

            var body = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
            var curL = new Label { Text = "当前版本", AutoSize = true, Left = 0, Top = Px(6), ForeColor = DshTheme.TextDim, BackColor = Color.Transparent, Font = DshFonts.Small };
            var latL = new Label { Text = "最新版本", AutoSize = true, Left = 0, Top = Px(32), ForeColor = DshTheme.TextDim, BackColor = Color.Transparent, Font = DshFonts.Small };
            upCurrent = new Label { Text = "检测中…", AutoSize = true, Left = Px(76), Top = Px(4), ForeColor = Color.White, BackColor = Color.Transparent, Font = DshFonts.Version };
            upLatest = new Label { Text = "—", AutoSize = true, Left = Px(76), Top = Px(30), ForeColor = DshTheme.TextDim, BackColor = Color.Transparent, Font = DshFonts.VersionPlain };
            upNote = new EllipsisLabel { Text = "", Left = 0, Top = Px(58), Width = 10, Height = Px(18), ForeColor = DshTheme.TextDim, BackColor = Color.Transparent, Font = DshFonts.Body };
            body.Controls.Add(curL); body.Controls.Add(latL);
            body.Controls.Add(upCurrent); body.Controls.Add(upLatest); body.Controls.Add(upNote);
            body.Resize += delegate { upNote.Width = Math.Max(10, body.Width); };   // 长更新说明单行省略, 不顶出卡片
            dsh.Controls.Add(body);
            dsh.Controls.Add(head);

            var plugins = new RoundPanel { Padding = new Padding(Px(14), Px(6), Px(14), Px(14)) };
            var pHead = new Panel { Dock = DockStyle.Top, Height = Px(34), BackColor = Color.Transparent };
            var pTitle = new Label { Text = "插件更新", AutoSize = true, Dock = DockStyle.Left, ForeColor = DshTheme.TextMain, BackColor = Color.Transparent, Font = DshFonts.CardTitle, TextAlign = ContentAlignment.MiddleLeft };
            pHead.Controls.Add(pTitle);
            upPluginDetail = MakeMonoBox();
            upPluginDetail.Dock = DockStyle.Fill;
            plugins.Controls.Add(upPluginDetail);
            plugins.Controls.Add(pHead);

            AddRow(stack, lch, SizeType.Absolute, Px(104));
            AddRow(stack, dsh, SizeType.Absolute, Px(136));
            AddRow(stack, plugins, SizeType.Percent, 100);
            page.Controls.Add(stack);
            return page;
        }

        // ============ 日志页 ============
        Panel BuildLogsPage()
        {
            var page = new BufPanel { BackColor = DshTheme.Bg };
            var stack = MakeVStack();
            AddRow(stack, MakeSection(Lang.T("日志查看")), SizeType.Absolute, Px(32));

            var toolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                WrapContents = false,
                BackColor = Color.Transparent,
                Padding = new Padding(0, Px(4), 0, 0)
            };
            logKind = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = Px(190),
                BackColor = DshTheme.BgInput,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0, Px(6), Px(8), 0)
            };
            logKind.Items.AddRange(new object[] { "启动器日志 (launcher.log)", "服务日志 (dsh.log)" });
            logKind.SelectedIndex = 0;
            logKind.SelectedIndexChanged += delegate { RefreshLogViews(); };
            logRefresh = MakeBtn("刷新", Px(76), Px(36), false); logRefresh.Margin = new Padding(0, 0, Px(8), 0); logRefresh.Click += delegate { RefreshLogViews(); };
            logClear = MakeDangerBtn("清空当前日志", Px(106), Px(36)); logClear.Margin = new Padding(0, 0, Px(8), 0); logClear.Click += delegate { ClearLogNow(); };
            logOpen = MakeBtn("打开日志目录", Px(106), Px(36), false); logOpen.Margin = Padding.Empty; logOpen.Click += delegate { OpenFolder(cfg.LogDir); };
            toolbar.Controls.AddRange(new Control[] { logKind, logRefresh, logClear, logOpen });

            var card = new RoundPanel { Padding = new Padding(Px(12), Px(6), Px(12), Px(12)) };
            var head = new Panel { Dock = DockStyle.Top, Height = Px(32), BackColor = Color.Transparent };
            var headTitle = new Label { Text = "日志内容", AutoSize = true, Dock = DockStyle.Left, ForeColor = DshTheme.TextMain, BackColor = Color.Transparent, Font = DshFonts.CardTitle, TextAlign = ContentAlignment.MiddleLeft };
            logAuto = new CheckBox { Text = "自动刷新", AutoSize = true, Dock = DockStyle.Right, ForeColor = DshTheme.TextDim, BackColor = Color.Transparent, Checked = true, Cursor = Cursors.Hand, TextAlign = ContentAlignment.MiddleRight };
            head.Controls.Add(headTitle);
            head.Controls.Add(logAuto);

            logText = MakeMonoBox();
            logText.Dock = DockStyle.Fill;
            logText.WordWrap = true;   // 长行自动换行, 避免横向截断
            card.Controls.Add(logText);
            card.Controls.Add(head);

            AddRow(stack, toolbar, SizeType.Absolute, Px(44));
            AddRow(stack, card, SizeType.Percent, 100);
            page.Controls.Add(stack);
            return page;
        }

        // ============ 设置页 ============
        Panel BuildSettingsPage()
        {
            var page = new BufPanel { BackColor = DshTheme.Bg };
            var stack = MakeVStack();
            AddRow(stack, MakeSection(Lang.T("设置")), SizeType.Absolute, Px(32));

            var card = new RoundPanel();
            var grid = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(Px(14)), ColumnCount = 2, RowCount = 10, BackColor = Color.Transparent };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Px(124)));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int i = 0; i < 10; i++)
                grid.RowStyles.Add(new RowStyle(SizeType.Absolute, Px(i < 9 ? 38 : 48)));

            setCmd = NewSettingBox(grid, 0, 6, Lang.T("dsh 命令"), cfg.DshCommand);
            setPort = NewSettingBox(grid, 1, 7, Lang.T("服务端口"), cfg.Port.ToString());
            setHome = NewSettingBox(grid, 2, 8, "DSH_HOME", cfg.DshHome);
            setPlugins = NewSettingBox(grid, 3, 2, Lang.T("插件目录"), cfg.PluginsRoot);
            setLog = NewSettingBox(grid, 4, 4, Lang.T("日志目录"), cfg.LogDir);
            setNpm = NewSettingBox(grid, 5, 2, Lang.T("npm 包名"), cfg.NpmPackage);
            setLupUrl = NewSettingBox(grid, 6, 9, Lang.T("启动器更新源"), cfg.LauncherUpdateUrl);
            setProxy = NewSettingBox(grid, 7, 7, Lang.T("代理地址"), cfg.Proxy);

            // 语言选择(带地球图标 + 国旗)
            var langCell = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Margin = Padding.Empty };
            langCell.Paint += delegate(object s, PaintEventArgs e)
            {
                int isz = Px(16);
                Glyphs.Draw(e.Graphics, 10, new Rectangle(Px(2), (langCell.Height - isz) / 2, isz, isz), DshTheme.TextDim);
            };
            var langLbl = new Label { Text = Lang.T("界面语言"), Dock = DockStyle.Fill, ForeColor = DshTheme.TextMain, BackColor = Color.Transparent, TextAlign = ContentAlignment.MiddleLeft, Margin = Padding.Empty, Padding = new Padding(Px(24), 0, 0, 0) };
            langCell.Controls.Add(langLbl);
            setLang = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Dock = DockStyle.Fill,
                BackColor = DshTheme.BgInput,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0, Px(5), 0, Px(5))
            };
            setLang.Items.Add("🌐 " + Lang.T("跟随系统") + " (Auto)");
            setLang.Items.Add("🇨🇳 简体中文");
            setLang.Items.Add("🇺🇸 English");
            setLang.SelectedIndex = (cfg.Language == "zh") ? 1 : (cfg.Language == "en" ? 2 : 0);
            grid.Controls.Add(langCell, 0, 8);
            grid.Controls.Add(setLang, 1, 8);

            var btnCell = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, BackColor = Color.Transparent, Margin = Padding.Empty, Padding = new Padding(0, Px(4), 0, 0) };
            setDetect = MakeBtn(Lang.T("自动检测"), Px(88), Px(36), false); setDetect.Margin = new Padding(0, 0, Px(10), 0); setDetect.Click += delegate { RunDetect(); };
            setSave = MakeBtn(Lang.T("保存设置"), Px(88), Px(36), true); setSave.Margin = new Padding(0, 0, Px(10), 0); setSave.Click += delegate { SaveSettings(); };
            setOpenCfg = MakeBtn(Lang.T("打开配置文件"), Px(100), Px(36), false); setOpenCfg.Margin = new Padding(0, 0, Px(10), 0); setOpenCfg.Click += delegate { OpenConfigFile(); };
            var setShortcut = MakeBtn(Lang.T("桌面快捷方式"), Px(100), Px(36), false); setShortcut.Margin = new Padding(0, 0, Px(10), 0); setShortcut.Click += delegate { CreateDesktopShortcut(); };
            var setProxyDetect = MakeBtn(Lang.T("检测代理"), Px(88), Px(36), false); setProxyDetect.Margin = Padding.Empty; setProxyDetect.Click += delegate { DetectProxyUi(); };
            btnCell.Controls.AddRange(new Control[] { setDetect, setSave, setOpenCfg, setShortcut, setProxyDetect });
            grid.Controls.Add(btnCell, 1, 9);

            card.Controls.Add(grid);

            AddRow(stack, card, SizeType.Percent, 100);
            page.Controls.Add(stack);
            return page;
        }

        // 从 awesome 精选列表 Markdown 解析仓库链接 (镜像兜底)
        public static List<StoreItem> ParseMdList(string md)
        {
            var list = new List<StoreItem>();
            if (string.IsNullOrEmpty(md)) return list;
            try
            {
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                string[] lines = md.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
                foreach (string raw in lines)
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line.IndexOf("github.com/") < 0) continue;

                    if (line.StartsWith("|"))
                    {
                        // 表格行: | 需求 | [a](u) · [b](u) | 为什么 |  → 描述取链接所在格之后的第一个非空格
                        string[] cells = line.Split('|');
                        for (int c = 0; c < cells.Length; c++)
                        {
                            string cell = cells[c].Trim();
                            if (cell.Length == 0) continue;
                            int pos = 0;
                            while (pos < cell.Length)
                            {
                                Match m = LinkRe.Match(cell, pos);
                                if (!m.Success) break;
                                pos = m.Index + m.Length;
                                string full = OwnerRepo(m.Groups[2].Value);
                                if (full == null || !seen.Add(full)) continue;
                                string desc = "";
                                for (int c2 = c + 1; c2 < cells.Length; c2++)
                                {
                                    string dc = CleanDesc(cells[c2]);
                                    if (dc.Length > 0) { desc = dc; break; }
                                }
                                list.Add(MakeItem(full, m.Groups[1].Value, desc));
                            }
                        }
                        continue;
                    }

                    // 非表格行: 标题 "### [name — desc](u)" 或普通条目 "- [name](u) - desc"
                    int p2 = 0;
                    int lastEnd = 0;
                    while (p2 < line.Length)
                    {
                        Match m = LinkRe.Match(line, p2);
                        if (!m.Success) break;
                        lastEnd = m.Index + m.Length;
                        p2 = lastEnd;
                        string full = OwnerRepo(m.Groups[2].Value);
                        if (full == null || !seen.Add(full)) continue;
                        string label = m.Groups[1].Value.Trim();
                        string name = full.Substring(full.IndexOf('/') + 1);
                        string desc = "";
                        int dash = label.IndexOf(" — ");
                        if (dash < 0) dash = label.IndexOf(" - ");
                        if (dash > 0)
                        {
                            string left = label.Substring(0, dash).Trim();
                            if (left.Length > 0) name = left;
                            desc = CleanDesc(label.Substring(dash + 3));
                        }
                        if (desc.Length == 0)
                        {
                            string rest = line.Substring(lastEnd).Trim();
                            rest = rest.TrimStart('-', '·', '—', '–', '|', ' ').Trim();
                            desc = CleanDesc(rest);
                        }
                        list.Add(MakeItem(full, name, desc));
                    }
                }
            }
            catch { }
            return list;
        }

        static readonly Regex LinkRe = new Regex("\\[([^\\]\\n]*)\\]\\((https://github\\.com/[^)\\s]+)\\)", RegexOptions.Compiled);

        static string OwnerRepo(string url)
        {
            if (string.IsNullOrEmpty(url)) return null;
            url = url.TrimEnd('/', '.', ')', '#');
            if (url.IndexOf("/issues/") >= 0 || url.IndexOf("/tree/") >= 0 || url.IndexOf("/blob/") >= 0 || url.IndexOf("/topics/") >= 0) return null;
            string full = url.Substring("https://github.com/".Length);
            if (full.Length == 0 || full.IndexOf('/') < 0 || full.Split('/').Length != 2) return null;
            return full;
        }

        static StoreItem MakeItem(string full, string name, string desc)
        {
            if (string.IsNullOrEmpty(name)) name = full.Substring(full.IndexOf('/') + 1);
            if (desc.Length > 140) desc = desc.Substring(0, 140) + "…";
            return new StoreItem { FullName = full, Name = name, Url = "https://github.com/" + full, Desc = desc, Stars = -1 };
        }

        static string CleanDesc(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            s = Regex.Replace(s, "\\[([^\\]]*)\\]\\([^)]*\\)", "$1");   // 链接只留文字
            s = Regex.Replace(s, "<[^>]+>", " ");                        // 去 HTML 标签
            s = s.Replace("`", "").Replace("|", " ").Replace("·", " ");
            s = Regex.Replace(s, "\\s+", " ").Trim();
            s = s.TrimStart('-', '—', '–', '*', '>', ':').Trim();
            return s;
        }

        public static List<StoreItem> ParseStoreJson(string json)
        {
            var list = new List<StoreItem>();
            if (string.IsNullOrEmpty(json)) return list;
            try
            {
                int arr = json.IndexOf("\"items\"");
                if (arr < 0) return list;
                int start = json.IndexOf('[', arr);
                if (start < 0) return list;
                int depth = 0;
                int objStart = -1;
                bool inStr = false;
                bool esc = false;
                for (int i = start; i < json.Length; i++)
                {
                    char c = json[i];
                    if (inStr)
                    {
                        if (esc) esc = false;
                        else if (c == '\\') esc = true;
                        else if (c == '"') inStr = false;
                        continue;
                    }
                    if (c == '"') { inStr = true; continue; }
                    if (c == '{')
                    {
                        depth++;
                        if (depth == 2) objStart = i;
                    }
                    else if (c == '}')
                    {
                        depth--;
                        if (depth == 1 && objStart >= 0)
                        {
                            var it = ParseStoreItem(json.Substring(objStart, i - objStart + 1));
                            if (it != null) list.Add(it);
                            objStart = -1;
                        }
                    }
                    else if (c == ']' && depth == 1) break;
                }
            }
            catch { }
            return list;
        }

        public static StoreItem ParseStoreItem(string obj)
        {
            try
            {
                var it = new StoreItem();
                it.FullName = JsStr(obj, "\"full_name\"");
                it.Url = JsStr(obj, "\"html_url\"");
                it.Desc = JsStr(obj, "\"description\"");
                it.Lang = JsStr(obj, "\"language\"");
                it.Branch = JsStr(obj, "\"default_branch\"");
                string pushed = JsStr(obj, "\"pushed_at\"");
                if (pushed.Length >= 10) it.Pushed = pushed.Substring(0, 10);
                Match m = Regex.Match(obj, "\"stargazers_count\"\\s*:\\s*(\\d+)");
                if (m.Success) int.TryParse(m.Groups[1].Value, out it.Stars);
                if (string.IsNullOrEmpty(it.FullName)) return null;
                int slash = it.FullName.IndexOf('/');
                it.Name = slash >= 0 ? it.FullName.Substring(slash + 1) : it.FullName;
                return it;
            }
            catch { return null; }
        }

        public static string JsStr(string obj, string key)
        {
            int k = obj.IndexOf(key);
            if (k < 0) return "";
            int colon = obj.IndexOf(':', k);
            if (colon < 0) return "";
            int i = colon + 1;
            while (i < obj.Length && (obj[i] == ' ' || obj[i] == '\t')) i++;
            if (i < obj.Length && obj[i] == '"')
            {
                var sb = new StringBuilder();
                for (int j = i + 1; j < obj.Length; j++)
                {
                    char c = obj[j];
                    if (c == '\\' && j + 1 < obj.Length)
                    {
                        char n = obj[j + 1];
                        if (n == 'n') { sb.Append('\n'); j++; }
                        else if (n == '"') { sb.Append('"'); j++; }
                        else if (n == '\\') { sb.Append('\\'); j++; }
                        else sb.Append(c);
                    }
                    else if (c == '"') break;
                    else sb.Append(c);
                }
                return sb.ToString();
            }
            if (i < obj.Length && obj[i] == 'n') return "";
            return "";
        }

        public void InstallFromStore(StoreItem it)
        {
            if (string.IsNullOrEmpty(currentEnv.GitPath))
            {
                DarkDialog.Show(this, Lang.T("未检测到 Git：请先到「环境」页安装或手动选择 git.exe"), Lang.T("安装"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            string target = Path.Combine(cfg.PluginsRoot, it.Name);
            if (Directory.Exists(target) || File.Exists(target))
            {
                DarkDialog.Show(this, Lang.T("该插件已安装（目录已存在）：\n") + target, Lang.T("安装"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            SetStatus(Lang.T("正在安装插件") + " " + it.Name + " …", DshTheme.Info);
            SetBusy(true);
            var worker = new Thread(delegate ()
            {
                string r = RunGit("clone \"" + it.Url + "\" \"" + target + "\"", 300000);
                AppendLog("[plugin] clone " + it.Url + (r == null ? " (超时/失败)" : " 完成"));
                Ui(delegate
                {
                    SetBusy(false);
                    if (r == null)
                        ShowError(Lang.T("插件安装失败"), Lang.T("克隆失败（网络或地址错误）。\n详见「日志」页。"));
                    else
                    {
                        SetStatus(Lang.T("插件") + " " + it.Name + " " + Lang.T("已安装"), DshTheme.Success);
                        RenderPlugins();
                        DarkDialog.Show(this, Lang.T("插件") + " " + it.Name + " " + Lang.T("已安装。\n重启服务后生效；若插件在仓库子目录，请在 DSH 设置中配置挂载。"), Lang.T("安装"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                });
            });
            worker.IsBackground = true;
            worker.Start();
        }

        // 手动触发代理探测并回填输入框
        void DetectProxyUi()
        {
            setProxy.Text = "";
            SetStatus("正在探测代理…", DshTheme.Info);
            var worker = new Thread(delegate ()
            {
                proxyChecked = false;
                detectedProxy = null;
                string p = ResolveProxy();
                Ui(delegate
                {
                    if (p != null)
                    {
                        setProxy.Text = p;
                        SetStatus("已检测到代理 " + p, DshTheme.Success);
                    }
                    else
                    {
                        SetStatus("未检测到可用代理", DshTheme.Warn);
                        DarkDialog.Show(this, "未检测到可用代理。\n\n提示：开启 Clash/v2rayN 等工具后，可勾选其「系统代理」模式，或在「代理地址」中手动填写（如 http://127.0.0.1:7890）。", "检测代理", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                });
            });
            worker.IsBackground = true;
            worker.Start();
        }

        TextBox NewSettingBox(TableLayoutPanel grid, int row, int iconId, string label, string value)
        {
            var cell = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Margin = Padding.Empty };
            cell.Paint += delegate(object s, PaintEventArgs e)
            {
                int isz = Px(16);
                Glyphs.Draw(e.Graphics, iconId, new Rectangle(Px(2), (cell.Height - isz) / 2, isz, isz), DshTheme.TextDim);
            };
            var l = new Label { Text = label, ForeColor = DshTheme.TextMain, BackColor = Color.Transparent, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Margin = Padding.Empty, Padding = new Padding(Px(24), 0, 0, 0) };
            cell.Controls.Add(l);
            var t = new TextBox { Text = value, Dock = DockStyle.Fill, BorderStyle = BorderStyle.FixedSingle, BackColor = DshTheme.BgInput, ForeColor = Color.White, Margin = new Padding(0, Px(5), 0, Px(5)) };
            grid.Controls.Add(cell, 0, row);
            grid.Controls.Add(t, 1, row);
            return t;
        }

        CheckBox NewSettingCheck(TableLayoutPanel grid, int row, string label, bool value)
        {
            var c = new CheckBox { Text = label, Checked = value, ForeColor = DshTheme.TextMain, BackColor = Color.Transparent, Dock = DockStyle.Fill, Cursor = Cursors.Hand, TextAlign = ContentAlignment.MiddleLeft, Margin = Padding.Empty };
            grid.SetColumnSpan(c, 2);
            grid.Controls.Add(c, 0, row);
            return c;
        }

        // ============ 状态栏 (28px, 顶部 1px 分隔线) ============
        void BuildStatusBar()
        {
            var bar = new Panel { Dock = DockStyle.Bottom, Height = Px(28), BackColor = DshTheme.BgDeep };
            bar.Paint += delegate(object s, PaintEventArgs e)
            {
                using (var pen = new Pen(DshTheme.BorderSoft, 1f))
                    e.Graphics.DrawLine(pen, 0, 0, bar.Width, 0);
            };

            sbDot = new Label { Text = "●", AutoSize = true, Left = Px(14), Top = Px(9), ForeColor = DshTheme.TextDim, BackColor = Color.Transparent, Font = DshFonts.DotSmall };
            sbText = new Label { Text = "准备就绪", AutoSize = true, Left = Px(30), Top = Px(7), ForeColor = DshTheme.TextDim, BackColor = Color.Transparent, Font = DshFonts.Caption };
            sbRight = new Label { Text = "", AutoSize = true, Top = Px(7), ForeColor = DshTheme.TextFaint, BackColor = Color.Transparent, Font = DshFonts.Caption, Anchor = AnchorStyles.Right | AnchorStyles.Top, TextAlign = ContentAlignment.MiddleRight };

            bar.Controls.Add(sbDot);
            bar.Controls.Add(sbText);
            bar.Controls.Add(sbRight);
            Controls.Add(bar);
        }

        // ============ 托盘 ============
        void BuildTray()
        {
            tray = new NotifyIcon { Text = "DeepSeek Harness", Visible = true };
            try { tray.Icon = new Icon(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "deepseek.ico")); }
            catch { tray.Icon = SystemIcons.Application; }

            var menu = new ContextMenuStrip();
            menu.Items.Add(Lang.T("打开启动器"), null, delegate { ShowFromTray(); });
            menu.Items.Add(Lang.T("打开浏览器"), null, delegate { OpenBrowser(); });
            menu.Items.Add(Lang.T("启动 / 停止服务"), null, delegate { ToggleService(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(Lang.T("退出"), null, delegate { QuitApp(); });
            tray.ContextMenuStrip = menu;
            tray.DoubleClick += delegate { ShowFromTray(); };
        }

        void CheckReopenFlag()
        {
            try
            {
                if (File.Exists(Program.ReopenFlagPath()))
                {
                    File.Delete(Program.ReopenFlagPath());
                    ShowFromTray();
                }
            }
            catch { }
        }

        void ShowFromTray()
        {
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
            BringToFront();
        }

        void HideToTray()
        {
            Hide();
            tray.Visible = true;
            try { tray.ShowBalloonTip(2000, "DeepSeek Harness", "已最小化到系统托盘，双击图标重新打开。", ToolTipIcon.Info); }
            catch { }
        }

        void QuitApp()
        {
            quitting = true;
            if (serverProc != null && !serverProc.HasExited)
            {
                var choice = DarkDialog.Show(this, "服务仍在运行。\n\n是否停止服务后再退出？", "退出 DeepSeek Harness",
                    MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (choice == DialogResult.Yes)
                {
                    KillProcessTree(serverProc.Id);
                }
                else if (choice == DialogResult.Cancel)
                {
                    quitting = false;
                    return;
                }
            }
            tray.Visible = false;
            try { tray.Dispose(); } catch { }
            Close();
        }

        void ToggleService()
        {
            if (IsPortOpen(cfg.Port)) StopService();
            else StartService();
        }

        // ============ 导航切换 ============
        void SwitchPage(int idx)
        {
            for (int i = 0; i < navs.Length; i++)
            {
                navs[i].Active = (i == idx);
                pages[i].Visible = (i == idx);
            }
            pages[idx].Invalidate(true);   // 切页后整页(含所有自绘子控件)干净重绘, 不留残影
            if (idx == 0) RenderOverview();
            else if (idx == 1) RenderEnvironment();
            else if (idx == 2) RenderPlugins();
            else if (idx == 3) RenderUpdateInfo();
            else if (idx == 4) RefreshLogViews();
        }

        // ============ 状态与UI刷新 ============
        void Ui(Action a)
        {
            try { if (IsHandleCreated && !IsDisposed) BeginInvoke((MethodInvoker)delegate { a(); }); }
            catch { }
        }

        void SetStatus(string text)
        {
            sbText.Text = text;
        }

        void SetStatus(string text, Color color)
        {
            sbText.Text = text;
            sbText.ForeColor = color;
            sbDot.ForeColor = color;
        }

        void SetBusy(bool busy)
        {
            progress.Visible = busy;
        }

        bool ServiceRunning()
        {
            return IsPortOpen(cfg.Port);
        }

        void UpdateSbRight()
        {
            sbRight.Text = string.Format("端口 {0} · 启动器 v{1}", cfg.Port, LauncherVersion);
            sbRight.Left = ClientSize.Width - sbRight.Width - Px(12);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (sbRight != null) UpdateSbRight();
        }

        // ============ 概览渲染 ============
        void RenderOverview()
        {
            bool running = ServiceRunning();
            bool nodeOk = !string.IsNullOrEmpty(currentEnv.NodePath);
            bool dshOk = !string.IsNullOrEmpty(currentEnv.DshPath);
            string url = string.Format("http://127.0.0.1:{0}", cfg.Port);
            string ver = string.IsNullOrEmpty(currentEnv.DshVersion) ? "?" : currentEnv.DshVersion;

            if (!envDetected)
            {
                // 首次检测完成前的中性占位: 不展示任何"未检测到"的假阴性状态
                ovDot.ForeColor = DshTheme.TextDim;
                ovStatusTitle.Text = "检测中…";
                ovStatusSub.Text = "正在检测运行环境与服务状态，请稍候";
                ovTertiary.Visible = false;
            }
            else if (!dshOk || !nodeOk)
            {
                ovDot.ForeColor = DshTheme.Warn;
                ovStatusTitle.Text = !nodeOk ? "未检测到 Node.js" : "未检测到 dsh";
                ovStatusSub.Text = "首次使用请点击「一键安装」，约 1-3 分钟即可就绪";
                ovPrimary.Text = "↓ " + Lang.T("一键安装");
                ovSecondary.Text = "↻ " + Lang.T("重新检测");
                ovTertiary.Visible = false;
            }
            else if (!running)
            {
                ovDot.ForeColor = DshTheme.TextDim;
                ovStatusTitle.Text = "服务未启动";
                ovStatusSub.Text = string.Format("点击「一键启动」开始使用 · dsh v{0}", ver);
                ovPrimary.Text = "▶ " + Lang.T("一键启动");
                ovSecondary.Text = "↻ " + Lang.T("检查更新");
                ovTertiary.Visible = false;
            }
            else
            {
                ovDot.ForeColor = DshTheme.Success;
                ovStatusTitle.Text = "服务运行中";
                ovStatusSub.Text = string.Format("{0} · dsh v{1}", url, ver);
                ovPrimary.Text = "▶ " + Lang.T("打开浏览器");
                ovSecondary.Text = "■ " + Lang.T("停止服务");
                ovTertiary.Visible = true;
                ovTertiary.Text = "↻ " + Lang.T("重启服务");
            }

            bool busy = starting || stopping || installing || updating;
            StyleEnabled(ovPrimary, !busy, true);
            StyleEnabled(ovSecondary, !busy, false);
            StyleEnabled(ovTertiary, !busy, false);

            string envLine;
            if (!nodeOk || !dshOk) envLine = "环境不完整（点击一键安装）";
            else envLine = string.Format("Node {0} · npm {1} · git {2} · dsh {3}",
                nodeOk ? "✓" : "✗",
                string.IsNullOrEmpty(currentEnv.NpmPath) ? "✗" : "✓",
                string.IsNullOrEmpty(currentEnv.GitPath) ? "✗" : "✓",
                dshOk ? "✓" : "✗");
            string updLine;
            Color updColor = DshTheme.TextDim;
            if (currentUpdate.HasUpdate) { updLine = "有更新：" + currentUpdate.Detail; updColor = DshTheme.Warn; }
            else if (!string.IsNullOrEmpty(currentUpdate.DshLatest)) { updLine = "已是最新"; updColor = DshTheme.Success; }
            else updLine = "更新未检查";

            // 状态芯片
            ovChips.Controls.Clear();
            if (!envDetected)
            {
                AddChip(ovChips, "正在检测运行环境…", DshTheme.TextDim);
            }
            else
            {
                AddChip(ovChips, "dsh " + (string.IsNullOrEmpty(currentEnv.DshVersion) ? "未安装" : "v" + currentEnv.DshVersion), dshOk ? DshTheme.Success : DshTheme.Error);
                AddChip(ovChips, url, running ? DshTheme.Success : DshTheme.TextDim);
                AddChip(ovChips, "Node " + (nodeOk ? "✓" : "✗"), nodeOk ? DshTheme.Success : DshTheme.Error);
                AddChip(ovChips, "npm " + (string.IsNullOrEmpty(currentEnv.NpmPath) ? "✗" : "✓"), string.IsNullOrEmpty(currentEnv.NpmPath) ? DshTheme.Error : DshTheme.Success);
                AddChip(ovChips, "git " + (string.IsNullOrEmpty(currentEnv.GitPath) ? "✗" : "✓"), string.IsNullOrEmpty(currentEnv.GitPath) ? DshTheme.Error : DshTheme.Success);
                AddChip(ovChips, "插件 " + currentEnv.PluginDirs + " 个", DshTheme.TextDim);
                AddChip(ovChips, updLine, updColor);
            }

            ovLog.Text = ReadTail(Path.Combine(cfg.LogDir, "launcher.log"), 8);
            ovLog.SelectionStart = ovLog.Text.Length;
            ovLog.ScrollToCaret();
            UpdateSbRight();
        }

        // 首页状态芯片
        void AddChip(FlowLayoutPanel host, string text, Color color)
        {
            var chip = new ChipLabel
            {
                Text = text,
                ForeColor = color
            };
            host.Controls.Add(chip);
        }

        void RenderEnvironment()
        {
            envHost.ClearAll();
            envHost.BeginAdd();

            AddEnvSection(envHost, "已装工具");
            AddEnvRow(envHost, "Node.js", currentEnv.NodePath, currentEnv.NodeVersion);
            AddEnvRow(envHost, "npm", currentEnv.NpmPath, currentEnv.NpmVersion);
            AddEnvRow(envHost, "git", currentEnv.GitPath, currentEnv.GitVersion);
            AddEnvRow(envHost, "dsh", currentEnv.DshPath, currentEnv.DshVersion);

            // 缺失环境 → 多策略解决方案: 自动安装 / 手动选择 / 目录扫描, 附带小白友好提示
            if (string.IsNullOrEmpty(currentEnv.NodePath))
                AddEnvActionRow2(envHost, "未检测到 Node.js：可点「一键安装」自动装好（含国内镜像），或选择已安装的 node.exe", "一键安装", delegate { InstallDshNow(); }, "手动选择", delegate { PickTool("node"); }, "扫描目录", delegate { ScanToolDir("node"); });
            if (string.IsNullOrEmpty(currentEnv.NpmPath))
                AddEnvActionRow2(envHost, "未检测到 npm：它随 Node.js 一起安装，通常在 nodejs 安装目录内", "手动选择", delegate { PickTool("npm"); }, "扫描目录", delegate { ScanToolDir("npm"); });
            if (string.IsNullOrEmpty(currentEnv.GitPath))
                AddEnvActionRow2(envHost, "未检测到 Git：插件管理需要它，可从镜像站下载安装，或选择已装的 git.exe", "安装 Git", delegate { OpenMirrorGit(); }, "手动选择", delegate { PickTool("git"); }, "扫描目录", delegate { ScanToolDir("git"); });
            if (string.IsNullOrEmpty(currentEnv.DshPath))
                AddEnvActionRow2(envHost, "未检测到 dsh：点「一键安装」，或手动选择 dsh.cmd（通常在 npm 全局目录，可用 npm root -g 查看）", "一键安装", delegate { InstallDshNow(); }, "手动选择", delegate { PickTool("dsh"); }, "扫描目录", delegate { ScanToolDir("dsh"); });

            AddEnvSection(envHost, "关键目录");
            AddPathRow(envHost, "数据目录 (DSH_HOME)", cfg.DshHome);
            AddPathRow(envHost, "插件目录", cfg.PluginsRoot);
            AddPathRow(envHost, "日志目录", cfg.LogDir);
        }

        void AddEnvSection(StackPanel host, string text)
        {
            var lbl = new Label
            {
                Text = text,
                AutoSize = false,
                ForeColor = DshTheme.TextMain,
                Font = DshFonts.CardTitle,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };
            host.Add(lbl, Px(24));
        }

        void AddEnvActionRow(StackPanel host, string text, string btnText, Action action)
        {
            AddEnvActionRow2(host, text, btnText, action, "", null, "", null);
        }

        void AddEnvActionRow2(StackPanel host, string text, string b1, Action a1, string b2, Action a2)
        {
            AddEnvActionRow2(host, text, b1, a1, b2, a2, "", null);
        }

        void AddEnvActionRow2(StackPanel host, string text, string b1, Action a1, string b2, Action a2, string b3, Action a3)
        {
            var row = new Panel { BackColor = DshTheme.BgInput };
            row.Paint += delegate(object s, PaintEventArgs e)
            {
                var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var p = Program.RoundRectPath(new Rectangle(0, 0, row.Width - 1, row.Height - 1), (int)Math.Round(8 * S)))
                using (var pen = new Pen(DshTheme.Warn, 1f)) g.DrawPath(pen, p);
            };
            var lbl = new Label
            {
                Text = text,
                AutoSize = false,
                Location = new Point(Px(12), Px(6)),
                ForeColor = DshTheme.Warn,
                BackColor = Color.Transparent,
                Font = DshFonts.Body
            };
            lbl.Width = Math.Max(10, row.Width - Px(150));
            var btns = new List<ModernButton>();
            Action<string, Action> addBtn = delegate(string t, Action a)
            {
                if (string.IsNullOrEmpty(t)) return;
                var b = MakeBtn(t, Px(t.Length > 4 ? 104 : 96), Px(26), t.IndexOf("安装") >= 0);
                b.Anchor = AnchorStyles.Right | AnchorStyles.Top;
                b.Click += delegate { a(); };
                row.Controls.Add(b);
                btns.Add(b);
            };
            addBtn(b3, a3); addBtn(b2, a2); addBtn(b1, a1);
            row.Controls.Add(lbl);
            row.Resize += delegate
            {
                int x = row.Width - Px(10);
                for (int i = 0; i < btns.Count; i++)
                {
                    btns[i].Location = new Point(x - btns[i].Width, Px(11));
                    x -= btns[i].Width + Px(8);
                }
                lbl.Width = Math.Max(10, x - Px(10));
            };
            host.Add(row, Px(50));
        }

        // 手动选择工具文件
        void PickTool(string which)
        {
            using (var d = new OpenFileDialog())
            {
                d.Title = "选择工具文件";
                d.Filter = (which == "dsh" || which == "npm")
                    ? "命令文件 (*.cmd;*.exe)|*.cmd;*.exe|所有文件 (*.*)|*.*"
                    : "程序 (*.exe)|*.exe|所有文件 (*.*)|*.*";
                if (d.ShowDialog(this) != DialogResult.OK) return;
                if (which == "node") cfg.NodePath = d.FileName;
                else if (which == "npm") cfg.NpmPath = d.FileName;
                else if (which == "git") cfg.GitPath = d.FileName;
                else cfg.DshCommand = d.FileName;
                cfg.Save();
                RedetectNow();
            }
        }

        // 扫描目录找工具(当前层 + bin/cmd/一层子目录)
        void ScanToolDir(string which)
        {
            using (var d = new FolderBrowserDialog())
            {
                d.Description = "选择包含工具文件的目录（会自动向下查找 bin/cmd 子目录）";
                if (d.ShowDialog(this) != DialogResult.OK) return;
                string found = FindInDir(d.SelectedPath, which);
                if (string.IsNullOrEmpty(found))
                {
                    DarkDialog.Show(this, "在该目录下未找到对应工具。\n请选择工具所在的精确目录后重试。", "扫描目录", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (which == "node") cfg.NodePath = found;
                else if (which == "npm") cfg.NpmPath = found;
                else if (which == "git") cfg.GitPath = found;
                else cfg.DshCommand = found;
                cfg.Save();
                RedetectNow();
            }
        }

        static string FindInDir(string dir, string which)
        {
            string[] names;
            if (which == "node") names = new string[] { "node.exe" };
            else if (which == "npm") names = new string[] { "npm.cmd", "npm.exe" };
            else if (which == "git") names = new string[] { "git.exe" };
            else names = new string[] { "dsh.cmd", "dsh.exe", "dsh" };
            var dirs = new List<string>();
            dirs.Add(dir);
            try
            {
                dirs.AddRange(Directory.GetDirectories(dir));
                if (Directory.Exists(Path.Combine(dir, "bin"))) dirs.Add(Path.Combine(dir, "bin"));
                if (Directory.Exists(Path.Combine(dir, "cmd"))) dirs.Add(Path.Combine(dir, "cmd"));
            }
            catch { }
            foreach (string d0 in dirs)
                foreach (string n in names)
                {
                    try { if (File.Exists(Path.Combine(d0, n))) return Path.Combine(d0, n); }
                    catch { }
                }
            return "";
        }

        void OpenMirrorGit()
        {
            try { Process.Start("https://npmmirror.com/mirrors/git-for-windows/"); }
            catch { }
        }

        void AddEnvRow(StackPanel host, string name, string path, string version)
        {
            var row = new Panel { BackColor = DshTheme.BgInput };
            row.Paint += delegate(object s, PaintEventArgs e)
            {
                var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var p = Program.RoundRectPath(new Rectangle(0, 0, row.Width - 1, row.Height - 1), (int)Math.Round(8 * S)))
                using (var pen = new Pen(DshTheme.BorderSoft, 1f)) g.DrawPath(pen, p);
            };
            bool ok = !string.IsNullOrEmpty(path);
            var dot = MakeDot(ok ? DshTheme.Success : DshTheme.Error, 11);
            dot.Location = new Point(Px(12), Px(9));
            var nameLbl = new Label
            {
                Text = name,
                AutoSize = true,
                Location = new Point(Px(32), Px(8)),
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Font = DshFonts.CardTitle
            };
            string ver = string.IsNullOrEmpty(version) ? "" : "  ·  " + version;
            var valLbl = new Label
            {
                Text = ok ? "已检测到" + ver : "未检测到",
                AutoSize = true,
                Location = new Point(Px(90), Px(9)),
                ForeColor = ok ? DshTheme.Success : DshTheme.Warn,
                BackColor = Color.Transparent,
                Font = DshFonts.Body
            };
            row.Controls.Add(dot);
            row.Controls.Add(nameLbl);
            row.Controls.Add(valLbl);
            WireHover(row, DshTheme.BgInput, DshTheme.RowHover);
            host.Add(row, Px(32));
        }

        void AddPathRow(StackPanel host, string label, string path)
        {
            var row = new Panel { BackColor = DshTheme.BgInput };
            row.Paint += delegate(object s, PaintEventArgs e)
            {
                var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var p = Program.RoundRectPath(new Rectangle(0, 0, row.Width - 1, row.Height - 1), (int)Math.Round(8 * S)))
                using (var pen = new Pen(DshTheme.BorderSoft, 1f)) g.DrawPath(pen, p);
            };
            var nameLbl = new Label
            {
                Text = label,
                AutoSize = true,
                Location = new Point(Px(12), Px(6)),
                ForeColor = DshTheme.TextDim,
                BackColor = Color.Transparent,
                Font = DshFonts.Small
            };
            var pathLbl = new Label
            {
                Text = path,
                AutoSize = false,
                AutoEllipsis = true,
                Location = new Point(Px(12), Px(21)),
                ForeColor = DshTheme.TextMain,
                BackColor = Color.Transparent,
                Font = DshFonts.MonoSmall
            };
            pathLbl.Width = Math.Max(10, row.Width - Px(124));
            var openBtn = MakeBtn("打开目录", Px(72), Px(24), false);
            openBtn.Location = new Point(0, Px(7));
            openBtn.Anchor = AnchorStyles.Right | AnchorStyles.Top;
            openBtn.Click += delegate { OpenFolder(path); };
            row.Controls.Add(nameLbl);
            row.Controls.Add(pathLbl);
            row.Controls.Add(openBtn);
            WireHover(row, DshTheme.BgInput, DshTheme.RowHover);
            row.Resize += delegate
            {
                openBtn.Location = new Point(row.Width - openBtn.Width - Px(10), Px(7));
                pathLbl.Width = Math.Max(10, row.Width - Px(134));
            };
            host.Add(row, Px(38));
        }

        // ============ 插件渲染 ============
        List<PluginItem> ScanPlugins()
        {
            var list = new List<PluginItem>();
            try
            {
                if (Directory.Exists(cfg.PluginsRoot))
                {
                    foreach (string d in Directory.GetDirectories(cfg.PluginsRoot))
                    {
                        string dirName = Path.GetFileName(d);
                        bool dis = dirName.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase);
                        string realName = dis ? dirName.Substring(0, dirName.Length - ".disabled".Length) : dirName;
                        var p = new PluginItem
                        {
                            Name = realName,
                            Path = d,
                            IsGit = Directory.Exists(Path.Combine(d, ".git")),
                            Disabled = dis
                        };
                        if (p.IsGit)
                        {
                            p.RemoteUrl = FirstLine(RunGit( string.Format("-C \"{0}\" config --get remote.origin.url", d), 10000));
                            p.Branch = FirstLine(RunGit( string.Format("-C \"{0}\" rev-parse --abbrev-ref HEAD", d), 10000));
                        }
                        list.Add(p);
                    }
                }
            }
            catch { }
            return list;
        }

        void RenderPlugins()
        {
            var plugins = ScanPlugins();
            pluginList.ClearAll();
            pluginList.BeginAdd();

            // 可更新插件集合(来自最近一次检查更新的结果)
            var updatable = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrEmpty(currentUpdate.PluginNames))
            {
                foreach (string n in currentUpdate.PluginNames.Split(','))
                    updatable.Add(n.Trim());
            }

            if (plugins.Count == 0)
            {
                var empty = new Label
                {
                    Text = Lang.T("未在插件目录中发现任何插件。\n\n插件目录: ") + cfg.PluginsRoot,
                    AutoSize = false,
                    ForeColor = DshTheme.TextDim,
                    Font = DshFonts.Body,
                    BackColor = Color.Transparent
                };
                pluginList.Add(empty, Px(60));
                pluginSummary.Text = "";
            }
            else
            {
                int gitCount = 0;
                foreach (var p in plugins)
                {
                    if (p.IsGit) gitCount++;
                    AddPluginRow(pluginList, p, updatable.Contains(p.Name));
                }
                pluginSummary.Text = string.Format(Lang.T("共 {0} 个目录 · {1} 个 git 仓库"), plugins.Count, gitCount);
            }
        }

        void AddPluginRow(StackPanel host, PluginItem p, bool updatable)
        {
            var row = new RoundPanel { BorderColor = DshTheme.BorderSoft };
            var nameLbl = new Label
            {
                Text = p.Name,
                AutoSize = false,
                AutoEllipsis = true,
                Location = new Point(Px(14), Px(8)),
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Font = DshFonts.CardTitle
            };
            nameLbl.Width = Px(180);
            var statusLbl = new Label
            {
                Text = p.Disabled ? "● " + Lang.T("已禁用") : (p.IsGit ? "● git" : "● " + Lang.T("普通目录")),
                AutoSize = true,
                Location = new Point(Px(200), Px(11)),
                ForeColor = p.Disabled ? DshTheme.Warn : (p.IsGit ? DshTheme.Success : DshTheme.TextDim),
                BackColor = Color.Transparent,
                Font = DshFonts.Body
            };
            var updateBadge = new Label
            {
                Text = "↻ " + Lang.T("可更新"),
                AutoSize = true,
                Location = new Point(Px(276), Px(11)),
                ForeColor = DshTheme.Warn,
                BackColor = Color.Transparent,
                Font = DshFonts.Body,
                Visible = updatable
            };
            string detail = p.IsGit
                ? (string.IsNullOrEmpty(p.RemoteUrl) ? p.Path : p.RemoteUrl) + (string.IsNullOrEmpty(p.Branch) ? "" : "   [" + p.Branch + "]")
                  + (pluginLocalHashes.ContainsKey(p.Name) ? "   本地 " + pluginLocalHashes[p.Name] : "")
                  + (pluginRemoteHashes.ContainsKey(p.Name) ? "  →  最新 " + pluginRemoteHashes[p.Name] : "")
                : p.Path;
            var detailLbl = new Label
            {
                Text = detail,
                AutoSize = false,
                AutoEllipsis = true,
                Location = new Point(Px(14), Px(34)),
                ForeColor = DshTheme.TextDim,
                BackColor = Color.Transparent,
                Font = DshFonts.MonoSmall
            };
            detailLbl.Width = Math.Max(10, row.Width - Px(320));

            var pullBtn = MakeBtn("↻ " + Lang.T("更新"), Px(76), Px(28), false);
            var openBtn = MakeBtn(Lang.T("目录"), Px(64), Px(28), false);
            var delBtn = MakeDangerBtn(Lang.T("卸载"), Px(64), Px(28));
            var toggleBtn = MakeBtn(p.Disabled ? Lang.T("启用") : Lang.T("禁用"), Px(64), Px(28), false);
            pullBtn.Anchor = AnchorStyles.Right | AnchorStyles.Top;
            openBtn.Anchor = AnchorStyles.Right | AnchorStyles.Top;
            delBtn.Anchor = AnchorStyles.Right | AnchorStyles.Top;
            toggleBtn.Anchor = AnchorStyles.Right | AnchorStyles.Top;
            pullBtn.Click += delegate { PullPlugin(p); };
            openBtn.Click += delegate { OpenFolder(p.Path); };
            delBtn.Click += delegate { UninstallPlugin(p); };
            toggleBtn.Click += delegate { TogglePlugin(p); };
            row.Controls.Add(nameLbl);
            row.Controls.Add(statusLbl);
            row.Controls.Add(updateBadge);
            row.Controls.Add(detailLbl);
            row.Controls.Add(toggleBtn);
            row.Controls.Add(delBtn);
            row.Controls.Add(openBtn);
            if (p.IsGit) row.Controls.Add(pullBtn);
            row.Resize += delegate
            {
                int x = row.Width - Px(12);
                toggleBtn.Location = new Point(x - toggleBtn.Width, Px(18)); x -= toggleBtn.Width + Px(8);
                delBtn.Location = new Point(x - delBtn.Width, Px(18)); x -= delBtn.Width + Px(8);
                openBtn.Location = new Point(x - openBtn.Width, Px(18)); x -= openBtn.Width + Px(8);
                if (p.IsGit) { pullBtn.Location = new Point(x - pullBtn.Width, Px(18)); x -= pullBtn.Width + Px(8); }
                detailLbl.Width = Math.Max(10, x - Px(14));
            };
            host.Add(row, Px(64));
        }

        // ---------- 插件安装 / 卸载 / 一键维护 ----------
        void InstallPlugin()
        {
            string input = DarkDialog.Input(this, Lang.T("安装插件"),
                Lang.T("支持两种方式安装:") + "\n\n  1. git 仓库地址 (克隆到插件目录)\n  2. npm 包名 (全局安装)\n\n" + Lang.T("示例") + ": https://github.com/user/plugin.git", "");
            if (string.IsNullOrEmpty(input)) return;
            // 注入防护: 拒绝含引号/管道/重定向等 shell 元字符的输入
            if (input.IndexOf('"') >= 0 || input.IndexOf('&') >= 0 || input.IndexOf('|') >= 0
                || input.IndexOf(';') >= 0 || input.IndexOf('>') >= 0 || input.IndexOf('<') >= 0
                || input.IndexOf('`') >= 0 || input.IndexOf('$') >= 0 || input.IndexOf('%') >= 0)
            {
                DarkDialog.Show(this, Lang.T("地址格式不正确，请输入完整的 git 仓库地址。"), Lang.T("安装插件"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            bool isUrl = input.IndexOf("://") >= 0 || input.StartsWith("git@") || input.StartsWith("http");
            if (!isUrl && (input.IndexOf(' ') >= 0 || (input.IndexOf('.') < 0 && input.IndexOf('/') < 0 && input.IndexOf('@') < 0)))
            {
                DarkDialog.Show(this, Lang.T("地址格式不正确，请输入完整的 git 仓库地址。"), Lang.T("安装插件"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (!isUrl)
            {
                // npm 包方式安装
                SetStatus(Lang.T("正在安装插件") + " " + input + " …", DshTheme.Info);
                SetBusy(true);
                var w = new Thread(delegate ()
                {
                    string r = RunCapture("cmd.exe", "/c npm install -g " + input, 300000);
                    AppendLog("[plugin] npm install -g " + input + (r == null ? " (超时/失败)" : " 完成"));
                    Ui(delegate
                    {
                        SetBusy(false);
                        if (r == null)
                            ShowError(Lang.T("插件安装失败"), Lang.T("npm 安装失败（网络或包名错误）。\n详见「日志」页。"));
                        else
                        {
                            SetStatus(Lang.T("插件") + " " + input + " " + Lang.T("已安装"), DshTheme.Success);
                            DarkDialog.Show(this, Lang.T("插件") + " " + input + " " + Lang.T("已安装。\n重启服务后生效。"), Lang.T("安装插件"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                    });
                });
                w.IsBackground = true;
                w.Start();
                return;
            }
            string url = input;
            string name = Path.GetFileName(url.TrimEnd('/'));
            if (name.EndsWith(".git")) name = name.Substring(0, name.Length - 4);
            string target = Path.Combine(cfg.PluginsRoot, name);
            if (Directory.Exists(target) || File.Exists(target))
            {
                DarkDialog.Show(this, "目标目录已存在：\n" + target, "安装插件", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            SetStatus("正在克隆插件 " + name + " …", DshTheme.Info);
            SetBusy(true);
            var worker = new Thread(delegate ()
            {
                string r = RunGit( "clone \"" + url + "\" \"" + target + "\"", 300000);
                AppendLog("[plugin] clone " + url + (r == null ? " (超时/失败)" : " 完成"));
                Ui(delegate
                {
                    SetBusy(false);
                    RenderPlugins();
                    if (r == null)
                        ShowError("插件安装失败", "克隆失败（网络或地址错误）。\n详见「日志」页。");
                    else
                    {
                        SetStatus("插件 " + name + " 已安装", DshTheme.Success);
                        DarkDialog.Show(this, "插件 " + name + " 已克隆到:\n" + target + "\n\n重启服务后生效。", "安装插件", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                });
            });
            worker.IsBackground = true;
            worker.Start();
        }

        // 插件启用/禁用: 目录重命名加 .disabled 后缀, 可逆且不影响数据
        void TogglePlugin(PluginItem p)
        {
            string target = p.Disabled
                ? p.Path.Substring(0, p.Path.Length - ".disabled".Length)
                : p.Path + ".disabled";
            try
            {
                Directory.Move(p.Path, target);
                SetStatus((p.Disabled ? Lang.T("已启用插件") : Lang.T("已禁用插件")) + " " + p.Name, DshTheme.Success);
                RenderPlugins();
            }
            catch (Exception ex)
            {
                ShowError(Lang.T("操作失败"), ex.Message);
            }
        }

        void UninstallPlugin(PluginItem p)
        {
            var r = DarkDialog.Show(this, "确定卸载插件「" + p.Name + "」？\n\n将删除目录:\n" + p.Path + "\n\n（此操作不可撤销）",
                "卸载插件", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (r != DialogResult.Yes) return;
            try
            {
                Directory.Delete(p.Path, true);
                SetStatus("已卸载插件 " + p.Name, DshTheme.TextDim);
                RenderPlugins();
            }
            catch (Exception ex)
            {
                ShowError("卸载失败", "目录可能被占用或权限不足：\n" + ex.Message);
            }
        }

        void MaintainPlugins()
        {
            var plugins = ScanPlugins();
            int gitCount = 0;
            foreach (var p in plugins) if (p.IsGit) gitCount++;
            var r = DarkDialog.Show(this, string.Format("一键维护将依次执行：\n\n1. 更新全部插件 (git pull, 共 {0} 个仓库)\n2. 重建插件链接依赖\n\n是否开始？", gitCount),
                "一键维护", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (r != DialogResult.Yes) return;

            SetStatus("正在更新所有插件…", DshTheme.Info);
            SetBusy(true);
            var worker = new Thread(delegate ()
            {
                int ok = 0, fail = 0;
                foreach (var p in plugins)
                {
                    if (!p.IsGit) continue;
                    string res = RunGit( string.Format("-C \"{0}\" pull", p.Path), 120000);
                    AppendLog("[plugin] git pull " + p.Name + (res == null ? " (超时/失败)" : " 完成"));
                    if (res == null) fail++; else ok++;
                }
                Ui(delegate
                {
                    SetBusy(false);
                    RepairPlugins();
                    DarkDialog.Show(this, string.Format("维护完成：更新成功 {0} 个，失败 {1} 个。\n\n已打开依赖修复脚本，完成后建议重启服务。", ok, fail),
                        "一键维护", MessageBoxButtons.OK, MessageBoxIcon.Information);
                });
            });
            worker.IsBackground = true;
            worker.Start();
        }

        // ============ 更新渲染 ============
        void RenderUpdateInfo()
        {
            upCurrent.Text = string.IsNullOrEmpty(currentUpdate.DshCurrent) ? (string.IsNullOrEmpty(currentEnv.DshVersion) ? "未知" : currentEnv.DshVersion) : currentUpdate.DshCurrent;
            upLatest.Text = string.IsNullOrEmpty(currentUpdate.DshLatest) ? "未检查" : currentUpdate.DshLatest;

            if (currentUpdate.HasUpdate)
            {
                upNote.Text = currentUpdate.Detail;
                upNote.ForeColor = DshTheme.Warn;
                upCurrent.ForeColor = DshTheme.Warn;
                upLatest.ForeColor = DshTheme.Success;
            }
            else if (!string.IsNullOrEmpty(currentUpdate.DshLatest))
            {
                upNote.Text = "已是最新版本，无需更新。";
                upNote.ForeColor = DshTheme.Success;
                upCurrent.ForeColor = DshTheme.Success;
                upLatest.ForeColor = DshTheme.Success;
            }
            else
            {
                upNote.Text = "点击「检查更新」获取最新版本信息。";
                upNote.ForeColor = DshTheme.TextDim;
                upCurrent.ForeColor = Color.White;
                upLatest.ForeColor = DshTheme.TextDim;
            }
            StyleEnabled(upApply, currentUpdate.HasUpdate, true);

            upPluginDetail.Text = string.IsNullOrEmpty(currentUpdate.PluginNames)
                ? "暂无需要更新的插件。\n\n提示：在「插件」页可对单个插件执行 git pull。"
                : "以下插件有可用更新（本地 HEAD → 最新）：\n\n" + BuildPluginUpdateDetail();
        }

        string BuildPluginUpdateDetail()
        {
            var lines = new List<string>();
            foreach (string raw in currentUpdate.PluginNames.Split(','))
            {
                string n = raw.Trim();
                if (n.Length == 0) continue;
                string l = pluginLocalHashes.ContainsKey(n) ? pluginLocalHashes[n] : "?";
                string r = pluginRemoteHashes.ContainsKey(n) ? pluginRemoteHashes[n] : "?";
                lines.Add("  · " + n + "  (本地 " + l + " → 最新 " + r + ")");
            }
            return string.Join("\n", lines.ToArray());
        }

        // ============ 日志 ============
        string CurrentLogPath()
        {
            string name = (logKind.SelectedIndex == 1) ? "dsh.log" : "launcher.log";
            return Path.Combine(cfg.LogDir, name);
        }

        void RefreshLogViews()
        {
            if (logText == null) return;
            if (!logAuto.Checked) return;   // 自动刷新复选框真实生效
            string path = CurrentLogPath();
            logText.Text = ReadTail(path, 500);
            logText.SelectionStart = logText.Text.Length;
            logText.ScrollToCaret();
        }

        void ClearLogNow()
        {
            string path = CurrentLogPath();
            var choice = DarkDialog.Show(this, "确定清空以下日志文件吗？\n\n" + path, "清空日志",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (choice != DialogResult.Yes) return;
            try { File.WriteAllText(path, ""); }
            catch (Exception ex) { ShowError("清空日志失败", ex.Message); }
            RefreshLogViews();
        }

        // ============ 启动流程 ============
        void RunStartupFlow()
        {
            UpdateSbRight();
            Program.DLog("startup", "begin; S=" + S + " client=" + ClientSize + " cfg=" + LauncherConfig.ConfigPath);
            SetBusy(true);
            SetStatus("正在检测环境…", DshTheme.Info);

            var worker = new Thread(delegate ()
            {
                ResolveProxy();   // 探测本机代理(Clash 等)并注入子进程环境, 让 curl/npm/git 走代理
                EnvInfo env = DetectEnvironment();
                Program.DLog("startup", "detect done; node=" + env.NodePath + " npm=" + env.NpmPath + " git=" + env.GitPath + " dsh=" + env.DshPath + " v" + env.DshVersion);
                currentEnv = env;
                Ui(delegate
                {
                    envDetected = true;
                    RenderEnvironment();
                    RenderOverview();
                });

                if (cfg.CheckUpdatesOnStart)
                {
                    Ui(delegate { SetStatus("正在检查更新…", DshTheme.Info); });
                    UpdateInfo info = CheckUpdates(env);
                    Program.DLog("startup", "update check done; dshUpd=" + info.DshUpdate + " pluginsUpd=" + info.PluginCount + " detail=" + info.Detail);
                    currentUpdate = info;
                    Ui(delegate
                    {
                        RenderUpdateInfo();
                        if (!string.IsNullOrEmpty(cfg.LauncherUpdateUrl)) CheckLauncherUpdateNow();
                        if (info.HasUpdate)
                        {
                            PromptUpdateOnStart(info);
                        }
                        else
                        {
                            ContinueAfterCheck();
                        }
                    });
                }
                else
                {
                    Ui(delegate { ContinueAfterCheck(); });
                }

                // 商城预热: 后台拉取插件列表并写入本地缓存, 打开商城秒出结果 (傻瓜式, 无需手动点获取)
                try { StoreForm.WarmUp(this); } catch { }
            });
            worker.IsBackground = true;
            worker.Start();
        }

        void PromptUpdateOnStart(UpdateInfo info)
        {
            var r = DarkDialog.Show(this,
                "发现可用更新：\n\n" + info.Detail + "\n\n是否立即更新？\n（更新完成后会自动启动服务）",
                "发现更新", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (r == DialogResult.Yes)
            {
                RunUpdates(info, true);
            }
            else
            {
                ContinueAfterCheck();
            }
        }

        void ContinueAfterCheck()
        {
            SetBusy(false);
            if (cfg.AutoStartService)
            {
                if (ServiceRunning())
                {
                    // 服务已在运行且无需更新 → 直接打开, 不做无意义的重启
                    SetStatus("服务已在运行", DshTheme.Success);
                    if (cfg.OpenBrowserOnStart) OpenBrowser();
                    RenderOverview();
                }
                else
                {
                    StartService();
                }
            }
            else
            {
                SetStatus("就绪（未自动启动）", DshTheme.TextDim);
                RenderOverview();
            }
        }

        void RedetectNow()
        {
            SetBusy(true);
            SetStatus("正在重新检测环境…", DshTheme.Info);
            var worker = new Thread(delegate ()
            {
                EnvInfo env = DetectEnvironment();
                currentEnv = env;
                Ui(delegate
                {
                    SetBusy(false);
                    envDetected = true;
                    RenderEnvironment();
                    RenderOverview();
                    SetStatus("环境检测完成", DshTheme.Success);
                });
            });
            worker.IsBackground = true;
            worker.Start();
        }

        // ============ 环境检测 ============
        // 进程 PATH 可能陈旧(启动器从资源管理器继承), 检测时合并注册表中的用户/系统 PATH
        static List<string> AllPathDirs()
        {
            var dirs = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Action<string> add = delegate(string path)
            {
                if (string.IsNullOrEmpty(path)) return;
                foreach (string seg in path.Split(';'))
                {
                    string t = seg.Trim().Trim('"');
                    if (t.Length > 0 && seen.Add(t)) dirs.Add(t);
                }
            };
            add(Environment.GetEnvironmentVariable("Path"));
            try { using (var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("Environment")) add(k.GetValue("Path") as string); } catch { }
            try { using (var k = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Environment")) add(k.GetValue("Path") as string); } catch { }
            return dirs;
        }

        static string FindTool(params string[] names)
        {
            foreach (string d in AllPathDirs())
                foreach (string n in names)
                {
                    try
                    {
                        string p = Path.Combine(d, n);
                        if (File.Exists(p)) return p;
                    }
                    catch { }
                }
            return "";
        }

        EnvInfo DetectEnvironment()
        {
            var env = new EnvInfo();
            try
            {
                // dsh: 手动配置的 DshCommand 若是文件路径则直接采用; 否则 where/扫描
                if (cfg.DshCommand.IndexOf('\\') >= 0 && File.Exists(cfg.DshCommand))
                    env.DshPath = cfg.DshCommand;
                if (string.IsNullOrEmpty(env.DshPath))
                {
                    string where = RunCapture("where", "dsh", 10000);
                    if (where != null)
                    {
                        string[] lines = where.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                        if (lines.Length > 0 && File.Exists(lines[0].Trim()))
                            env.DshPath = lines[0].Trim();
                    }
                }
                if (string.IsNullOrEmpty(env.DshPath))
                    env.DshPath = FindTool("dsh.cmd", "dsh.exe", "dsh");
                Program.DLog("detect", "dshPath=" + env.DshPath);

                string dshCmd = string.IsNullOrEmpty(env.DshPath) ? "dsh" : env.DshPath;
                string ver = RunCapture("cmd.exe", "/c \"" + dshCmd + "\" --version", 15000);
                if (ver != null)
                {
                    Match m = Regex.Match(ver, "(\\d+\\.\\d+\\.\\d+[^\\s]*)");
                    if (m.Success) env.DshVersion = m.Groups[1].Value;
                }

                // npm: 手动配置优先
                if (!string.IsNullOrEmpty(cfg.NpmPath) && File.Exists(cfg.NpmPath)) env.NpmPath = cfg.NpmPath;
                else env.NpmPath = FirstLine(RunCapture("where", "npm", 10000));
                if (string.IsNullOrEmpty(env.NpmPath)) env.NpmPath = FindTool("npm.cmd", "npm.exe");
                env.NpmVersion = FirstLine(RunCapture("cmd.exe", "/c npm --version", 15000));

                // git: 手动配置优先
                if (!string.IsNullOrEmpty(cfg.GitPath) && File.Exists(cfg.GitPath)) env.GitPath = cfg.GitPath;
                else env.GitPath = FindGit();
                env.GitVersion = FirstLine(RunCapture("cmd.exe", "/c git --version", 15000));

                // node: 手动配置优先
                if (!string.IsNullOrEmpty(cfg.NodePath) && File.Exists(cfg.NodePath)) env.NodePath = cfg.NodePath;
                else env.NodePath = FindNode();
                env.NodeVersion = FirstLine(RunCapture("cmd.exe", "/c node --version", 15000));

                env.DshHomeExists = Directory.Exists(cfg.DshHome);
                if (Directory.Exists(cfg.PluginsRoot))
                {
                    env.PluginDirs = Directory.GetDirectories(cfg.PluginsRoot).Length;
                    foreach (string d in Directory.GetDirectories(cfg.PluginsRoot))
                        if (Directory.Exists(Path.Combine(d, ".git"))) env.PluginGitRepos++;
                }
            }
            catch { }
            return env;
        }

        static string FirstLine(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            string[] lines = s.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            return lines.Length > 0 ? lines[0].Trim() : "";
        }

        static string FindNode()
        {
            string where = RunCapture("where", "node", 10000);
            if (where != null)
            {
                string[] lines = where.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string l in lines)
                {
                    string p = l.Trim();
                    if (File.Exists(p)) return p;
                    // where 可能给出无后缀的路径
                    if (!p.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && File.Exists(p + ".exe")) return p + ".exe";
                }
            }

            // 常见安装位置: nvm-windows / 官方安装包 / scoop / chocolatey
            var cands = new List<string>();
            string nvmSymlink = Environment.GetEnvironmentVariable("NVM_SYMLINK");
            if (!string.IsNullOrEmpty(nvmSymlink)) cands.Add(Path.Combine(nvmSymlink, "node.exe"));
            cands.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "nodejs", "node.exe"));
            cands.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "nodejs", "node.exe"));
            cands.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "node", "node.exe"));
            string nvmHome = Environment.GetEnvironmentVariable("NVM_HOME");
            if (string.IsNullOrEmpty(nvmHome))
                nvmHome = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "nvm");
            if (Directory.Exists(nvmHome))
            {
                var vers = new List<string>();
                try { foreach (string d in Directory.GetDirectories(nvmHome)) if (Path.GetFileName(d).StartsWith("v", StringComparison.OrdinalIgnoreCase)) vers.Add(d); } catch { }
                vers.Sort();
                vers.Reverse();   // 新版本优先
                foreach (string v in vers) cands.Add(Path.Combine(v, "node.exe"));
            }
            cands.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "scoop", "apps", "nodejs", "current", "node.exe"));
            cands.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "chocolatey", "bin", "node.exe"));
            foreach (string c in cands)
            {
                try { if (File.Exists(c)) return c; }
                catch { }
            }
            return FindTool("node.exe");
        }

        static string FindGit()
        {
            string where = RunCapture("where", "git", 10000);
            if (where != null)
            {
                string[] lines = where.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string l in lines)
                {
                    string p = l.Trim();
                    if (File.Exists(p)) return p;
                }
            }
            var cands = new string[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Git", "cmd", "git.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Git", "cmd", "git.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Git", "cmd", "git.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "chocolatey", "bin", "git.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "scoop", "apps", "git", "current", "cmd", "git.exe")
            };
            foreach (string c in cands)
            {
                try { if (File.Exists(c)) return c; }
                catch { }
            }
            return FindTool("git.exe");
        }

        // ============ 一键安装 ============
        void InstallDshNow()
        {
            if (installing) return;
            installing = true;
            envInstall.Enabled = false;
            ovPrimary.Enabled = false;
            SetBusy(true);
            SetStatus("正在检测环境…", DshTheme.Info);

            var worker = new Thread(delegate ()
            {
                string error = "";
                try
                {
                    string nodeHome = "";
                    string nodeExe = FindNode();

                    if (string.IsNullOrEmpty(nodeExe))
                    {
                        Ui(delegate { SetStatus("未检测到 Node.js，正在获取下载地址…", DshTheme.Warn); });
                        string nodeUrl = GetLatestLtsUrl();
                        if (nodeUrl == null) throw new Exception("无法获取 Node.js 下载地址（请检查网络连接后重试）");
                        string ver = Regex.Match(nodeUrl, "node-(v[^/]+)-win-(x64|x86)").Groups[1].Value;
                        string arch = Regex.Match(nodeUrl, "node-(v[^/]+)-win-(x64|x86)").Groups[2].Value;
                        string zip = Path.Combine(Path.GetTempPath(), "node-" + ver + "-win-" + arch + ".zip");
                        Ui(delegate { SetStatus("正在下载 Node.js " + ver + "（约 30MB）…", DshTheme.Info); });
                        if (!DownloadFile(nodeUrl, zip))
                        {
                            // 下载失败回退国内镜像
                            string mirrorZip = "https://npmmirror.com/mirrors/node/" + ver + "/node-" + ver + "-win-" + arch + ".zip";
                            AppendLog("[install] 官方源下载失败, 回退镜像 " + mirrorZip);
                            if (!DownloadFile(mirrorZip, zip))
                                throw new Exception("Node.js 下载失败（请检查网络后重试）");
                        }

                        nodeHome = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "node");
                        string tmp = Path.Combine(Path.GetTempPath(), "node-x-" + Guid.NewGuid().ToString("N"));
                        Directory.CreateDirectory(tmp);
                        if (Directory.Exists(nodeHome)) { try { Directory.Delete(nodeHome, true); } catch { } }
                        Ui(delegate { SetStatus("正在解压安装 Node.js…", DshTheme.Info); });
                        string tar = RunCapture("tar.exe", "-xf \"" + zip + "\" -C \"" + tmp + "\"", 120000);
                        if (tar == null) throw new Exception("解压 Node.js 失败（系统可能缺少 tar.exe）");
                        string[] dirs = Directory.GetDirectories(tmp);
                        if (dirs.Length == 0) throw new Exception("解压后未找到 Node.js 目录");
                        Directory.Move(dirs[0], nodeHome);
                        try { File.Delete(zip); } catch { }
                        try { Directory.Delete(tmp, true); } catch { }
                        AddUserPath(nodeHome);
                        nodeExe = Path.Combine(nodeHome, "node.exe");
                        AppendLog("[install] Node.js " + ver + " -> " + nodeHome);
                    }
                    else
                    {
                        nodeHome = Path.GetDirectoryName(nodeExe);
                    }

                    string npmCmd = Path.Combine(nodeHome, "npm.cmd");
                    if (!File.Exists(npmCmd)) npmCmd = "npm";
                    Program.DLog("install", "node=" + nodeExe + " npmCmd=" + npmCmd);
                    Ui(delegate { SetStatus("正在安装 dsh（npm install -g " + cfg.NpmPackage + "）…", DshTheme.Info); });
                    string r = NpmInstallGlobal(cfg.NpmPackage, 360000);
                    if (r == null) throw new Exception("npm 安装 dsh 失败（超时）。请检查网络后重试。");
                    AppendLog("[install] dsh installed via npm");

                    Ui(delegate { SetStatus("环境安装完成", DshTheme.Success); });
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                    AppendLog("[install] error: " + error);
                }

                Ui(delegate
                {
                    installing = false;
                    envInstall.Enabled = true;
                    SetBusy(false);
                    currentEnv = DetectEnvironment();
                    envDetected = true;
                    RenderEnvironment();
                    RenderOverview();

                    if (error.Length > 0)
                    {
                        ShowError("安装未完成", error + "\n\n详细信息见 launcher.log，可在「日志」页查看。");
                    }
                    else
                    {
                        DarkDialog.Show(this,
                            "环境已就绪：\n\n" +
                            "Node.js: " + (string.IsNullOrEmpty(currentEnv.NodePath) ? "✗ 仍缺失" : "✓ " + currentEnv.NodeVersion) + "\n" +
                            "dsh: " + (string.IsNullOrEmpty(currentEnv.DshPath) ? "✗ 仍缺失" : "✓ v" + currentEnv.DshVersion) + "\n\n" +
                            "接下来点击「一键启动」即可开始使用。",
                            "一键安装", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        SetStatus("环境就绪", DshTheme.Success);
                    }
                });
            });
            worker.IsBackground = true;
            worker.Start();
        }

        static string GetLatestLtsUrl()
        {
            // 官方源优先, 失败回退国内镜像; 自动识别 32/64 位; 内置下载器优先(不依赖 curl)
            string[] indexUrls = {
                "https://nodejs.org/dist/index.json",
                "https://npmmirror.com/mirrors/node/index.json"
            };
            string[] distBases = {
                "https://nodejs.org/dist/",
                "https://npmmirror.com/mirrors/node/"
            };
            string arch = Environment.Is64BitOperatingSystem ? "x64" : "x86";
            for (int i = 0; i < indexUrls.Length; i++)
            {
                string json = null;
                try
                {
                    using (var wc = new WebClient())
                    {
                        wc.Headers[HttpRequestHeader.UserAgent] = "dsh-launcher";
                        json = wc.DownloadString(indexUrls[i]);
                    }
                }
                catch { }
                if (json == null) json = RunCapture("curl.exe", "-s -m 30 " + indexUrls[i], 45000);
                if (json == null) continue;
                Match m = Regex.Match(json, "\"version\":\"(v\\d+\\.\\d+\\.\\d+)\",\"lts\":\"[A-Za-z]+\"");
                if (!m.Success) continue;
                string ver = m.Groups[1].Value;
                return distBases[i] + ver + "/node-" + ver + "-win-" + arch + ".zip";
            }
            return null;
        }

        bool DownloadFile(string url, string dest)
        {
            try
            {
                using (var wc = new WebClient())
                {
                    var wp = CurrentWebProxy();
                    if (wp != null) wc.Proxy = wp;
                    wc.DownloadFile(url, dest);
                }
                return true;
            }
            catch { return false; }
        }
        static void AddUserPath(string dir)
        {
            try
            {
                string userPath = Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.User) ?? "";
                if (userPath.IndexOf(dir, StringComparison.OrdinalIgnoreCase) < 0)
                    Environment.SetEnvironmentVariable("Path", userPath + ";" + dir, EnvironmentVariableTarget.User);
                string procPath = Environment.GetEnvironmentVariable("Path") ?? "";
                if (procPath.IndexOf(dir, StringComparison.OrdinalIgnoreCase) < 0)
                    Environment.SetEnvironmentVariable("Path", procPath + ";" + dir);
            }
            catch { }
        }

        // ============ 主按钮动作 ============
        void PrimaryAction()
        {
            bool running = ServiceRunning();
            bool nodeOk = !string.IsNullOrEmpty(currentEnv.NodePath);
            bool dshOk = !string.IsNullOrEmpty(currentEnv.DshPath);

            if (running) OpenBrowser();
            else if (!nodeOk || !dshOk) InstallDshNow();
            else StartService();
        }

        void SecondaryAction()
        {
            if (ServiceRunning()) StopService();
            else if (string.IsNullOrEmpty(currentEnv.NodePath) || string.IsNullOrEmpty(currentEnv.DshPath)) RedetectNow();
            else CheckUpdateNow();
        }

        void StartService()
        {
            if (starting || stopping) return;
            if (serverProc != null && !serverProc.HasExited) { OpenBrowser(); return; }

            if (IsPortOpen(cfg.Port))
            {
                if (cfg.RestartIfRunning)
                {
                    SetStatus("检测到旧服务，正在重启…", DshTheme.Warn);
                    int pid = FindPidByPort(cfg.Port);
                    if (pid > 0) KillProcessTree(pid);
                    for (int i = 0; i < 20 && IsPortOpen(cfg.Port); i++) Thread.Sleep(300);
                }
                else
                {
                    OpenBrowser();
                    SetStatus("服务已在运行", DshTheme.Success);
                    RenderOverview();
                    return;
                }
            }

            starting = true;
            SetBusy(true);
            SetStatus("正在启动服务…", DshTheme.Info);
            Program.DLog("svc", "start begin; cmd=" + cfg.DshCommand + " port=" + cfg.Port + " logdir=" + cfg.LogDir);
            ovPrimary.Enabled = false;
            ovSecondary.Enabled = false;
            ovTertiary.Enabled = false;

            var worker = new Thread(delegate ()
            {
                string launchError = "";
                string args = string.Format("/c {0} web --host 127.0.0.1 --port {1}", cfg.DshCommand, cfg.Port);
                try
                {
                    var psi = new ProcessStartInfo("cmd.exe", args)
                    {
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        WorkingDirectory = cfg.LogDir
                    };
                    var p = new Process { StartInfo = psi };
                    p.OutputDataReceived += (o, e) => AppendLog(e.Data);
                    p.ErrorDataReceived += (o, e) => AppendLog(e.Data);
                    p.Exited += (o, e) => AppendLog("[server exited]");
                    p.EnableRaisingEvents = true;
                    p.Start();
                    p.BeginOutputReadLine();
                    p.BeginErrorReadLine();
                    serverProc = p;
                }
                catch (Exception ex)
                {
                    launchError = ex.Message;
                    AppendLog("[launch error] " + ex.Message);
                }

                int waited = 0;
                while (waited < 120 && !IsPortOpen(cfg.Port) && (serverProc == null || !serverProc.HasExited))
                {
                    Thread.Sleep(500);
                    waited++;
                }

                Ui(delegate
                {
                    starting = false;
                    SetBusy(false);
                    bool ok = IsPortOpen(cfg.Port);
                    if (ok)
                    {
                        SetStatus("服务已就绪", DshTheme.Success);
                        if (cfg.OpenBrowserOnStart) OpenBrowser();
                    }
                    else
                    {
                        string hint = launchError.Length > 0 ? launchError : "服务未能就绪，可能是 dsh 未安装或配置错误。";
                        SetStatus("启动失败", DshTheme.Error);
                        ShowError("启动服务失败", hint + "\n\n请到「日志」页查看详细报错。\n若提示缺少依赖（如 schemastery），请点击「修复插件依赖」。");
                    }
                    RenderOverview();
                    RefreshLogViews();
                });
            });
            worker.IsBackground = true;
            worker.Start();
        }

        void StopService()
        {
            if (stopping) return;
            stopping = true;
            SetStatus("正在停止服务…", DshTheme.Info);
            SetBusy(true);

            var worker = new Thread(delegate ()
            {
                try
                {
                    if (serverProc != null && !serverProc.HasExited)
                        KillProcessTree(serverProc.Id);
                    else
                    {
                        int pid = FindPidByPort(cfg.Port);
                        if (pid > 0) KillProcessTree(pid);
                    }
                }
                catch { }
                Thread.Sleep(1200);

                Ui(delegate
                {
                    stopping = false;
                    SetBusy(false);
                    SetStatus("服务已停止", DshTheme.TextDim);
                    serverProc = null;
                    RenderOverview();
                });
            });
            worker.IsBackground = true;
            worker.Start();
        }

        void RestartService()
        {
            if (starting || stopping) return;
            stopping = true;
            SetStatus("正在重启服务…", DshTheme.Info);
            SetBusy(true);

            var worker = new Thread(delegate ()
            {
                try
                {
                    if (serverProc != null && !serverProc.HasExited) KillProcessTree(serverProc.Id);
                    int pid = FindPidByPort(cfg.Port);
                    if (pid > 0) KillProcessTree(pid);
                }
                catch { }
                for (int i = 0; i < 20 && IsPortOpen(cfg.Port); i++) Thread.Sleep(300);

                Ui(delegate
                {
                    stopping = false;
                    serverProc = null;
                    StartService();
                });
            });
            worker.IsBackground = true;
            worker.Start();
        }

        // ---------- npm 镜像源兜底 ----------
        const string MirrorRegistry = "https://registry.npmmirror.com";

        string NpmRegArg()
        {
            if (!string.IsNullOrEmpty(cfg.NpmRegistry)) return " --registry " + cfg.NpmRegistry;
            return "";
        }

        // npm view 查最新版本: 官方源失败自动回退国内镜像
        string QueryNpmLatest(string pkg)
        {
            string latest = RunCapture("cmd.exe", "/c npm view " + pkg + " version" + NpmRegArg(), 25000);
            if (latest == null && string.IsNullOrEmpty(cfg.NpmRegistry))
            {
                AppendLog("[npm] 官方源查询失败, 回退国内镜像 " + MirrorRegistry);
                latest = RunCapture("cmd.exe", "/c npm view " + pkg + " version --registry " + MirrorRegistry, 30000);
            }
            return latest;
        }

        // npm 全局安装: 失败自动回退国内镜像重试一次
        string NpmInstallGlobal(string pkg, int timeoutMs)
        {
            Program.DLog("npm", "install -g " + pkg + " reg=" + (string.IsNullOrEmpty(cfg.NpmRegistry) ? "(default)" : cfg.NpmRegistry));
            string r = RunCapture("cmd.exe", "/c npm install -g " + pkg + NpmRegArg(), timeoutMs);
            if (r == null && string.IsNullOrEmpty(cfg.NpmRegistry))
            {
                AppendLog("[npm] 安装失败, 回退国内镜像重试 " + MirrorRegistry);
                Program.DLog("npm", "fallback mirror " + MirrorRegistry);
                r = RunCapture("cmd.exe", "/c npm install -g " + pkg + " --registry " + MirrorRegistry, timeoutMs);
            }
            Program.DLog("npm", "result=" + (r == null ? "FAIL" : "OK"));
            return r;
        }

        // 后台静默检查: 更新环境/版本显示, 不弹任何对话框
        void AutoCheckNow()
        {
            if (checkingAuto || !cfg.CheckUpdatesOnStart) return;
            checkingAuto = true;
            var worker = new Thread(delegate ()
            {
                EnvInfo env = DetectEnvironment();
                UpdateInfo info = CheckUpdates(env);
                Ui(delegate
                {
                    currentEnv = env;
                    currentUpdate = info;
                    RenderEnvironment();
                    RenderOverview();
                    RenderUpdateInfo();
                    RenderPlugins();
                    // 注意: CheckLauncherUpdateNow 开头会直接写控件, 必须在 UI 线程调用
                    if (!string.IsNullOrEmpty(cfg.LauncherUpdateUrl)) CheckLauncherUpdateNow();
                    checkingAuto = false;
                });
            });
            worker.IsBackground = true;
            worker.Start();
        }

        // ============ 更新 ============
        // 启动器自更新检查: 从配置的 URL 拉取纯版本号并比较
        void CheckLauncherUpdateNow()
        {
            if (string.IsNullOrEmpty(cfg.LauncherUpdateUrl))
            {
                upLauncherNote.Text = Lang.T("未配置更新源，可在「设置」中填写");
                upLauncherNote.ForeColor = DshTheme.TextFaint;
                return;
            }
            upLauncherCheck.Enabled = false;
            upLauncherNote.Text = Lang.T("正在检查更新…");
            upLauncherNote.ForeColor = DshTheme.TextDim;
            var worker = new Thread(delegate ()
            {
                string latest = null;
                try
                {
                    // 多源链式获取: 配置源 → jsDelivr CDN(国内可达)
                    var urls = new List<string>();
                    if (!string.IsNullOrEmpty(cfg.LauncherUpdateUrl)) urls.Add(cfg.LauncherUpdateUrl);
                    urls.Add("https://cdn.jsdelivr.net/gh/loudMore/dsh-launcher@main/version.txt");
                    foreach (string u in urls)
                    {
                        Program.DLog("lupd", "try " + u);
                        string outp = RunCapture("curl.exe", "-s -L -m 25 \"" + u + "\"", 35000);
                        if (outp == null) { Program.DLog("lupd", "failed " + u); continue; }
                        string[] lines = outp.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                        if (lines.Length > 0)
                        {
                            Match m = Regex.Match(lines[0].Trim(), "(\\d+\\.\\d+\\.\\d+)");
                            if (m.Success) { latest = m.Groups[1].Value; break; }
                        }
                    }
                }
                catch { }
                Ui(delegate
                {
                    upLauncherCheck.Enabled = true;
                    if (latest == null)
                    {
                        upLauncherLatest.Text = Lang.T("最新版本") + "  " + Lang.T("获取失败");
                        upLauncherLatest.ForeColor = DshTheme.Error;
                        upLauncherNote.Text = Lang.T("无法获取更新信息（检查网络/更新源）");
                        upLauncherNote.ForeColor = DshTheme.Error;
                        StyleEnabled(upLauncherGo, false, true);
                    }
                    else
                    {
                        upLauncherLatest.Text = Lang.T("最新版本") + "  v" + latest;
                        int cmp = CompareVersions(LauncherVersion, latest);
                        if (cmp < 0)
                        {
                            upLauncherLatest.ForeColor = DshTheme.Success;
                            upLauncherNote.Text = Lang.T("发现新版本") + " v" + latest + "，点击「下载新版本」";
                            upLauncherNote.ForeColor = DshTheme.Warn;
                            StyleEnabled(upLauncherGo, true, true);
                        }
                        else
                        {
                            upLauncherLatest.ForeColor = DshTheme.Success;
                            upLauncherNote.Text = Lang.T("已是最新版本，无需更新。");
                            upLauncherNote.ForeColor = DshTheme.Success;
                            StyleEnabled(upLauncherGo, false, true);
                        }
                    }
                });
            });
            worker.IsBackground = true;
            worker.Start();
        }

        static int CompareVersions(string a, string b)
        {
            try
            {
                string[] pa = a.Split('.');
                string[] pb = b.Split('.');
                for (int i = 0; i < Math.Max(pa.Length, pb.Length); i++)
                {
                    int va = i < pa.Length ? int.Parse(pa[i]) : 0;
                    int vb = i < pb.Length ? int.Parse(pb[i]) : 0;
                    if (va != vb) return va < vb ? -1 : 1;
                }
                return 0;
            }
            catch { return 0; }
        }

        void CheckUpdateNow()
        {
            upCheck.Enabled = false;
            SetStatus("正在检查更新…", DshTheme.Info);
            SetBusy(true);
            if (!string.IsNullOrEmpty(cfg.LauncherUpdateUrl)) CheckLauncherUpdateNow();

            var worker = new Thread(delegate ()
            {
                EnvInfo env = DetectEnvironment();
                currentEnv = env;
                UpdateInfo info = CheckUpdates(env);
                currentUpdate = info;

                Ui(delegate
                {
                    upCheck.Enabled = true;
                    SetBusy(false);
                    envDetected = true;
                    RenderEnvironment();
                    RenderOverview();
                    RenderUpdateInfo();

                    if (info.HasUpdate)
                    {
                        var r = DarkDialog.Show(this, "发现可用更新：\n\n" + info.Detail + "\n\n是否立即更新？",
                            "发现更新", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                        if (r == DialogResult.Yes) RunUpdates(info, false);
                    }
                    else
                    {
                        SetStatus("当前已是最新版本", DshTheme.Success);
                    }
                });
            });
            worker.IsBackground = true;
            worker.Start();
        }

        void ApplyUpdateNow()
        {
            if (!currentUpdate.HasUpdate)
            {
                DarkDialog.Show(this, "当前没有可用更新，无需升级。", "更新", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            RunUpdates(currentUpdate, false);
        }

        void RunUpdates(UpdateInfo info, bool autoStartAfter)
        {
            if (updating) return;
            updating = true;
            upApply.Enabled = false;
            upCheck.Enabled = false;
            SetBusy(true);
            SetStatus("正在更新，请稍候（约 1-3 分钟）…", DshTheme.Info);

            var worker = new Thread(delegate ()
            {
                ExecuteUpdates(info);
                Ui(delegate
                {
                    updating = false;
                    upApply.Enabled = true;
                    upCheck.Enabled = true;
                    SetBusy(false);

                    currentEnv = DetectEnvironment();
                    currentUpdate = new UpdateInfo();
                    envDetected = true;
                    RenderEnvironment();
                    RenderOverview();
                    RenderUpdateInfo();

                    if (info.DshUpdate)
                    {
                        var r = DarkDialog.Show(this,
                            "更新完成。\n\n升级 dsh 后插件的链接依赖可能需要重建，是否立即运行「修复插件依赖」？",
                            "更新完成", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                        if (r == DialogResult.Yes) RepairPlugins();
                    }

                    if (autoStartAfter)
                    {
                        if (cfg.AutoStartService) StartService();
                    }
                    else
                    {
                        SetStatus("更新完成", DshTheme.Success);
                    }
                });
            });
            worker.IsBackground = true;
            worker.Start();
        }

        UpdateInfo CheckUpdates(EnvInfo env)
        {
            var info = new UpdateInfo();
            try
            {
                string pkg = FindDshPackageJson();
                if (pkg != null)
                {
                    Match m = Regex.Match(File.ReadAllText(pkg), "\"version\"\\s*:\\s*\"([^\"]+)\"");
                    if (m.Success) info.DshCurrent = m.Groups[1].Value;
                }
                if (string.IsNullOrEmpty(info.DshCurrent)) info.DshCurrent = env.DshVersion;

                string latest = QueryNpmLatest(cfg.NpmPackage);
                if (latest != null)
                {
                    latest = latest.Trim();
                    if (latest.Length > 0 && latest[0] == 'v') latest = latest.Substring(1);
                    info.DshLatest = latest;
                }
                info.DshUpdate = !string.IsNullOrEmpty(info.DshCurrent) && !string.IsNullOrEmpty(info.DshLatest)
                    && !info.DshCurrent.Equals(info.DshLatest, StringComparison.OrdinalIgnoreCase);

                var names = new List<string>();
                pluginLocalHashes.Clear();
                pluginRemoteHashes.Clear();
                if (Directory.Exists(cfg.PluginsRoot))
                {
                    foreach (string dir in Directory.GetDirectories(cfg.PluginsRoot))
                    {
                        if (!Directory.Exists(Path.Combine(dir, ".git"))) continue;
                        string remote = RunGit( string.Format("-C \"{0}\" ls-remote origin HEAD", dir), 20000);
                        string local = RunGit( string.Format("-C \"{0}\" rev-parse HEAD", dir), 10000);
                        if (remote != null && local != null)
                        {
                            string[] parts = remote.Split(new char[] { '\t', ' ', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                            string rh = parts.Length > 0 ? parts[0] : "";
                            string lh = local.Trim();
                            string name = Path.GetFileName(dir);
                            if (lh.Length >= 7) pluginLocalHashes[name] = lh.Substring(0, 7);
                            if (rh.Length >= 7) pluginRemoteHashes[name] = rh.Substring(0, 7);
                            if (rh.Length >= 7 && !rh.Equals(lh, StringComparison.OrdinalIgnoreCase))
                                names.Add(name);
                        }
                    }
                }
                info.PluginCount = names.Count;
                info.PluginNames = string.Join(", ", names.ToArray());
                info.HasUpdate = info.DshUpdate || info.PluginCount > 0;

                var parts2 = new List<string>();
                if (info.DshUpdate) parts2.Add(string.Format("DSH {0} → {1}", info.DshCurrent, info.DshLatest));
                if (info.PluginCount > 0) parts2.Add(string.Format("插件 {0} 个（{1}）", info.PluginCount, info.PluginNames));
                info.Detail = string.Join(" · ", parts2.ToArray());
            }
            catch { info.HasUpdate = false; }
            return info;
        }

        string FindDshPackageJson()
        {
            try
            {
                string npmRoot = RunCapture("cmd.exe", "/c npm root -g", 15000);
                if (npmRoot != null)
                {
                    string p = Path.Combine(npmRoot.Trim(), cfg.NpmPackage, "package.json");
                    if (File.Exists(p)) return p;
                }
            }
            catch { }
            return null;
        }

        void ExecuteUpdates(UpdateInfo info)
        {
            try
            {
                if (info.DshUpdate)
                {
                    AppendLog("[update] npm install -g " + cfg.NpmPackage + "@latest");
                    string r = NpmInstallGlobal(cfg.NpmPackage + "@latest", 300000);
                    AppendLog("[update] dsh " + (r == null ? "(超时/失败)" : "完成"));
                }
                if (Directory.Exists(cfg.PluginsRoot))
                {
                    foreach (string dir in Directory.GetDirectories(cfg.PluginsRoot))
                    {
                        if (!Directory.Exists(Path.Combine(dir, ".git"))) continue;
                        string name = Path.GetFileName(dir);
                        AppendLog("[update] git pull " + name);
                        string r = RunGit( string.Format("-C \"{0}\" pull", dir), 240000);
                        AppendLog("[update] plugin " + name + " " + (r == null ? "(超时/失败)" : "完成"));
                    }
                }
            }
            catch (Exception ex) { AppendLog("[update] error: " + ex.Message); }
        }

        // ============ 插件操作 ============
        void PullPlugin(PluginItem p)
        {
            if (!p.IsGit)
            {
                DarkDialog.Show(this, "该目录不是 git 仓库，无法更新。", "插件", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            SetStatus("正在更新插件 " + p.Name + " …", DshTheme.Info);
            SetBusy(true);

            var worker = new Thread(delegate ()
            {
                string r = RunGit( string.Format("-C \"{0}\" pull", p.Path), 120000);
                AppendLog("[plugin] git pull " + p.Name + (r == null ? " (超时/失败)" : " 完成"));
                Ui(delegate
                {
                    SetBusy(false);
                    if (r == null)
                        ShowError("插件更新失败", "插件 " + p.Name + " 更新失败（超时或网络错误）。\n详见「日志」页。");
                    else
                    {
                        SetStatus("插件 " + p.Name + " 已更新", DshTheme.Success);
                        DarkDialog.Show(this, "插件 " + p.Name + " 已更新。\n重启服务后生效。", "插件", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                });
            });
            worker.IsBackground = true;
            worker.Start();
        }

        void UpdateAllPlugins()
        {
            var plugins = ScanPlugins();
            int gitCount = 0;
            foreach (var p in plugins) if (p.IsGit) gitCount++;
            if (gitCount == 0)
            {
                DarkDialog.Show(this, "插件目录中没有 git 仓库，无需更新。", "插件", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            SetStatus("正在更新所有插件…", DshTheme.Info);
            SetBusy(true);
            var worker = new Thread(delegate ()
            {
                int ok = 0, fail = 0;
                foreach (var p in plugins)
                {
                    if (!p.IsGit) continue;
                    string r = RunGit( string.Format("-C \"{0}\" pull", p.Path), 120000);
                    AppendLog("[plugin] git pull " + p.Name + (r == null ? " (超时/失败)" : " 完成"));
                    if (r == null) fail++; else ok++;
                }
                Ui(delegate
                {
                    SetBusy(false);
                    SetStatus("插件更新完成", DshTheme.Success);
                    DarkDialog.Show(this, string.Format("插件更新完成：成功 {0} 个，失败 {1} 个。\n重启服务后生效。", ok, fail),
                        "插件", MessageBoxButtons.OK, MessageBoxIcon.Information);
                });
            });
            worker.IsBackground = true;
            worker.Start();
        }

        void RepairPlugins()
        {
            string fixBat = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fix_plugins.bat");
            if (!File.Exists(fixBat))
            {
                ShowError("缺少修复脚本", "未找到 fix_plugins.bat（" + fixBat + "）。");
                return;
            }
            try
            {
                Process.Start(new ProcessStartInfo(fixBat) { UseShellExecute = true });
                SetStatus("已打开插件依赖修复脚本", DshTheme.Info);
            }
            catch (Exception ex)
            {
                ShowError("无法运行修复脚本", ex.Message);
            }
        }

        // ============ 设置 ============
        void SaveSettings()
        {
            int p;
            if (!int.TryParse(setPort.Text.Trim(), out p) || p <= 0 || p >= 65536)
            {
                ShowError("端口无效", "端口必须是 1 - 65535 之间的整数。");
                return;
            }
            cfg.DshCommand = setCmd.Text.Trim();
            cfg.Port = p;
            cfg.DshHome = setHome.Text.Trim();
            cfg.PluginsRoot = setPlugins.Text.Trim();
            cfg.LogDir = setLog.Text.Trim();
            cfg.NpmPackage = setNpm.Text.Trim();
            cfg.LauncherUpdateUrl = setLupUrl.Text.Trim();
            cfg.Proxy = setProxy.Text.Trim();
            if (!string.IsNullOrEmpty(cfg.Proxy))
            {
                try
                {
                    Environment.SetEnvironmentVariable("HTTP_PROXY", cfg.Proxy);
                    Environment.SetEnvironmentVariable("HTTPS_PROXY", cfg.Proxy);
                }
                catch { }
            }
            string newLang = (setLang.SelectedIndex == 1) ? "zh" : (setLang.SelectedIndex == 2 ? "en" : "");
            bool langChanged = (Lang.Code != Lang.Resolve(newLang));
            cfg.Language = newLang;
            // 自动检查更新 / 自动启动服务 / 自动打开浏览器 / 自动重启旧服务 均为内置默认行为
            cfg.ApplyDefaults();
            if (!cfg.Save())
            {
                ShowError(Lang.T("操作失败"), "设置保存失败（配置文件可能被占用或无权限）:\n" + LauncherConfig.ConfigPath);
                return;
            }

            try { Environment.SetEnvironmentVariable("DSH_HOME", cfg.DshHome); } catch { }

            setPort.Text = cfg.Port.ToString();
            setHome.Text = cfg.DshHome;
            setPlugins.Text = cfg.PluginsRoot;
            setLog.Text = cfg.LogDir;

            SetStatus(Lang.T("设置已保存"), DshTheme.Success);
            UpdateSbRight();
            RenderOverview();

            if (langChanged)
            {
                Lang.Set(cfg.Language);
                DarkDialog.Show(this, "语言已切换，重启启动器后完全生效。\n\n（立即重启？请关闭后重新双击打开）", "设置", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                DarkDialog.Show(this, "设置已保存。\n\n端口/路径等改动在下次启动服务时生效。", "设置", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        void RunDetect()
        {
            try
            {
                string where = RunCapture("where", "dsh", 10000);
                if (where != null && where.Trim().Length > 0) setCmd.Text = "dsh";
                string userDsh = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dsh");
                if (Directory.Exists(userDsh)) setHome.Text = userDsh;
                string exePlugins = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugins");
                if (Directory.Exists(exePlugins)) setPlugins.Text = exePlugins;
                string exeLog = AppDomain.CurrentDomain.BaseDirectory;
                if (!string.IsNullOrEmpty(exeLog)) setLog.Text = exeLog;

                DarkDialog.Show(this, "自动检测完成：已根据系统环境填充可识别的路径。\n未识别的项请手动填写。", "自动检测",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                ShowError("检测失败", ex.Message);
            }
        }

        // 零基础用户: 一键创建桌面快捷方式
        void CreateDesktopShortcut()
        {
            try
            {
                string lnk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "DeepSeek Harness.lnk");
                Type t = Type.GetTypeFromProgID("WScript.Shell");
                object shell = Activator.CreateInstance(t);
                object shortcut = t.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { lnk });
                Type st = shortcut.GetType();
                st.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { Application.ExecutablePath });
                st.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, new object[] { AppDomain.CurrentDomain.BaseDirectory });
                st.InvokeMember("Description", BindingFlags.SetProperty, null, shortcut, new object[] { "DeepSeek Harness 启动器" });
                try { st.InvokeMember("IconLocation", BindingFlags.SetProperty, null, shortcut, new object[] { Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "deepseek.ico") + ",0" }); } catch { }
                st.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
                DarkDialog.Show(this, "已在桌面创建「DeepSeek Harness」快捷方式，双击即可启动。", "桌面快捷方式", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                ShowError(Lang.T("操作失败"), ex.Message);
            }
        }

        void OpenConfigFile()
        {
            try
            {
                if (!File.Exists(LauncherConfig.ConfigPath))
                {
                    cfg.Save();
                }
                Process.Start("notepad.exe", LauncherConfig.ConfigPath);
            }
            catch (Exception ex) { ShowError("无法打开配置文件", ex.Message); }
        }

        // ============ 工具 ============
        void ShowError(string title, string message)
        {
            DarkDialog.Show(this, message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        void OpenFolder(string path)
        {
            try
            {
                if (!Directory.Exists(path)) Directory.CreateDirectory(path);
                Process.Start("explorer.exe", "\"" + path + "\"");
            }
            catch (Exception ex) { ShowError("无法打开目录", ex.Message + "\n\n路径: " + path); }
        }

        void OpenBrowser()
        {
            if (!ServiceRunning())
            {
                var r = DarkDialog.Show(this, "服务尚未启动，是否立即启动？", "打开浏览器",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (r == DialogResult.Yes) StartService();
                return;
            }
            try { Process.Start(string.Format("http://127.0.0.1:{0}", cfg.Port)); }
            catch (Exception ex) { ShowError("无法打开浏览器", ex.Message); }
        }

        void KillProcessTree(int pid)
        {
            try
            {
                Process.Start(new ProcessStartInfo("taskkill", string.Format("/pid {0} /T /F", pid))
                { UseShellExecute = false, CreateNoWindow = true });
            }
            catch { }
        }

        static int FindPidByPort(int port)
        {
            try
            {
                var psi = new ProcessStartInfo("netstat", "-ano")
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };
                var p = Process.Start(psi);
                string output = p.StandardOutput.ReadToEnd();
                p.WaitForExit(5000);
                string marker = ":" + port + " ";
                string[] lines = output.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string line in lines)
                {
                    if (line.IndexOf(marker, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    if (line.IndexOf("LISTENING", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    string[] parts = line.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 5)
                    {
                        int pid;
                        if (int.TryParse(parts[parts.Length - 1], out pid) && pid > 0) return pid;
                    }
                }
            }
            catch { }
            return 0;
        }

        static bool IsPortOpen(int port)
        {
            try
            {
                using (var c = new TcpClient())
                {
                    var ar = c.BeginConnect("127.0.0.1", port, null, null);
                    bool ok = ar.AsyncWaitHandle.WaitOne(400);
                    if (!ok) return false;
                    c.EndConnect(ar);
                    return true;
                }
            }
            catch { return false; }
        }

        static string ReadTail(string path, int maxLines)
        {
            try
            {
                if (!File.Exists(path)) return "";
                string[] lines = File.ReadAllLines(path);
                int start = Math.Max(0, lines.Length - maxLines);
                var sb = new StringBuilder();
                for (int i = start; i < lines.Length; i++) sb.AppendLine(lines[i]);
                return sb.ToString();
            }
            catch { return ""; }
        }

        string LogFileOf()
        {
            return Path.Combine(cfg.LogDir, "launcher.log");
        }

        void AppendLog(string line)
        {
            if (string.IsNullOrEmpty(line)) return;
            try
            {
                Directory.CreateDirectory(cfg.LogDir);
                File.AppendAllText(LogFileOf(), DateTime.Now.ToString("HH:mm:ss") + "  " + line + "\r\n");
            }
            catch { }
        }

        static string RunCapture(string program, string args, int timeoutMs)
        {
            long t0 = Environment.TickCount;
            string r = Program.RunCaptureStatic(program, args, timeoutMs);
            if (Program.DebugMode)
                Program.DLog("run", program + " " + args + " -> " + (r == null ? "TIMEOUT/FAIL" : "ok(" + r.Length + "B)") + " in " + (Environment.TickCount - t0) + "ms");
            return r;
        }

        // git 调用: 使用检测到的完整 git.exe 路径, 并把其依赖目录(mingw64\bin、usr\bin)临时前置到 PATH。
        // git for Windows 在受限 PATH 下缺少 MSYS 工具(usr\bin)会访问违例(0xC0000005), 必须补全
        string RunGit(string args, int timeoutMs)
        {
            string git = !string.IsNullOrEmpty(currentEnv.GitPath) ? currentEnv.GitPath : "git";
            string dir = Path.GetDirectoryName(git);
            string prefix = "";
            if (!string.IsNullOrEmpty(dir))
            {
                prefix = dir + ";";
                try
                {
                    string root = Path.GetDirectoryName(dir);
                    if (root.EndsWith("mingw64", StringComparison.OrdinalIgnoreCase) || root.EndsWith("mingw32", StringComparison.OrdinalIgnoreCase))
                        root = Path.GetDirectoryName(root);
                    string usrBin = Path.Combine(root, "usr", "bin");
                    if (Directory.Exists(usrBin)) prefix += usrBin + ";";
                    string gitBin = Path.Combine(root, "bin");
                    if (Directory.Exists(gitBin) && !gitBin.Equals(dir, StringComparison.OrdinalIgnoreCase)) prefix += gitBin + ";";
                }
                catch { }
            }
            string oldPath = Environment.GetEnvironmentVariable("Path");
            try { Environment.SetEnvironmentVariable("Path", prefix + oldPath); } catch { }
            string r = RunCapture(git, args, timeoutMs);
            try { Environment.SetEnvironmentVariable("Path", oldPath); } catch { }
            return r;
        }

        // ---- 代理: 多级自动探测(手动配置 → 环境变量 → Windows 系统代理 → 常见端口扫描), 统一注入子进程 ----
        static readonly string[] ProxyPorts = { "7890", "7897", "7891", "7892", "7893", "7894", "7895", "7896", "10809", "10808", "1080", "8118", "2080", "8888", "1087" };
        string detectedProxy;
        bool proxyChecked;

        bool TestProxy(string p)
        {
            if (string.IsNullOrEmpty(p)) return false;
            if (p.IndexOf("://") < 0) p = "http://" + p;
            return RunCapture("curl.exe", "-x " + p + " -s -m 3 https://api.github.com/zen", 6000) != null;
        }

        public string ResolveProxy()
        {
            if (!string.IsNullOrEmpty(cfg.Proxy))
            {
                if (TestProxy(cfg.Proxy)) { ApplyProxy(cfg.Proxy); return cfg.Proxy; }
                // 配置的代理失效则继续往下探测
            }
            if (proxyChecked) return detectedProxy;
            proxyChecked = true;

            // 1) 环境变量 (终端里已 export 的代理)
            string env = Environment.GetEnvironmentVariable("HTTPS_PROXY");
            if (string.IsNullOrEmpty(env)) env = Environment.GetEnvironmentVariable("HTTP_PROXY");
            if (string.IsNullOrEmpty(env)) env = Environment.GetEnvironmentVariable("ALL_PROXY");
            if (!string.IsNullOrEmpty(env) && TestProxy(env)) detectedProxy = env;

            // 2) Windows 系统代理 (Clash 开"系统代理"时浏览器走的通道)
            if (detectedProxy == null)
            {
                try
                {
                    using (var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings"))
                    {
                        if (k != null && Convert.ToInt32(k.GetValue("ProxyEnable", 0)) == 1)
                        {
                            string ps = k.GetValue("ProxyServer") as string;
                            if (!string.IsNullOrEmpty(ps))
                            {
                                Match m = Regex.Match(ps, "(https?|socks)=([^;]+)");
                                string host = m.Success ? m.Groups[2].Value.Trim() : ps.Trim();
                                string scheme = m.Success ? (m.Groups[1].Value == "socks" ? "socks://" : "http://") : "http://";
                                if (host.IndexOf("://") < 0) host = scheme + host;
                                if (TestProxy(host)) detectedProxy = host;
                            }
                        }
                    }
                }
                catch { }
            }

            // 3) 常见本地代理端口扫描 (Clash/v2rayN/sing-box/privoxy 等)
            if (detectedProxy == null)
            {
                foreach (string port in ProxyPorts)
                {
                    string p = "http://127.0.0.1:" + port;
                    if (TestProxy(p)) { detectedProxy = p; break; }
                }
            }

            if (detectedProxy != null)
            {
                Program.DLog("proxy", "detected " + detectedProxy);
                ApplyProxy(detectedProxy);
                if (string.IsNullOrEmpty(cfg.Proxy))
                {
                    cfg.Proxy = detectedProxy;
                    try { cfg.Save(); } catch { }
                }
            }
            return detectedProxy;
        }

        void ApplyProxy(string p)
        {
            try
            {
                // curl/npm/git 子进程自动读取这两个环境变量走代理
                Environment.SetEnvironmentVariable("HTTP_PROXY", p);
                Environment.SetEnvironmentVariable("HTTPS_PROXY", p);
            }
            catch { }
        }

        public WebProxy CurrentWebProxy()
        {
            string p = ResolveProxy();
            return string.IsNullOrEmpty(p) ? null : new WebProxy(p);
        }

        // ============ 关闭 ============
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            if (!quitting)
            {
                // 关闭按钮 → 隐藏到托盘, 服务继续运行
                e.Cancel = true;
                HideToTray();
                return;
            }
            try { tray.Visible = false; } catch { }
        }
    }

    // ============ 轻量 HTTP 助手: 自动代理 + 智能编码 (UTF-8/GBK), 杜绝中文乱码 ============
    static class SmartHttp
    {
        public static byte[] Get(string url, string proxy, int timeoutMs)
        {
            try
            {
                var req = (HttpWebRequest)WebRequest.Create(url);
                req.UserAgent = "dsh-launcher";
                req.Accept = "*/*";
                req.Timeout = timeoutMs;
                req.ReadWriteTimeout = timeoutMs;
                if (!string.IsNullOrEmpty(proxy)) req.Proxy = new WebProxy(proxy);
                using (var resp = (HttpWebResponse)req.GetResponse())
                using (var s = resp.GetResponseStream())
                using (var ms = new MemoryStream())
                {
                    byte[] buf = new byte[8192];
                    int n;
                    while ((n = s.Read(buf, 0, buf.Length)) > 0) ms.Write(buf, 0, n);
                    return ms.ToArray();
                }
            }
            catch { return null; }
        }

        // 严格 UTF-8 校验 → UTF-8; 否则按 GBK 解码 (国内镜像/老仓库常为 GBK), 两者都失败回退 UTF-8
        public static string Decode(byte[] b)
        {
            if (b == null || b.Length == 0) return "";
            if (b.Length >= 3 && b[0] == 0xEF && b[1] == 0xBB && b[2] == 0xBF)
            {
                byte[] t = new byte[b.Length - 3];
                Array.Copy(b, 3, t, 0, t.Length);
                b = t;
            }
            if (IsUtf8(b)) return Encoding.UTF8.GetString(b);
            try { return Encoding.GetEncoding(936).GetString(b); } catch { }
            try { return Encoding.UTF8.GetString(b); } catch { }
            return "";
        }

        static bool IsUtf8(byte[] b)
        {
            int i = 0;
            while (i < b.Length)
            {
                byte c = b[i];
                if (c < 0x80) { i++; continue; }
                if (c >= 0xC2 && c <= 0xDF) { if (i + 1 >= b.Length || (b[i + 1] & 0xC0) != 0x80) return false; i += 2; continue; }
                if (c >= 0xE0 && c <= 0x0EF) { if (i + 2 >= b.Length || (b[i + 1] & 0xC0) != 0x80 || (b[i + 2] & 0xC0) != 0x80) return false; i += 3; continue; }
                if (c >= 0xF0 && c <= 0xF4) { if (i + 3 >= b.Length || (b[i + 1] & 0xC0) != 0x80 || (b[i + 2] & 0xC0) != 0x80 || (b[i + 3] & 0xC0) != 0x80) return false; i += 4; continue; }
                return false;
            }
            return true;
        }
    }

    // ============ 商城本地小缓存 (秒开列表, 后台静默刷新) ============
    static class StoreCache
    {
        const int ListTtlSec = 6 * 3600;      // 列表缓存 6 小时

        static string Dir()
        {
            string d = Path.Combine(Path.GetTempPath(), "dsh-launcher-cache");
            try { Directory.CreateDirectory(d); } catch { }
            return d;
        }

        public static void SaveList(List<StoreItem> items)
        {
            try
            {
                var sb = new StringBuilder();
                sb.Append("{\"t\":").Append(DateTime.UtcNow.Ticks).Append(",\"items\":[");
                for (int i = 0; i < items.Count; i++)
                {
                    var it = items[i];
                    if (i > 0) sb.Append(',');
                    sb.Append("{\"f\":\"").Append(J(it.FullName)).Append("\",\"n\":\"").Append(J(it.Name))
                      .Append("\",\"u\":\"").Append(J(it.Url)).Append("\",\"d\":\"").Append(J(it.Desc))
                      .Append("\",\"s\":").Append(it.Stars).Append(",\"l\":\"").Append(J(it.Lang))
                      .Append("\",\"b\":\"").Append(J(it.Branch)).Append("\",\"p\":\"").Append(J(it.Pushed)).Append("\"}");
                }
                sb.Append("]}");
                File.WriteAllText(Path.Combine(Dir(), "store.json"), sb.ToString(), Encoding.UTF8);
                Program.DLog("store", "cache saved " + items.Count + " items");
            }
            catch { }
        }

        // 返回缓存列表; ageSec = 缓存年龄(秒), 无缓存返回 null
        public static List<StoreItem> LoadList(out long ageSec)
        {
            ageSec = long.MaxValue;
            try
            {
                string f = Path.Combine(Dir(), "store.json");
                if (!File.Exists(f)) return null;
                string json = File.ReadAllText(f, Encoding.UTF8);
                Match tm = Regex.Match(json, "\"t\":(\\d+)");
                long ticks;
                if (tm.Success && long.TryParse(tm.Groups[1].Value, out ticks))
                    ageSec = Math.Max(0, (DateTime.UtcNow.Ticks - ticks) / TimeSpan.TicksPerSecond);
                var list = new List<StoreItem>();
                foreach (Match m in Regex.Matches(json, "\\{\"f\":\"(.*?)\",\"n\":\"(.*?)\",\"u\":\"(.*?)\",\"d\":\"(.*?)\",\"s\":(-?\\d+),\"l\":\"(.*?)\",\"b\":\"(.*?)\"(?:,\"p\":\"(.*?)\")?\\}"))
                {
                    var it = new StoreItem
                    {
                        FullName = UJ(m.Groups[1].Value),
                        Name = UJ(m.Groups[2].Value),
                        Url = UJ(m.Groups[3].Value),
                        Desc = UJ(m.Groups[4].Value),
                        Lang = UJ(m.Groups[6].Value),
                        Branch = UJ(m.Groups[7].Value),
                        Pushed = UJ(m.Groups[8].Value),
                        Stars = -1
                    };
                    int.TryParse(m.Groups[5].Value, out it.Stars);
                    list.Add(it);
                }
                return list.Count > 0 ? list : null;
            }
            catch { return null; }
        }

        public static int ListTtl() { return ListTtlSec; }

        // 缓存为机器可读格式: 引号替换为单引号, 换行折叠, 保证 Regex 可解析
        static string J(string s)
        {
            return (s ?? "").Replace('"', '\'').Replace("\r", " ").Replace("\n", " ").Replace("\\", "/");
        }

        static string UJ(string s) { return s ?? ""; }
    }

    // ============ 现代输入框 (圆角深色 + 占位提示 + 聚焦蓝色描边, 纯自绘零依赖) ============
    class ModernTextBox : Control
    {
        TextBox inner;
        bool focused;
        bool hover;
        int Pad { get { return (int)Math.Round(12 * DshTheme.S); } }
        int Rad { get { return (int)Math.Round(9 * DshTheme.S); } }

        public string Placeholder = "";

        public ModernTextBox()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.IBeam;
            inner = new TextBox
            {
                BorderStyle = BorderStyle.None,
                BackColor = DshTheme.BgInput,
                ForeColor = Color.White,
                Font = DshFonts.Body,
                Multiline = false
            };
            inner.GotFocus += delegate { focused = true; Invalidate(); };
            inner.LostFocus += delegate { focused = false; Invalidate(); };
            inner.TextChanged += delegate
            {
                Invalidate();
                if (TextChanged != null) TextChanged(this, EventArgs.Empty);
            };
            Controls.Add(inner);
            LayoutInner();
            MouseEnter += delegate { hover = true; Invalidate(); };
            MouseLeave += delegate { hover = false; Invalidate(); };
            Click += delegate { inner.Focus(); };
        }

        public override string Text
        {
            get { return inner.Text; }
            set { inner.Text = value; Invalidate(); }
        }

        public new event EventHandler TextChanged;

        void LayoutInner()
        {
            int ph = Font.Height + 10;
            inner.Location = new Point(Pad, Math.Max(0, (Height - ph) / 2));
            inner.Width = Math.Max(10, Width - Pad * 2);
            inner.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            LayoutInner();
        }

        protected override void OnPaintBackground(PaintEventArgs e) { }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var p = Program.RoundRectPath(rect, Rad))
            {
                using (var b = new SolidBrush(DshTheme.BgInput)) g.FillPath(b, p);
                Color bc = focused ? DshTheme.Blue : (hover ? DshTheme.Border : DshTheme.BorderSoft);
                using (var pen = new Pen(bc, focused ? 1.6f : 1f)) g.DrawPath(pen, p);
            }
            if (inner.Text.Length == 0 && !focused && Placeholder.Length > 0)
            {
                using (var b = new SolidBrush(DshTheme.TextFaint))
                using (var sf = new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter })
                    g.DrawString(Placeholder, DshFonts.Body, b, new RectangleF(Pad + 2, 0, Math.Max(1, Width - Pad * 2 - 4), Height), sf);
            }
        }
    }

    // ============ 现代下拉框 (圆角深色自绘 + 深色圆角弹出列表, 告别原生直角白底) ============
    class ModernDropdown : Control
    {
        string[] items = new string[0];
        bool hover;
        DropdownPopup popup;
        DateTime lastClose = DateTime.MinValue;
        int Pad { get { return (int)Math.Round(12 * DshTheme.S); } }
        int Rad { get { return (int)Math.Round(9 * DshTheme.S); } }

        public int SelectedIndex = -1;

        public string SelectedItem
        {
            get { return (SelectedIndex >= 0 && SelectedIndex < items.Length) ? items[SelectedIndex] : ""; }
        }

        public event EventHandler SelectionChanged;

        public ModernDropdown()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            MouseEnter += delegate { hover = true; Invalidate(); };
            MouseLeave += delegate { hover = false; Invalidate(); };
        }

        public string[] Items { get { return items; } }

        public void SetItems(string[] arr, int sel)
        {
            items = (arr == null) ? new string[0] : (string[])arr.Clone();
            SelectedIndex = (items.Length > 0) ? Math.Min(Math.Max(0, sel), items.Length - 1) : -1;
            Invalidate();
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            if ((DateTime.Now - lastClose).TotalMilliseconds < 350) return;   // 点击外部关闭时抑制立即重开
            if (popup != null && !popup.IsDisposed) { popup.Close(); return; }
            if (items.Length == 0) return;
            popup = new DropdownPopup(this);
            popup.FormClosed += delegate { popup = null; lastClose = DateTime.Now; };
            popup.Show();
        }

        internal void Pick(int i)
        {
            if (i >= 0 && i < items.Length)
            {
                SelectedIndex = i;
                Invalidate();
                if (SelectionChanged != null) SelectionChanged(this, EventArgs.Empty);
            }
            if (popup != null && !popup.IsDisposed) popup.Close();
        }

        protected override void OnPaintBackground(PaintEventArgs e) { }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var p = Program.RoundRectPath(rect, Rad))
            {
                using (var b = new SolidBrush(DshTheme.BgInput)) g.FillPath(b, p);
                bool open = (popup != null && !popup.IsDisposed);
                Color bc = open ? DshTheme.Blue : (hover ? DshTheme.Border : DshTheme.BorderSoft);
                using (var pen = new Pen(bc, open ? 1.6f : 1f)) g.DrawPath(pen, p);
            }
            using (var sf = new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter })
                g.DrawString(SelectedItem, DshFonts.Body, Brushes.White, new RectangleF(Pad, 0, Math.Max(1, Width - Pad - Px(22)), Height), sf);
            using (var b = new SolidBrush(DshTheme.TextDim))
            using (var sf2 = new StringFormat { LineAlignment = StringAlignment.Center, Alignment = StringAlignment.Far })
                g.DrawString("▾", DshFonts.Body, b, new RectangleF(Width - Px(24), 0, Px(18), Height), sf2);
        }

        int Px(int v) { return (int)Math.Round(v * DshTheme.S); }
    }

    // 下拉弹出列表 (无边框深色圆角小窗)
    class DropdownPopup : Form
    {
        ModernDropdown owner;

        public DropdownPopup(ModernDropdown ctl)
        {
            owner = ctl;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            BackColor = DshTheme.BgCard;
            KeyPreview = true;
            int rowH = (int)Math.Round(34 * DshTheme.S);
            int pad = (int)Math.Round(8 * DshTheme.S);
            int w = ctl.Width;
            foreach (string s in ctl.Items)
                w = Math.Max(w, TextRenderer.MeasureText(s, DshFonts.Body).Width + (int)Math.Round(44 * DshTheme.S));
            Size = new Size(w, ctl.Items.Length * rowH + pad * 2);

            var panel = new BufPanel { Dock = DockStyle.Fill, BackColor = DshTheme.BgCard, Padding = new Padding(pad) };
            var stack = new StackPanel { Dock = DockStyle.Fill };
            stack.Gap = 2;
            stack.PadLeft = 0;
            stack.BeginAdd();
            for (int i = 0; i < ctl.Items.Length; i++)
            {
                int idx = i;
                var lbl = new Label
                {
                    Text = ctl.Items[i],
                    AutoSize = false,
                    TextAlign = ContentAlignment.MiddleLeft,
                    ForeColor = (i == ctl.SelectedIndex) ? DshTheme.Blue : DshTheme.TextMain,
                    BackColor = Color.Transparent,
                    Font = DshFonts.Body,
                    Cursor = Cursors.Hand,
                    Padding = new Padding((int)Math.Round(10 * DshTheme.S), 0, 0, 0)
                };
                lbl.MouseEnter += delegate { lbl.BackColor = Color.FromArgb(255, 34, 42, 62); };
                lbl.MouseLeave += delegate { lbl.BackColor = Color.Transparent; };
                lbl.Click += delegate { owner.Pick(idx); };
                stack.Add(lbl, rowH);
            }
            panel.Controls.Add(stack);
            Controls.Add(panel);
            var oldRegion = Region;
            Region = new Region(Program.RoundRectPath(new Rectangle(0, 0, Width, Height), (int)Math.Round(12 * DshTheme.S)));
            if (oldRegion != null) oldRegion.Dispose();
            Deactivate += delegate { Close(); };
            KeyDown += delegate(object s, KeyEventArgs e) { if (e.KeyCode == Keys.Escape) Close(); };
            Shown += delegate { Position(); };
        }

        void Position()
        {
            var p = owner.PointToScreen(new Point(0, owner.Height + 3));
            var wa = Screen.FromPoint(p).WorkingArea;
            if (p.Y + Height > wa.Bottom) p = owner.PointToScreen(new Point(0, -Height - 3));
            if (p.X + Width > wa.Right) p.X = wa.Right - Width;
            if (p.X < wa.Left) p.X = wa.Left;
            Location = p;
        }
    }

    // ============ 插件商城独立窗口 ============
    class StoreForm : Form
    {
        LauncherForm owner;
        ModernTextBox search;
        ModernDropdown sort;
        ModernDropdown langFilter;
        ModernButton fetchBtn;
        Label note;
        StackPanel list;
        List<StoreItem> items = new List<StoreItem>();
        bool loading;
        bool dragging;
        Point dragStart;
        Timer retryTimer;
        bool pending;
        int autoTries;

        // 启动时预热: 后台拉好列表写入缓存, 打开商城秒出结果 (无需手动点获取)
        public static List<StoreItem> WarmList;
        static bool warmed;

        int P(int v) { return (int)Math.Round(v * DshTheme.S); }

        // 无边框窗口: 边缘出现缩放指针并支持拖拽调整大小 (WM_NCHITTEST)
        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg == 0x84)
            {
                int x = unchecked((short)((long)m.LParam & 0xFFFF));
                int y = unchecked((short)(((long)m.LParam >> 16) & 0xFFFF));
                Point pt = PointToClient(new Point(x, y));
                int b = P(6);
                bool top = pt.Y <= b, left = pt.X <= b, right = pt.X >= ClientSize.Width - b, bottom = pt.Y >= ClientSize.Height - b;
                if (top && left) m.Result = (IntPtr)13;
                else if (top && right) m.Result = (IntPtr)14;
                else if (bottom && left) m.Result = (IntPtr)16;
                else if (bottom && right) m.Result = (IntPtr)17;
                else if (top) m.Result = (IntPtr)12;
                else if (left) m.Result = (IntPtr)10;
                else if (right) m.Result = (IntPtr)11;
                else if (bottom) m.Result = (IntPtr)15;
            }
        }

        public StoreForm(LauncherForm ownerForm)
        {
            owner = ownerForm;
            Text = Lang.T("插件商城");
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            BackColor = DshTheme.Bg;
            int w = P(820), h = P(620);
            try
            {
                var wa = Screen.PrimaryScreen.WorkingArea;
                w = Math.Min(w, wa.Width - 40);
                h = Math.Min(h, wa.Height - 40);
            }
            catch { }
            Size = new Size(Math.Max(P(560), w), Math.Max(P(440), h));
            MinimumSize = new Size(P(560), P(440));
            KeyPreview = true;
            KeyDown += delegate(object s, KeyEventArgs e) { if (e.KeyCode == Keys.Escape) Close(); };
            BuildUi();
            retryTimer = new Timer();
            retryTimer.Interval = 15000;
            retryTimer.Tick += delegate { retryTimer.Stop(); FetchList(true); };
            Shown += delegate
            {
                long age;
                var cached = StoreCache.LoadList(out age);
                var seed = WarmList ?? cached;
                if (seed != null && seed.Count > 0)
                {
                    items = seed;
                    note.Text = string.Format(Lang.T("共 {0} 个插件 · 数据来自 GitHub"), seed.Count) + " · " + Lang.T("缓存");
                    note.ForeColor = DshTheme.TextFaint;
                    BuildLangFilter();
                    Render();
                }
                // 始终后台静默刷新: 有缓存先显示缓存, 拉取成功后自动替换 —— 傻瓜式, 无需手动点获取
                FetchList(true);
            };
        }

        public static void WarmUp(LauncherForm owner)
        {
            if (warmed) return;
            warmed = true;
            var t = new Thread(delegate()
            {
                try
                {
                    long age;
                    var cached = StoreCache.LoadList(out age);
                    if (cached != null && cached.Count > 0 && age <= StoreCache.ListTtl())
                    {
                        WarmList = cached;
                        Program.DLog("store", "warm: cache hit " + cached.Count);
                        return;
                    }
                    string proxy = null;
                    try { proxy = owner.ResolveProxy(); } catch { }
                    var got = Fetch(proxy);
                    if (got != null && got.Count > 0)
                    {
                        WarmList = got;
                        StoreCache.SaveList(got);
                        Program.DLog("store", "warm: fetched " + got.Count);
                    }
                    else
                    {
                        Program.DLog("store", "warm: fetch empty");
                    }
                }
                catch { }
            });
            t.IsBackground = true;
            t.Start();
        }

        // 拉取插件列表: GitHub API(星标/语言/日期) → jsDelivr 精选列表兜底; 全程自动代理 + 自动编码
        public static List<StoreItem> Fetch(string proxy)
        {
            var got = new List<StoreItem>();
            string json = null;
            try
            {
                byte[] b = SmartHttp.Get("https://api.github.com/search/repositories?q=topic%3Adsh-plugin&sort=stars&order=desc&per_page=100", proxy, 20000);
                if (b != null) json = SmartHttp.Decode(b);
            }
            catch { }
            if (!string.IsNullOrEmpty(json)) got = LauncherForm.ParseStoreJson(json);
            if (got.Count == 0)
            {
                string[] mdUrls = {
                    "https://cdn.jsdelivr.net/gh/bruc3van/awesome-dsh-plugin@main/README.md",
                    "https://cdn.jsdelivr.net/gh/0xsline/awesome-deepseek-harness@main/README.md"
                };
                foreach (string u in mdUrls)
                {
                    string md = null;
                    try { byte[] b = SmartHttp.Get(u, proxy, 15000); if (b != null) md = SmartHttp.Decode(b); } catch { }
                    got = LauncherForm.ParseMdList(md);
                    if (got.Count > 0) break;
                }
            }
            return got;
        }

        void BuildUi()
        {
            // ---- 标题栏 ----
            var titleBar = new Panel { Dock = DockStyle.Top, Height = P(42), BackColor = DshTheme.BgDeep };
            titleBar.Paint += delegate(object s, PaintEventArgs e)
            {
                using (var pen = new Pen(DshTheme.BorderSoft, 1f))
                    e.Graphics.DrawLine(pen, 0, titleBar.Height - 1, titleBar.Width, titleBar.Height - 1);
            };
            var icon = new Label { Text = "🛍", AutoSize = true, Left = P(16), Top = P(8), ForeColor = Color.White, BackColor = Color.Transparent, Font = DshFonts.BodyBold };
            var title = new Label { Text = Lang.T("插件商城"), AutoSize = true, Left = P(50), Top = P(10), ForeColor = Color.White, BackColor = Color.Transparent, Font = DshFonts.BodyBold };
            var close = new Label { Text = "✕", AutoSize = false, Width = P(40), Height = P(30), Top = P(6), TextAlign = ContentAlignment.MiddleCenter, ForeColor = DshTheme.TextDim, BackColor = Color.Transparent, Font = DshFonts.Body, Cursor = Cursors.Hand };
            close.MouseEnter += delegate { close.ForeColor = DshTheme.Error; };
            close.MouseLeave += delegate { close.ForeColor = DshTheme.TextDim; };
            close.Click += delegate { Close(); };
            titleBar.MouseDown += delegate(object s, MouseEventArgs e) { if (e.Button == MouseButtons.Left) { dragging = true; dragStart = e.Location; } };
            titleBar.MouseMove += delegate(object s, MouseEventArgs e) { if (dragging) Location = new Point(Left + e.X - dragStart.X, Top + e.Y - dragStart.Y); };
            titleBar.MouseUp += delegate(object s, MouseEventArgs e) { dragging = false; };
            titleBar.Controls.Add(icon);
            titleBar.Controls.Add(title);
            titleBar.Controls.Add(close);
            titleBar.Resize += delegate { close.Left = titleBar.Width - close.Width - P(8); };

            // ---- 工具栏: 搜索(多关键词模糊) + 排序 + 语言筛选 + 获取列表 + 打开网页 (状态文字自动换行) ----
            var toolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = P(78),
                BackColor = DshTheme.Bg,
                Padding = new Padding(P(14), P(8), P(14), 0),
                WrapContents = true
            };
            search = new ModernTextBox
            {
                Width = P(220),
                Height = P(34),
                Placeholder = Lang.T("搜索插件…"),
                Margin = new Padding(0, P(3), P(8), 0)
            };
            search.TextChanged += delegate { Render(); };
            sort = new ModernDropdown
            {
                Width = P(140),
                Height = P(34),
                Margin = new Padding(0, P(3), P(8), 0)
            };
            sort.SetItems(new string[] { "★ " + Lang.T("按星标排序"), Lang.T("按名称排序"), Lang.T("默认顺序") }, 0);
            sort.SelectionChanged += delegate { Render(); };
            langFilter = new ModernDropdown
            {
                Width = P(116),
                Height = P(34),
                Margin = new Padding(0, P(3), P(8), 0)
            };
            langFilter.SetItems(new string[] { Lang.T("全部语言") }, 0);
            langFilter.SelectionChanged += delegate { Render(); };
            fetchBtn = MakeBtn("↻ " + Lang.T("获取列表"), P(96), P(34), true);
            fetchBtn.Margin = new Padding(0, 0, P(8), 0);
            fetchBtn.Click += delegate { FetchList(false); };
            var webBtn = MakeBtn(Lang.T("打开网页"), P(96), P(34), false);
            webBtn.Margin = new Padding(0, 0, P(8), 0);
            webBtn.Click += delegate { try { Process.Start("https://github.com/topics/dsh-plugin"); } catch { } };
            note = new Label
            {
                Text = "",
                AutoSize = true,
                ForeColor = DshTheme.TextFaint,
                BackColor = Color.Transparent,
                Font = DshFonts.Caption,
                Margin = new Padding(P(2), P(9), 0, 0)
            };
            toolbar.Controls.AddRange(new Control[] { search, sort, langFilter, fetchBtn, webBtn, note });

            // ---- 列表卡片 ----
            var card = new RoundPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(P(10), P(6), P(10), P(10)),
                Margin = new Padding(P(14), 0, P(14), P(14))
            };
            list = new StackPanel { Dock = DockStyle.Fill };
            list.Gap = P(8);
            list.PadLeft = P(2);
            card.Controls.Add(list);

            Controls.Add(card);
            Controls.Add(toolbar);
            Controls.Add(titleBar);
        }

        ModernButton MakeBtn(string text, int w, int h, bool primary)
        {
            return new ModernButton
            {
                Text = text,
                Width = w,
                Height = h,
                Primary = primary,
                ForeColor = primary ? Color.White : DshTheme.TextMain,
                Font = DshFonts.Body
            };
        }

        List<StoreItem> Sorted()
        {
            var l = new List<StoreItem>(items);
            int mode = (sort == null ? 0 : sort.SelectedIndex);
            if (mode == 0)
                l.Sort(delegate(StoreItem a, StoreItem b) { return b.Stars.CompareTo(a.Stars); });
            else if (mode == 1)
                l.Sort(delegate(StoreItem a, StoreItem b) { return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase); });
            return l;
        }

        // 语言筛选下拉: 根据当前列表动态生成 (全部语言 + 出现过的语言)
        void BuildLangFilter()
        {
            if (langFilter == null) return;
            var set = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var it in items)
                if (!string.IsNullOrEmpty(it.Lang)) set.Add(it.Lang);
            string prev = langFilter.SelectedItem;
            var arr = new List<string>();
            arr.Add(Lang.T("全部语言"));
            foreach (string l in set) arr.Add(l);
            int sel = 0;
            for (int i = 0; i < arr.Count; i++)
                if (string.Equals(arr[i], prev, StringComparison.OrdinalIgnoreCase)) { sel = i; break; }
            langFilter.SetItems(arr.ToArray(), sel);
        }

        void Render()
        {
            if (list == null) return;
            list.ClearAll();
            list.BeginAdd();
            // 多关键词模糊匹配: 空格分隔, 每个词都必须在 名称/简介/仓库名 中出现 (AND)
            string raw = (search == null ? "" : search.Text.Trim().ToLowerInvariant());
            string[] terms = raw.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string langSel = (langFilter == null || langFilter.SelectedIndex <= 0) ? "" : langFilter.SelectedItem;
            int shown = 0;
            foreach (var it in Sorted())
            {
                if (!string.IsNullOrEmpty(langSel) && !string.Equals(it.Lang, langSel, StringComparison.OrdinalIgnoreCase))
                    continue;
                bool ok = true;
                for (int t = 0; t < terms.Length; t++)
                {
                    string term = terms[t];
                    if (term.Length == 0) continue;
                    if (it.Name.ToLowerInvariant().IndexOf(term) < 0
                        && it.Desc.ToLowerInvariant().IndexOf(term) < 0
                        && it.FullName.ToLowerInvariant().IndexOf(term) < 0
                        && it.Lang.ToLowerInvariant().IndexOf(term) < 0)
                    { ok = false; break; }
                }
                if (!ok) continue;
                AddRow(it);
                shown++;
            }
            if (items.Count == 0)
            {
                var empty = new Label
                {
                    Text = Lang.T("正在刷新…"),
                    AutoSize = false,
                    ForeColor = DshTheme.TextDim,
                    BackColor = Color.Transparent,
                    Font = DshFonts.Body
                };
                list.Add(empty, P(60));
            }
            else if (shown == 0)
            {
                var empty = new Label
                {
                    Text = Lang.T("没有匹配的插件，换个关键词试试"),
                    AutoSize = false,
                    ForeColor = DshTheme.TextDim,
                    BackColor = Color.Transparent,
                    Font = DshFonts.Body
                };
                list.Add(empty, P(60));
            }
        }

        void AddRow(StoreItem it)
        {
            var row = new RoundPanel { BorderColor = DshTheme.BorderSoft };
            var nameLbl = new Label
            {
                Text = it.Name,
                AutoSize = false,
                AutoEllipsis = true,
                Location = new Point(P(14), P(8)),
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Font = DshFonts.CardTitle
            };
            nameLbl.Width = P(160);
            // 属性行: ★ 星标数 · 语言 · 最近更新日期
            var meta = new StringBuilder();
            if (it.Stars >= 0) meta.Append("★ ").Append(it.Stars);
            if (!string.IsNullOrEmpty(it.Lang)) { if (meta.Length > 0) meta.Append(" · "); meta.Append(it.Lang); }
            if (!string.IsNullOrEmpty(it.Pushed)) { if (meta.Length > 0) meta.Append(" · "); meta.Append(it.Pushed); }
            var starLbl = new Label
            {
                Text = meta.Length > 0 ? meta.ToString() : "GitHub",
                AutoSize = true,
                Location = new Point(P(182), P(12)),
                ForeColor = DshTheme.Warn,
                BackColor = Color.Transparent,
                Font = DshFonts.Body
            };
            var descLbl = new Label
            {
                Text = string.IsNullOrEmpty(it.Desc) ? it.FullName : it.Desc,
                AutoSize = false,
                AutoEllipsis = true,
                Location = new Point(P(14), P(34)),
                ForeColor = DshTheme.TextDim,
                BackColor = Color.Transparent,
                Font = DshFonts.MonoSmall
            };
            // 行内两个统一宽度按钮: 浏览(跳转仓库页) / 安装
            var webBtn = MakeBtn("↗ " + Lang.T("浏览"), P(84), P(28), false);
            var installBtn = MakeBtn("↓ " + Lang.T("安装"), P(84), P(28), true);
            webBtn.Click += delegate { try { Process.Start(it.Url); } catch { } };
            installBtn.Click += delegate { owner.InstallFromStore(it); };
            row.Controls.Add(nameLbl);
            row.Controls.Add(starLbl);
            row.Controls.Add(descLbl);
            row.Controls.Add(webBtn);
            row.Controls.Add(installBtn);
            row.Resize += delegate
            {
                int x = row.Width - P(12);
                installBtn.Location = new Point(x - installBtn.Width, P(16)); x -= installBtn.Width + P(8);
                webBtn.Location = new Point(x - webBtn.Width, P(16)); x -= webBtn.Width + P(8);
                descLbl.Width = Math.Max(10, x - P(14));
            };
            list.Add(row, P(62));
        }

        void FetchList(bool silent)
        {
            if (loading) { pending = true; return; }   // 手动点击不丢失: 当前完成后自动再拉一次
            loading = true;
            if (!silent)
            {
                autoTries = 0;
                retryTimer.Stop();
                note.Text = Lang.T("正在获取插件列表…");
                note.ForeColor = DshTheme.TextDim;
                fetchBtn.Enabled = false;
            }
            string proxy = null;
            try { proxy = owner.ResolveProxy(); } catch { }   // 全自动: 手动配置 → 环境变量 → 系统代理 → 端口扫描
            var worker = new Thread(delegate()
            {
                var got = Fetch(proxy);
                Ui(delegate
                {
                    loading = false;
                    fetchBtn.Enabled = true;
                    if (got.Count > 0)
                    {
                        // 数据质量优先: 当前是 GitHub API 数据(带星标/日期), 新结果是精选列表兜底 → 不降级替换
                        bool gotIsMd = true;
                        foreach (var x in got) if (x.Stars >= 0) { gotIsMd = false; break; }
                        bool curIsApi = false;
                        foreach (var x in items) if (x.Stars >= 0) { curIsApi = true; break; }
                        if (!(gotIsMd && curIsApi))
                        {
                            items = got;
                            StoreCache.SaveList(got);
                            WarmList = got;
                        }
                        retryTimer.Stop();
                        autoTries = 0;
                        note.Text = string.Format(Lang.T("共 {0} 个插件 · 数据来自 GitHub"), items.Count);
                        note.ForeColor = DshTheme.TextFaint;
                        BuildLangFilter();
                    }
                    else if (items.Count > 0)
                    {
                        // 已有数据(缓存) 刷新失败: 保留现有列表, 稍后自动重试
                        if (autoTries < 3) { autoTries++; retryTimer.Start(); }
                        note.Text = string.Format(Lang.T("共 {0} 个插件 · 数据来自 GitHub"), items.Count) + " · " + Lang.T("缓存");
                        note.ForeColor = DshTheme.Warn;
                    }
                    else
                    {
                        // 空列表且拉取失败 → 静默自动重试, 网络/代理就绪后自动填上
                        if (autoTries < 3) { autoTries++; retryTimer.Start(); }
                        note.Text = Lang.T("正在刷新…");
                        note.ForeColor = DshTheme.TextDim;
                    }
                    Render();
                    if (pending) { pending = false; FetchList(true); }
                });
            });
            worker.IsBackground = true;
            worker.Start();
        }

        void Ui(Action a)
        {
            try { if (IsHandleCreated && !IsDisposed) BeginInvoke((MethodInvoker)delegate { a(); }); }
            catch { }
        }
    }

}
