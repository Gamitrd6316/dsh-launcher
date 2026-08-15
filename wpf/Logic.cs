// ============================================================
//  DeepSeek Harness 启动器 - WPF 重构版 · 逻辑层
//  从 WinForms 版移植: 命令助手 / 配置 / 环境检测 / 代理 / 服务 / 插件 / 商城 / 更新
//  全部 UI 无关; 通过 OnStatus / OnLog 回调向界面汇报
// ============================================================
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;

namespace DeepSeekHarness
{
    // ---------- 多语言 (zh/en) ----------
    // ---------- 多语言字典 (中/英/日/韩 全覆盖) ----------
    static class Lang
    {
        public static string Code = "zh";

        static Dictionary<string, string> en = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "概览", "Overview" }, { "环境", "Environment" }, { "插件", "Plugins" },
            { "更新", "Updates" }, { "日志", "Logs" }, { "设置", "Settings" },
            { "插件管理", "Plugin Manager" }, { "更新与升级", "Updates" }, { "日志查看", "Logs" },
            { "DSH 启动器 · WPF", "DSH Launcher · WPF" }, { "WPF 重构版", "WPF Edition" },
            { "准备就绪", "Ready" }, { "检测中…", "Detecting…" }, { "正在检测环境与服务状态…", "Detecting environment & service…" },
            { "一键启动", "Start" }, { "停止服务", "Stop" }, { "一键安装", "Install" },
            { "服务运行中", "Service Running" }, { "服务未启动", "Service Stopped" },
            { "环境已就绪，点击「一键启动」开始使用", "Ready — click Start to begin" },
            { "未检测到 Node.js", "Node.js not found" }, { "未检测到 dsh", "dsh not found" },
            { "首次使用请点击「一键安装」", "First run? Click Install" },
            { "运行环境", "Environment" }, { "未检测到（可点击上方「一键安装」）", "Not found (click Install above)" },
            { "数据目录", "Data dir" }, { "(不存在)", "(missing)" },
            { "重新检测", "Re-detect" }, { "一键安装 / 修复环境", "Install / Repair" },
            { "刷新列表", "Refresh" }, { "安装插件", "Install Plugin" }, { "插件商城", "Plugin Store" },
            { "全部更新", "Update All" }, { "一键维护", "Maintain" }, { "打开插件目录", "Open Plugins Dir" },
            { "打开目录", "Open Folder" }, { "卸载", "Remove" }, { "禁用", "Disable" }, { "启用", "Enable" },
            { "已禁用", "Disabled" }, { "普通目录", "Plain Dir" }, { "检查更新", "Check Updates" },
            { "前往 GitHub", "Open GitHub" }, { "立即升级 dsh", "Upgrade dsh" }, { "全部更新插件", "Update All Plugins" },
            { "当前", "Current" }, { "最新", "Latest" }, { "未检查", "Not checked" },
            { "已是最新版本", "Up to date" }, { "发现新版本，可前往 GitHub 下载", "New version available on GitHub" },
            { "发现新版本！", "New version!" }, { "插件更新", "Plugin updates" },
            { "自动刷新", "Auto refresh" }, { "打开日志目录", "Open Log Folder" },
            { "保存设置", "Save Settings" }, { "自动检测回填", "Auto-detect" }, { "打开配置文件", "Open Config" },
            { "服务端口", "Port" }, { "界面语言", "Language" }, { "npm 包名", "npm Package" },
            { "启动器更新源", "Launcher Update URL" }, { "代理地址", "Proxy" },
            { "获取列表", "Fetch List" }, { "打开网页", "Open Web" }, { "搜索插件…", "Search plugins…" },
            { "按星标排序", "Sort by stars" }, { "按名称排序", "Sort by name" }, { "默认顺序", "Default" },
            { "全部语言", "All languages" }, { "浏览", "Browse" }, { "安装", "Install" }, { "已安装", "Installed" },
            { "加载更多", "Load more" }, { "没有匹配的插件，换个关键词试试", "No matching plugins" },
            { "正在刷新…", "Refreshing…" }, { "正在获取插件列表…", "Fetching plugin list…" },
            { "共 {0} 个插件 · 数据来自 GitHub", "{0} plugins from GitHub" }, { " · 缓存", " · cache" },
            { "GitHub 项目主页", "GitHub Project" },
            { "启动器", "Launcher" }, { "端口 {0} · 启动器 v1.0.0 (WPF)", "Port {0} · Launcher v1.0.0 (WPF)" },
            { "共 {0} 个目录 · {1} 个 git 仓库", "{0} dirs · {1} git repos" }, { "目录", "Folder" },
            { "打开浏览器", "Open Browser" }, { "最近日志", "Recent Log" }, { "暂无日志", "No logs yet" },
            { "未检测到", "Not found" }, { "代理", "Proxy" }, { "直连", "Direct" }, { "npm 镜像", "npm Mirror" },
            { "重启服务", "Restart" }, { "滚轮滚动 · 完整日志在「日志」页", "Scroll · full log in Logs" },
            { "桌面快捷方式", "Desktop Shortcut" }, { "检测代理", "Detect Proxy" }, { "选择文件", "Browse" },
            { "服务已在运行", "Service running" }, { "界面主题", "Theme" }, { "深色模式 (Dark)", "Dark Mode" }, { "浅色模式 (Light)", "Light Mode" },
            { "清空显示", "Clear Log" }, { "复制日志", "Copy Log" }, { "搜索过滤…", "Filter logs…" }, { "已复制到剪贴板", "Copied to clipboard" }
        };

        static Dictionary<string, string> ja = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "概览", "概要" }, { "环境", "環境" }, { "插件", "プラグイン" },
            { "更新", "更新" }, { "日志", "ログ" }, { "设置", "設定" },
            { "一键启动", "起動" }, { "停止服务", "停止" }, { "一键安装", "インストール" },
            { "服务运行中", "サービス稼働中" }, { "服务未启动", "サービス停止中" },
            { "保存设置", "保存" }, { "界面语言", "表示言語" }, { "界面主题", "テーマ" },
            { "深色模式 (Dark)", "ダークモード" }, { "浅色模式 (Light)", "ライトモード" }
        };

        public static void Set(string code)
        {
            if (code == "en" || code == "ja") Code = code;
            else Code = "zh";
        }

        public static string T(string zh)
        {
            if (Code == "en")
            {
                string v;
                return en.TryGetValue(zh, out v) ? v : zh;
            }
            if (Code == "ja")
            {
                string v;
                return ja.TryGetValue(zh, out v) ? v : (en.TryGetValue(zh, out v) ? v : zh);
            }
            return zh;
        }
    }

    // ---------- 调试日志 ----------
    static class Proc
    {
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

        public static string ReopenFlagPath()
        {
            return Path.Combine(Path.GetTempPath(), "dsh-launcher-reopen-wpf.flag");
        }
    }

    // ---------- 配置 (与旧版 launcher.json 键名完全兼容) ----------
    class LauncherConfig
    {
        public int Port = 8099;
        public string DshCommand = "dsh";
        public string DshHome = "";
        public string PluginsRoot = "";
        public string LogDir = "";
        public bool CheckUpdatesOnStart = true;
        public bool AutoStartService = true;
        public bool RestartIfRunning = true;
        public bool OpenBrowserOnStart = true;
        public string NpmPackage = "@deepseek-ai/dsh";
        public string Language = "";
        public string Theme = "dark";
        public string LauncherUpdateUrl = "https://raw.githubusercontent.com/loudMore/dsh-launcher/main/version.txt";
        public string NpmRegistry = "";
        public string NodePath = "";
        public string NpmPath = "";
        public string GitPath = "";
        public string Proxy = "";

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
                    try
                    {
                        DshHome = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dsh");
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
                    var ser = new JavaScriptSerializer();
                    var d = ser.DeserializeObject(File.ReadAllText(ConfigPath)) as Dictionary<string, object>;
                    if (d != null)
                    {
                        cfg.Port = IntOf(d, "port", cfg.Port);
                        cfg.DshCommand = StrOf(d, "dshCommand", cfg.DshCommand);
                        cfg.DshHome = StrOf(d, "dshHome", cfg.DshHome);
                        cfg.PluginsRoot = StrOf(d, "pluginsRoot", cfg.PluginsRoot);
                        cfg.LogDir = StrOf(d, "logDir", cfg.LogDir);
                        cfg.CheckUpdatesOnStart = BoolOf(d, "checkUpdatesOnStart", cfg.CheckUpdatesOnStart);
                        cfg.AutoStartService = BoolOf(d, "autoStartService", cfg.AutoStartService);
                        cfg.RestartIfRunning = BoolOf(d, "restartIfRunning", cfg.RestartIfRunning);
                        cfg.OpenBrowserOnStart = BoolOf(d, "openBrowserOnStart", cfg.OpenBrowserOnStart);
                        cfg.NpmPackage = StrOf(d, "npmPackage", cfg.NpmPackage);
                        cfg.Language = StrOf(d, "language", cfg.Language);
                        cfg.Theme = StrOf(d, "theme", cfg.Theme);
                        cfg.LauncherUpdateUrl = StrOf(d, "launcherUpdateUrl", cfg.LauncherUpdateUrl);
                        cfg.NpmRegistry = StrOf(d, "npmRegistry", cfg.NpmRegistry);
                        cfg.NodePath = StrOf(d, "nodePath", cfg.NodePath);
                        cfg.NpmPath = StrOf(d, "npmPath", cfg.NpmPath);
                        cfg.GitPath = StrOf(d, "gitPath", cfg.GitPath);
                        cfg.Proxy = StrOf(d, "proxy", cfg.Proxy);
                    }
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
                var ser = new JavaScriptSerializer();
                string json = ser.Serialize(new
                {
                    port = Port,
                    dshCommand = DshCommand,
                    dshHome = DshHome,
                    pluginsRoot = PluginsRoot,
                    logDir = LogDir,
                    checkUpdatesOnStart = CheckUpdatesOnStart,
                    autoStartService = AutoStartService,
                    restartIfRunning = RestartIfRunning,
                    openBrowserOnStart = OpenBrowserOnStart,
                    npmPackage = NpmPackage,
                    language = Language,
                    theme = Theme,
                    launcherUpdateUrl = LauncherUpdateUrl,
                    npmRegistry = NpmRegistry,
                    nodePath = NodePath,
                    npmPath = NpmPath,
                    gitPath = GitPath,
                    proxy = Proxy
                });
                File.WriteAllText(ConfigPath, json);
                return true;
            }
            catch { return false; }
        }

        static string StrOf(Dictionary<string, object> d, string k, string def)
        {
            object v;
            return (d.TryGetValue(k, out v) && v != null) ? Convert.ToString(v) : def;
        }
        static int IntOf(Dictionary<string, object> d, string k, int def)
        {
            object v;
            int r;
            return (d.TryGetValue(k, out v) && v != null && int.TryParse(Convert.ToString(v), out r)) ? r : def;
        }
        static bool BoolOf(Dictionary<string, object> d, string k, bool def)
        {
            object v;
            bool r;
            return (d.TryGetValue(k, out v) && v != null && bool.TryParse(Convert.ToString(v), out r)) ? r : def;
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
        public string Name { get; set; }
        public string FullName { get; set; }
        public string Url { get; set; }
        public string Desc { get; set; }
        public int Stars { get; set; }
        public string Lang { get; set; }
        public string Branch { get; set; }
        public string Pushed { get; set; }

        public StoreItem()
        {
            Name = ""; FullName = ""; Url = ""; Desc = ""; Stars = -1; Lang = ""; Branch = ""; Pushed = "";
        }
    }

    // ---------- 轻量 HTTP (代理 + 智能编码) ----------
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
                if (c >= 0xE0 && c <= 0xEF) { if (i + 2 >= b.Length || (b[i + 1] & 0xC0) != 0x80 || (b[i + 2] & 0xC0) != 0x80) return false; i += 3; continue; }
                if (c >= 0xF0 && c <= 0xF4) { if (i + 3 >= b.Length || (b[i + 1] & 0xC0) != 0x80 || (b[i + 2] & 0xC0) != 0x80 || (b[i + 3] & 0xC0) != 0x80) return false; i += 4; continue; }
                return false;
            }
            return true;
        }
    }

    // ---------- 商城本地缓存 (官方 JSON 序列化器) ----------
    static class StoreCache
    {
        const int ListTtlSec = 6 * 3600;

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
                var ser = new JavaScriptSerializer();
                File.WriteAllText(Path.Combine(Dir(), "store.json"),
                    ser.Serialize(new { t = DateTime.UtcNow.Ticks, items = items }), Encoding.UTF8);
            }
            catch { }
        }

        public static List<StoreItem> LoadList(out long ageSec)
        {
            ageSec = long.MaxValue;
            try
            {
                string f = Path.Combine(Dir(), "store.json");
                if (!File.Exists(f)) return null;
                var ser = new JavaScriptSerializer();
                var root = ser.DeserializeObject(File.ReadAllText(f, Encoding.UTF8)) as Dictionary<string, object>;
                if (root == null) return null;
                object tObj;
                long ticks;
                if (root.TryGetValue("t", out tObj) && long.TryParse(Convert.ToString(tObj), out ticks))
                    ageSec = Math.Max(0, (DateTime.UtcNow.Ticks - ticks) / TimeSpan.TicksPerSecond);
                var arr = root["items"] as object[];
                if (arr == null) return null;
                var list = new List<StoreItem>();
                foreach (object o in arr)
                {
                    var it = ser.ConvertToType<StoreItem>(o);
                    if (it != null && !string.IsNullOrEmpty(it.FullName)) list.Add(it);
                }
                return list.Count > 0 ? list : null;
            }
            catch { return null; }
        }

        public static int ListTtl() { return ListTtlSec; }
    }

    // ============================================================
    //  核心逻辑
    // ============================================================
    class Dsh
    {
        public LauncherConfig Cfg;
        public EnvInfo Env = new EnvInfo();
        public UpdateInfo Update = new UpdateInfo();
        public Process ServerProc;

        public Action<string> OnStatus;     // 状态文字回调 (UI 线程外触发, 界面自行 Dispatcher)
        public Action<string> OnLog;        // 日志回调

        const string MirrorRegistry = "https://registry.npmmirror.com";

        // ---------- 命令助手 ----------
        public static string RunCaptureStatic(string program, string args, int timeoutMs)
        {
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
                    if (p.ExitCode != 0) return null;
                    return sb.ToString();
                }
            }
            catch { return null; }
        }

        public static string RunCapture(string program, string args, int timeoutMs)
        {
            long t0 = Environment.TickCount;
            string r = RunCaptureStatic(program, args, timeoutMs);
            if (Proc.DebugMode)
                Proc.DLog("run", program + " " + args + " -> " + (r == null ? "TIMEOUT/FAIL" : "ok(" + r.Length + "B)") + " in " + (Environment.TickCount - t0) + "ms");
            return r;
        }

        public static string FirstLine(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            string[] lines = s.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            return lines.Length > 0 ? lines[0].Trim() : "";
        }

        public string RunGit(string args, int timeoutMs)
        {
            string git = !string.IsNullOrEmpty(Env.GitPath) ? Env.GitPath : "git";
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

        public void AppendLog(string line)
        {
            if (string.IsNullOrEmpty(line)) return;
            try
            {
                Directory.CreateDirectory(Cfg.LogDir);
                File.AppendAllText(Path.Combine(Cfg.LogDir, "launcher.log"), DateTime.Now.ToString("HH:mm:ss") + "  " + line + "\r\n");
            }
            catch { }
            if (OnLog != null) { try { OnLog(line); } catch { } }
        }

        public static string ReadTail(string path, int maxLines)
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

        // ---------- 环境检测 ----------
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
                    if (!p.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && File.Exists(p + ".exe")) return p + ".exe";
                }
            }
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
                vers.Reverse();
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

        public EnvInfo DetectEnvironment()
        {
            var env = new EnvInfo();
            try
            {
                if (Cfg.DshCommand.IndexOf('\\') >= 0 && File.Exists(Cfg.DshCommand))
                    env.DshPath = Cfg.DshCommand;
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

                string dshCmd = string.IsNullOrEmpty(env.DshPath) ? "dsh" : env.DshPath;
                string ver = RunCapture("cmd.exe", "/c \"" + dshCmd + "\" --version", 15000);
                if (ver != null)
                {
                    Match m = Regex.Match(ver, "(\\d+\\.\\d+\\.\\d+[^\\s]*)");
                    if (m.Success) env.DshVersion = m.Groups[1].Value;
                }

                if (!string.IsNullOrEmpty(Cfg.NpmPath) && File.Exists(Cfg.NpmPath)) env.NpmPath = Cfg.NpmPath;
                else env.NpmPath = FirstLine(RunCapture("where", "npm", 10000));
                if (string.IsNullOrEmpty(env.NpmPath)) env.NpmPath = FindTool("npm.cmd", "npm.exe");
                env.NpmVersion = FirstLine(RunCapture("cmd.exe", "/c npm --version", 15000));

                if (!string.IsNullOrEmpty(Cfg.GitPath) && File.Exists(Cfg.GitPath)) env.GitPath = Cfg.GitPath;
                else env.GitPath = FindGit();
                env.GitVersion = FirstLine(RunCapture("cmd.exe", "/c git --version", 15000));

                if (!string.IsNullOrEmpty(Cfg.NodePath) && File.Exists(Cfg.NodePath)) env.NodePath = Cfg.NodePath;
                else env.NodePath = FindNode();
                env.NodeVersion = FirstLine(RunCapture("cmd.exe", "/c node --version", 15000));

                env.DshHomeExists = Directory.Exists(Cfg.DshHome);
                if (Directory.Exists(Cfg.PluginsRoot))
                {
                    env.PluginDirs = Directory.GetDirectories(Cfg.PluginsRoot).Length;
                    foreach (string d in Directory.GetDirectories(Cfg.PluginsRoot))
                        if (Directory.Exists(Path.Combine(d, ".git"))) env.PluginGitRepos++;
                }
            }
            catch { }
            return env;
        }

        // ---------- 代理: 多级自动探测 ----------
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
            if (!string.IsNullOrEmpty(Cfg.Proxy))
            {
                if (TestProxy(Cfg.Proxy)) { ApplyProxy(Cfg.Proxy); return Cfg.Proxy; }
            }
            if (proxyChecked) return detectedProxy;
            proxyChecked = true;

            string env = Environment.GetEnvironmentVariable("HTTPS_PROXY");
            if (string.IsNullOrEmpty(env)) env = Environment.GetEnvironmentVariable("HTTP_PROXY");
            if (string.IsNullOrEmpty(env)) env = Environment.GetEnvironmentVariable("ALL_PROXY");
            if (!string.IsNullOrEmpty(env) && TestProxy(env)) detectedProxy = env;

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
                Proc.DLog("proxy", "detected " + detectedProxy);
                ApplyProxy(detectedProxy);
                if (string.IsNullOrEmpty(Cfg.Proxy))
                {
                    Cfg.Proxy = detectedProxy;
                    try { Cfg.Save(); } catch { }
                }
            }
            return detectedProxy;
        }

        void ApplyProxy(string p)
        {
            try
            {
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

        // ---------- 端口 / 服务 ----------
        public static bool IsPortOpen(int port)
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

        static void KillProcessTree(int pid)
        {
            try
            {
                Process.Start(new ProcessStartInfo("taskkill", string.Format("/pid {0} /T /F", pid))
                { UseShellExecute = false, CreateNoWindow = true });
            }
            catch { }
        }

        public void OpenBrowser()
        {
            try { Process.Start(string.Format("http://127.0.0.1:{0}", Cfg.Port)); } catch { }
        }

        public void StartServiceAsync()
        {
            var worker = new Thread(delegate()
            {
                if (ServerProc != null && !ServerProc.HasExited) { OpenBrowser(); return; }
                if (IsPortOpen(Cfg.Port))
                {
                    if (Cfg.RestartIfRunning)
                    {
                        Report("检测到旧服务，正在重启…");
                        int pid = FindPidByPort(Cfg.Port);
                        if (pid > 0) KillProcessTree(pid);
                        for (int i = 0; i < 20 && IsPortOpen(Cfg.Port); i++) Thread.Sleep(300);
                    }
                    else
                    {
                        OpenBrowser();
                        Report("服务已在运行");
                        return;
                    }
                }
                Report("正在启动服务…");
                Proc.DLog("svc", "start begin; cmd=" + Cfg.DshCommand + " port=" + Cfg.Port + " logdir=" + Cfg.LogDir);
                string launchError = "";
                string args = string.Format("/c {0} web --host 127.0.0.1 --port {1}", Cfg.DshCommand, Cfg.Port);
                try
                {
                    var psi = new ProcessStartInfo("cmd.exe", args)
                    {
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        WorkingDirectory = Cfg.LogDir
                    };
                    var p = new Process { StartInfo = psi };
                    p.OutputDataReceived += delegate(object o, DataReceivedEventArgs e) { AppendLog(e.Data); };
                    p.ErrorDataReceived += delegate(object o, DataReceivedEventArgs e) { AppendLog(e.Data); };
                    p.Exited += delegate { AppendLog("[server exited]"); };
                    p.EnableRaisingEvents = true;
                    p.Start();
                    p.BeginOutputReadLine();
                    p.BeginErrorReadLine();
                    ServerProc = p;
                }
                catch (Exception ex)
                {
                    launchError = ex.Message;
                    AppendLog("[launch error] " + ex.Message);
                }
                int waited = 0;
                while (waited < 120 && !IsPortOpen(Cfg.Port) && (ServerProc == null || !ServerProc.HasExited))
                {
                    Thread.Sleep(500);
                    waited++;
                }
                bool ok = IsPortOpen(Cfg.Port);
                if (ok)
                {
                    Report("服务已就绪");
                    if (Cfg.OpenBrowserOnStart) OpenBrowser();
                }
                else
                {
                    Report(launchError.Length > 0 ? "启动失败: " + launchError : "启动失败: 服务未能就绪，可能是 dsh 未安装或配置错误");
                }
            });
            worker.IsBackground = true;
            worker.Start();
        }

        public void StopServiceAsync()
        {
            var worker = new Thread(delegate()
            {
                Report("正在停止服务…");
                if (ServerProc != null && !ServerProc.HasExited)
                {
                    try { KillProcessTree(ServerProc.Id); } catch { }
                    ServerProc = null;
                }
                else
                {
                    int pid = FindPidByPort(Cfg.Port);
                    if (pid > 0) KillProcessTree(pid);
                }
                for (int i = 0; i < 20 && IsPortOpen(Cfg.Port); i++) Thread.Sleep(300);
                Report(IsPortOpen(Cfg.Port) ? "服务未能停止" : "服务已停止");
            });
            worker.IsBackground = true;
            worker.Start();
        }

        public void RestartServiceAsync()
        {
            var worker = new Thread(delegate()
            {
                Report("正在停止服务…");
                if (ServerProc != null && !ServerProc.HasExited)
                {
                    try { KillProcessTree(ServerProc.Id); } catch { }
                    ServerProc = null;
                }
                else
                {
                    int pid = FindPidByPort(Cfg.Port);
                    if (pid > 0) KillProcessTree(pid);
                }
                for (int i = 0; i < 20 && IsPortOpen(Cfg.Port); i++) Thread.Sleep(300);
                Report("正在启动服务…");
                StartServiceAsync();
            });
            worker.IsBackground = true;
            worker.Start();
        }

        void Report(string s)
        {
            if (OnStatus != null) { try { OnStatus(s); } catch { } }
        }

        // ---------- 更新检查 ----------
        string NpmRegArg()
        {
            if (!string.IsNullOrEmpty(Cfg.NpmRegistry)) return " --registry " + Cfg.NpmRegistry;
            return "";
        }

        public string QueryNpmLatest(string pkg)
        {
            string latest = RunCapture("cmd.exe", "/c npm view " + pkg + " version" + NpmRegArg(), 25000);
            if (latest == null && string.IsNullOrEmpty(Cfg.NpmRegistry))
            {
                AppendLog("[npm] 官方源查询失败, 回退国内镜像 " + MirrorRegistry);
                latest = RunCapture("cmd.exe", "/c npm view " + pkg + " version --registry " + MirrorRegistry, 30000);
            }
            return latest;
        }

        public string NpmInstallGlobal(string pkg, int timeoutMs)
        {
            Proc.DLog("npm", "install -g " + pkg + " reg=" + (string.IsNullOrEmpty(Cfg.NpmRegistry) ? "(default)" : Cfg.NpmRegistry));
            string r = RunCapture("cmd.exe", "/c npm install -g " + pkg + NpmRegArg(), timeoutMs);
            if (r == null && string.IsNullOrEmpty(Cfg.NpmRegistry))
            {
                AppendLog("[npm] 安装失败, 回退国内镜像重试 " + MirrorRegistry);
                r = RunCapture("cmd.exe", "/c npm install -g " + pkg + " --registry " + MirrorRegistry, timeoutMs);
            }
            return r;
        }

        string FindDshPackageJson()
        {
            try
            {
                string npmRoot = RunCapture("cmd.exe", "/c npm root -g", 15000);
                if (npmRoot != null)
                {
                    string p = Path.Combine(npmRoot.Trim(), Cfg.NpmPackage, "package.json");
                    if (File.Exists(p)) return p;
                }
            }
            catch { }
            return null;
        }

        public UpdateInfo CheckUpdates(EnvInfo env)
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

                string latest = QueryNpmLatest(Cfg.NpmPackage);
                if (latest != null)
                {
                    latest = latest.Trim();
                    if (latest.Length > 0 && latest[0] == 'v') latest = latest.Substring(1);
                    info.DshLatest = latest;
                }
                info.DshUpdate = !string.IsNullOrEmpty(info.DshCurrent) && !string.IsNullOrEmpty(info.DshLatest)
                    && !info.DshCurrent.Equals(info.DshLatest, StringComparison.OrdinalIgnoreCase);

                var names = new List<string>();
                if (Directory.Exists(Cfg.PluginsRoot))
                {
                    foreach (string dir in Directory.GetDirectories(Cfg.PluginsRoot))
                    {
                        if (!Directory.Exists(Path.Combine(dir, ".git"))) continue;
                        string name = Path.GetFileName(dir);
                        // 按本地当前分支对比远程同名分支
                        string branch = FirstLine(RunGit(string.Format("-C \"{0}\" rev-parse --abbrev-ref HEAD", dir), 10000));
                        if (string.IsNullOrEmpty(branch)) continue;
                        string remote = RunGit(string.Format("-C \"{0}\" ls-remote origin {1}", dir, branch), 20000);
                        string local = RunGit(string.Format("-C \"{0}\" rev-parse HEAD", dir), 10000);
                        if (remote != null && local != null)
                        {
                            string[] parts = remote.Split(new char[] { '\t', ' ', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                            string rh = parts.Length > 0 ? parts[0] : "";
                            string lh = local.Trim();
                            if (rh.Length >= 7 && !rh.Equals(lh, StringComparison.OrdinalIgnoreCase))
                                names.Add(name);
                            continue;
                        }
                        // 本地分支与远程无同名分支 (如本地 master / 远程 main): 对比远程默认分支 HEAD
                        string remoteHead = RunGit(string.Format("-C \"{0}\" ls-remote origin HEAD", dir), 20000);
                        if (remoteHead != null && local != null)
                        {
                            string[] hp = remoteHead.Split(new char[] { '\t', ' ', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                            string rhh = hp.Length > 0 ? hp[0] : "";
                            string lh2 = local.Trim();
                            if (rhh.Length >= 7 && !rhh.Equals(lh2, StringComparison.OrdinalIgnoreCase))
                                names.Add(name);
                        }
                    }
                }
                info.PluginCount = names.Count;
                info.PluginNames = string.Join(", ", names.ToArray());
                info.HasUpdate = info.DshUpdate || info.PluginCount > 0;
                var parts2 = new List<string>();
                if (info.DshUpdate) parts2.Add(string.Format("DSH {0} → {1}", info.DshCurrent, info.DshLatest));
                if (info.PluginCount > 0) parts2.Add(string.Format("插件 {0} 个", info.PluginCount));
                info.Detail = string.Join(" · ", parts2.ToArray());
            }
            catch { info.HasUpdate = false; }
            return info;
        }

        // ---------- 插件扫描 ----------
        public List<PluginItem> ScanPlugins()
        {
            var list = new List<PluginItem>();
            try
            {
                if (Directory.Exists(Cfg.PluginsRoot))
                {
                    foreach (string d in Directory.GetDirectories(Cfg.PluginsRoot))
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
                            p.RemoteUrl = FirstLine(RunGit(string.Format("-C \"{0}\" config --get remote.origin.url", d), 10000));
                            p.Branch = FirstLine(RunGit(string.Format("-C \"{0}\" rev-parse --abbrev-ref HEAD", d), 10000));
                        }
                        list.Add(p);
                    }
                }
            }
            catch { }
            return list;
        }

        public bool IsPluginInstalled(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            try
            {
                string root = Cfg.PluginsRoot;
                if (!Directory.Exists(root)) return false;
                foreach (string d in Directory.GetDirectories(root))
                {
                    string n = Path.GetFileName(d);
                    if (n.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase))
                        n = n.Substring(0, n.Length - ".disabled".Length);
                    if (string.Equals(n, name, StringComparison.OrdinalIgnoreCase)) return true;
                }
            }
            catch { }
            return false;
        }

        // ---------- 商城拉取 (GitHub 多关键词组合检索 + npm 插件生态 + Awesome 多源聚合) ----------
        static readonly Regex LinkRe = new Regex("\\[([^\\]\\n]*)\\]\\((https://github\\.com/[^)\\s]+)\\)", RegexOptions.Compiled);

        public static List<StoreItem> FetchStore(string proxy)
        {
            var got = new List<StoreItem>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            Action<List<StoreItem>> merge = delegate(List<StoreItem> batch)
            {
                if (batch == null) return;
                foreach (var it in batch)
                {
                    if (it != null && !string.IsNullOrEmpty(it.FullName) && seen.Add(it.FullName))
                        got.Add(it);
                }
            };

            // 1. 组合 Query 抓取 GitHub 插件生态
            string[] queries = {
                "topic%3Adsh-plugin",
                "topic%3Adeepseek-harness",
                "topic%3Adsh-extension",
                "deepseek-harness-plugin",
                "topic%3Amcp-server+deepseek"
            };

            foreach (string q in queries)
            {
                for (int page = 1; page <= 3; page++)
                {
                    string json = null;
                    try
                    {
                        byte[] b = SmartHttp.Get("https://api.github.com/search/repositories?q=" + q + "&sort=stars&order=desc&per_page=100&page=" + page, proxy, 10000);
                        if (b != null) json = SmartHttp.Decode(b);
                    }
                    catch { }
                    if (string.IsNullOrEmpty(json)) break;
                    var batch = ParseStoreJson(json);
                    if (batch.Count == 0) break;
                    merge(batch);
                    if (batch.Count < 100) break;
                    try { Thread.Sleep(400); } catch { }
                }
            }

            // 2. npm 官方插件包检索 (深度覆盖 npm 发布的 deepseek/dsh 插件)
            try
            {
                string npmUrl = "https://registry.npmjs.org/-/v1/search?text=keywords:deepseek-harness,dsh-plugin,mcp-server&size=100";
                byte[] nb = SmartHttp.Get(npmUrl, proxy, 8000);
                if (nb != null)
                {
                    string njson = SmartHttp.Decode(nb);
                    var nlist = ParseNpmSearchJson(njson);
                    merge(nlist);
                }
            }
            catch { }

            // 3. Awesome 社区源聚合 (兜底与扩展)
            string[] mdUrls = {
                "https://cdn.jsdelivr.net/gh/bruc3van/awesome-dsh-plugin@main/README.md",
                "https://cdn.jsdelivr.net/gh/0xsline/awesome-deepseek-harness@main/README.md"
            };
            foreach (string u in mdUrls)
            {
                string md = null;
                try { byte[] b = SmartHttp.Get(u, proxy, 8000); if (b != null) md = SmartHttp.Decode(b); } catch { }
                var mdList = ParseMdList(md);
                merge(mdList);
            }

            return got;
        }

        static List<StoreItem> ParseNpmSearchJson(string json)
        {
            var list = new List<StoreItem>();
            if (string.IsNullOrEmpty(json)) return list;
            try
            {
                var ser = new JavaScriptSerializer();
                var root = ser.DeserializeObject(json) as Dictionary<string, object>;
                if (root == null) return list;
                var objects = root["objects"] as object[];
                if (objects == null) return list;
                foreach (object o in objects)
                {
                    var d = o as Dictionary<string, object>;
                    if (d == null) continue;
                    object pkgObj;
                    if (!d.TryGetValue("package", out pkgObj) || pkgObj == null) continue;
                    var pkg = pkgObj as Dictionary<string, object>;
                    if (pkg == null) continue;
                    var it = new StoreItem();
                    it.Name = JStr(pkg, "name");
                    it.FullName = it.Name;
                    it.Desc = JStr(pkg, "description");
                    it.Lang = "JavaScript";
                    object linksObj;
                    if (pkg.TryGetValue("links", out linksObj) && linksObj is Dictionary<string, object>)
                    {
                        var links = linksObj as Dictionary<string, object>;
                        it.Url = JStr(links, "repository");
                        if (string.IsNullOrEmpty(it.Url)) it.Url = JStr(links, "npm");
                    }
                    if (string.IsNullOrEmpty(it.Url)) it.Url = "https://www.npmjs.com/package/" + it.Name;
                    it.Stars = 10;
                    if (!string.IsNullOrEmpty(it.Name)) list.Add(it);
                }
            }
            catch { }
            return list;
        }

        public static List<StoreItem> ParseStoreJson(string json)
        {
            var list = new List<StoreItem>();
            if (string.IsNullOrEmpty(json)) return list;
            try
            {
                var ser = new JavaScriptSerializer();
                var root = ser.DeserializeObject(json) as Dictionary<string, object>;
                if (root == null) return list;
                var items = root["items"] as object[];
                if (items == null) return list;
                foreach (object o in items)
                {
                    var d = o as Dictionary<string, object>;
                    if (d == null) continue;
                    var it = new StoreItem();
                    it.FullName = JStr(d, "full_name");
                    it.Url = JStr(d, "html_url");
                    it.Desc = JStr(d, "description");
                    it.Lang = JStr(d, "language");
                    it.Branch = JStr(d, "default_branch");
                    it.Pushed = JStr(d, "pushed_at");
                    if (it.Pushed.Length >= 10) it.Pushed = it.Pushed.Substring(0, 10);
                    it.Stars = JInt(d, "stargazers_count");
                    if (string.IsNullOrEmpty(it.FullName)) continue;
                    int slash = it.FullName.IndexOf('/');
                    it.Name = slash >= 0 ? it.FullName.Substring(slash + 1) : it.FullName;
                    list.Add(it);
                }
            }
            catch { }
            return list;
        }

        static string JStr(Dictionary<string, object> d, string key)
        {
            object v;
            return (d.TryGetValue(key, out v) && v != null) ? Convert.ToString(v) : "";
        }

        static int JInt(Dictionary<string, object> d, string key)
        {
            object v;
            if (d.TryGetValue(key, out v) && v != null)
            {
                int n;
                if (int.TryParse(Convert.ToString(v), out n)) return n;
            }
            return -1;
        }

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
            s = Regex.Replace(s, "\\[([^\\]]*)\\]\\([^)]*\\)", "$1");
            s = Regex.Replace(s, "<[^>]+>", " ");
            s = s.Replace("`", "").Replace("|", " ").Replace("·", " ");
            s = Regex.Replace(s, "\\s+", " ").Trim();
            s = s.TrimStart('-', '—', '–', '*', '>', ':').Trim();
            return s;
        }

        // 桌面快捷方式 (WScript.Shell 反射创建)
        public string CreateDesktopShortcut()
        {
            try
            {
                string lnk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "DeepSeek Harness.lnk");
                Type t = Type.GetTypeFromProgID("WScript.Shell");
                object shell = Activator.CreateInstance(t);
                object shortcut = t.InvokeMember("CreateShortcut", System.Reflection.BindingFlags.InvokeMethod, null, shell, new object[] { lnk });
                Type st = shortcut.GetType();
                st.InvokeMember("TargetPath", System.Reflection.BindingFlags.SetProperty, null, shortcut, new object[] { System.Reflection.Assembly.GetExecutingAssembly().Location });
                st.InvokeMember("WorkingDirectory", System.Reflection.BindingFlags.SetProperty, null, shortcut, new object[] { AppDomain.CurrentDomain.BaseDirectory });
                st.InvokeMember("Description", System.Reflection.BindingFlags.SetProperty, null, shortcut, new object[] { "DeepSeek Harness 启动器" });
                try { st.InvokeMember("IconLocation", System.Reflection.BindingFlags.SetProperty, null, shortcut, new object[] { Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "deepseek.ico") + ",0" }); } catch { }
                st.InvokeMember("Save", System.Reflection.BindingFlags.InvokeMethod, null, shortcut, null);
                return "";
            }
            catch (Exception ex) { return ex.Message; }
        }

        // 插件本地版本 (git 短哈希)
        public string LocalHash(PluginItem p)
        {
            string r = RunGit(string.Format("-C \"{0}\" rev-parse --short HEAD", p.Path), 8000);
            return string.IsNullOrEmpty(r) ? "" : r.Trim();
        }

        // ---------- 插件操作 ----------
        public string InstallPluginFromUrl(string url)
        {
            // 注入防护: 拒绝 shell 元字符
            if (url.IndexOf('"') >= 0 || url.IndexOf('&') >= 0 || url.IndexOf('|') >= 0
                || url.IndexOf(';') >= 0 || url.IndexOf('>') >= 0 || url.IndexOf('<') >= 0
                || url.IndexOf('`') >= 0 || url.IndexOf('$') >= 0 || url.IndexOf('%') >= 0)
                return "地址格式不正确，请输入完整的 git 仓库地址。";
            string name = Path.GetFileName(url.TrimEnd('/'));
            if (name.EndsWith(".git")) name = name.Substring(0, name.Length - 4);
            string target = Path.Combine(Cfg.PluginsRoot, name);
            if (Directory.Exists(target) || File.Exists(target))
                return "目标目录已存在：\n" + target;
            Report("正在克隆插件 " + name + " …");
            string r = RunGit("clone \"" + url + "\" \"" + target + "\"", 300000);
            AppendLog("[plugin] clone " + url + (r == null ? " (超时/失败)" : " 完成"));
            return r == null ? "克隆失败（网络或地址错误）" : "";
        }

        public string InstallNpmPlugin(string pkg)
        {
            Report("正在安装插件 " + pkg + " …");
            string r = RunCapture("cmd.exe", "/c npm install -g " + pkg, 300000);
            AppendLog("[plugin] npm install -g " + pkg + (r == null ? " (超时/失败)" : " 完成"));
            return r == null ? "npm 安装失败（网络或包名错误）" : "";
        }

        // 启用/禁用: 目录重命名加 .disabled 后缀, 可逆
        public string TogglePlugin(PluginItem p)
        {
            try
            {
                string target = p.Disabled
                    ? p.Path.Substring(0, p.Path.Length - ".disabled".Length)
                    : p.Path + ".disabled";
                Directory.Move(p.Path, target);
                Report((p.Disabled ? "已启用插件 " : "已禁用插件 ") + p.Name);
                return "";
            }
            catch (Exception ex) { return ex.Message; }
        }

        public string UninstallPlugin(PluginItem p)
        {
            try
            {
                ClearReadOnly(p.Path);   // git 松散对象是只读文件, 必须先清除只读属性
                Directory.Delete(p.Path, true);
                Report("已卸载插件 " + p.Name);
                return "";
            }
            catch (Exception ex) { return "目录可能被占用或权限不足：" + ex.Message; }
        }

        static void ClearReadOnly(string root)
        {
            try
            {
                foreach (string f in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
                {
                    try { File.SetAttributes(f, FileAttributes.Normal); } catch { }
                }
                foreach (string d in Directory.GetDirectories(root, "*", SearchOption.AllDirectories))
                {
                    try { File.SetAttributes(d, FileAttributes.Normal); } catch { }
                }
            }
            catch { }
        }

        // 智能更新单个插件: 只有远程确实有可拉取的提交才执行 pull, 否则明确报告"已是最新", 绝不误报失败
        public string PullPlugin(PluginItem p)
        {
            string branch = FirstLine(RunGit(string.Format("-C \"{0}\" rev-parse --abbrev-ref HEAD", p.Path), 10000));
            if (string.IsNullOrEmpty(branch)) branch = "HEAD";

            // 1. 有远程跟踪分支: 按上游判断
            string upstream = RunGit(string.Format("-C \"{0}\" rev-parse --abbrev-ref --symbolic-full-name @{{u}}", p.Path), 10000);
            if (upstream != null)
            {
                string behind = RunGit(string.Format("-C \"{0}\" rev-list --count HEAD..@{{u}}", p.Path), 10000);
                int n = 0;
                behind = (behind ?? "").Trim();
                if (behind.Length > 0 && int.TryParse(behind, out n) && n <= 0)
                    return "已是最新";
                if (behind.Length == 0)
                {
                    // 无法读取落后数 → 保守: 尝试快速拉取
                    string r0 = RunGit(string.Format("-C \"{0}\" pull --ff-only", p.Path), 120000);
                    if (r0 == null) return "拉取失败（网络或冲突）";
                    return "已更新至最新";
                }
                string r = RunGit(string.Format("-C \"{0}\" pull --ff-only", p.Path), 120000);
                AppendLog("[plugin] git pull " + p.Name + (r == null ? " (失败)" : " 完成") + " ahead=" + n);
                if (r == null) return "拉取失败（网络或冲突）";
                return "已更新至最新";
            }

            // 2. 无上游跟踪分支 (如本地 master / 远程 main): 对比远程默认分支 HEAD
            string remote = RunGit(string.Format("-C \"{0}\" ls-remote origin HEAD", p.Path), 20000);
            string local = RunGit(string.Format("-C \"{0}\" rev-parse HEAD", p.Path), 10000);
            if (remote != null && local != null)
            {
                string[] parts = remote.Split(new char[] { '\t', ' ', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                string rh = parts.Length > 0 ? parts[0] : "";
                string lh = local.Trim();
                if (rh.Length >= 7 && rh.Equals(lh, StringComparison.OrdinalIgnoreCase))
                    return "已是最新";   // 本地与远程默认分支一致 → 就是最新的, 不报失败

                // 远程有不同提交: 尝试 fetch + fast-forward (不依赖分支名一致)
                string fetched = RunGit(string.Format("-C \"{0}\" fetch origin", p.Path), 60000);
                if (fetched == null) return "拉取失败（网络或冲突）";
                string behind2 = RunGit(string.Format("-C \"{0}\" rev-list --count HEAD..FETCH_HEAD", p.Path), 10000);
                int n2;
                behind2 = (behind2 ?? "").Trim();
                if (behind2.Length > 0 && int.TryParse(behind2, out n2) && n2 <= 0)
                    return "已是最新";   // fetch 后发现其实没有落后
                string r2 = RunGit(string.Format("-C \"{0}\" merge --ff-only FETCH_HEAD", p.Path), 60000);
                if (r2 == null) return "拉取失败（本地有改动或冲突）";
                return "已更新至最新";
            }

            return "无法连接远程仓库";
        }

        // 一键维护: 更新所有 git 插件 (仅真正有更新的执行 pull, 已最新明确显示, 绝不误报失败)
        public string[] PullAllPlugins()
        {
            var results = new List<string>();
            foreach (var p in ScanPlugins())
            {
                if (!p.IsGit) continue;
                string r = PullPlugin(p);
                if (r == "已是最新") results.Add(p.Name + " ✓ 已是最新");
                else if (r == "已更新至最新") results.Add(p.Name + " ✨ 已更新");
                else results.Add(p.Name + " ⚠️ " + r);
            }
            return results.ToArray();
        }

        // 修复依赖: 在每个插件目录执行 npm install
        public string[] RepairPlugins()
        {
            var results = new List<string>();
            foreach (var p in ScanPlugins())
            {
                if (p.IsGit)
                {
                    string r = RunCapture("cmd.exe", "/c cd /d \"" + p.Path + "\" && npm install", 300000);
                    if (r != null) results.Add(p.Name + " (依赖正常)");
                    else results.Add(p.Name + " ⚠️ 依赖安装超时");
                }
            }
            return results.ToArray();
        }

        // ---------- 启动器自更新检查 (多源: 配置源 → jsDelivr CDN) ----------
        public string CheckLauncherUpdate()
        {
            var urls = new List<string>();
            if (!string.IsNullOrEmpty(Cfg.LauncherUpdateUrl)) urls.Add(Cfg.LauncherUpdateUrl);
            urls.Add("https://cdn.jsdelivr.net/gh/loudMore/dsh-launcher@main/version.txt");
            foreach (string u in urls)
            {
                Proc.DLog("lupd", "try " + u);
                string outp = RunCapture("curl.exe", "-s -L -m 25 \"" + u + "\"", 35000);
                if (outp == null) continue;
                string[] lines = outp.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                if (lines.Length > 0)
                {
                    Match m = Regex.Match(lines[0].Trim(), "(\\d+\\.\\d+\\.\\d+)");
                    if (m.Success) return m.Groups[1].Value;
                }
            }
            return null;
        }

        // ---------- 一键安装 (Node.js + dsh) ----------
        string GetLatestLtsUrl()
        {
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
                try { json = SmartHttp.Decode(SmartHttp.Get(indexUrls[i], Cfg.Proxy, 20000)); } catch { }
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

        // ---------- 一键安装 (支持指定路径或默认 LocalAppData) ----------
        public bool InstallDshNow(out string error, string customNodeDir = null)
        {
            error = "";
            try
            {
                string nodeHome = "";
                string nodeExe = FindNode();
                if (string.IsNullOrEmpty(nodeExe))
                {
                    Report("未检测到 Node.js，正在获取下载地址…");
                    string nodeUrl = GetLatestLtsUrl();
                    if (nodeUrl == null) { error = "无法获取 Node.js 下载地址（请检查网络连接后重试）"; return false; }
                    string ver = Regex.Match(nodeUrl, "node-(v[^/]+)-win-(x64|x86)").Groups[1].Value;
                    string arch = Regex.Match(nodeUrl, "node-(v[^/]+)-win-(x64|x86)").Groups[2].Value;
                    string zip = Path.Combine(Path.GetTempPath(), "node-" + ver + "-win-" + arch + ".zip");
                    Report("正在下载 Node.js " + ver + "（约 30MB）…");
                    if (!DownloadFile(nodeUrl, zip))
                    {
                        string mirrorZip = "https://npmmirror.com/mirrors/node/" + ver + "/node-" + ver + "-win-" + arch + ".zip";
                        AppendLog("[install] 官方源下载失败, 回退镜像 " + mirrorZip);
                        if (!DownloadFile(mirrorZip, zip))
                        { error = "Node.js 下载失败（请检查网络后重试）"; return false; }
                    }

                    if (!string.IsNullOrEmpty(customNodeDir))
                    {
                        nodeHome = customNodeDir;
                    }
                    else
                    {
                        nodeHome = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "node");
                    }

                    string tmp = Path.Combine(Path.GetTempPath(), "node-x-" + Guid.NewGuid().ToString("N"));
                    Directory.CreateDirectory(tmp);
                    if (Directory.Exists(nodeHome)) { try { Directory.Delete(nodeHome, true); } catch { } }
                    Directory.CreateDirectory(nodeHome);

                    Report("正在解压安装 Node.js…");
                    string tar = RunCapture("tar.exe", "-xf \"" + zip + "\" -C \"" + tmp + "\"", 120000);
                    if (tar == null) { error = "解压 Node.js 失败（系统可能缺少 tar.exe）"; return false; }
                    string[] dirs = Directory.GetDirectories(tmp);
                    if (dirs.Length == 0) { error = "解压后未找到 Node.js 目录"; return false; }

                    // 移动解压文件到指定目标目录
                    foreach (string file in Directory.GetFiles(dirs[0], "*", SearchOption.AllDirectories))
                    {
                        string rel = file.Substring(dirs[0].Length).TrimStart('\\', '/');
                        string dest = Path.Combine(nodeHome, rel);
                        Directory.CreateDirectory(Path.GetDirectoryName(dest));
                        File.Copy(file, dest, true);
                    }

                    try { File.Delete(zip); } catch { }
                    try { Directory.Delete(tmp, true); } catch { }

                    // 关键: 持久化写入用户环境变量 PATH 与当前进程 PATH，确保指令全局可用
                    AddUserPath(nodeHome);
                    Cfg.NodePath = Path.Combine(nodeHome, "node.exe");
                    Cfg.NpmPath = Path.Combine(nodeHome, "npm.cmd");
                    Cfg.Save();

                    nodeExe = Path.Combine(nodeHome, "node.exe");
                    AppendLog("[install] Node.js " + ver + " -> " + nodeHome + " (已写入持久化环境变量 PATH)");
                }
                else
                {
                    nodeHome = Path.GetDirectoryName(nodeExe);
                }

                string npmCmd = Path.Combine(nodeHome, "npm.cmd");
                if (!File.Exists(npmCmd)) npmCmd = "npm";
                Proc.DLog("install", "node=" + nodeExe + " npmCmd=" + npmCmd);
                Report("正在安装 dsh（npm install -g " + Cfg.NpmPackage + "）…");
                string r = NpmInstallGlobal(Cfg.NpmPackage, 360000);
                if (r == null) { error = "npm 安装 dsh 失败（超时）。请检查网络后重试。"; return false; }

                // 检测全局 npm 路径并写入环境变量
                string npmPrefix = RunCapture("cmd.exe", "/c npm config get prefix", 15000);
                if (!string.IsNullOrEmpty(npmPrefix))
                {
                    npmPrefix = npmPrefix.Trim().Trim('\r', '\n');
                    if (Directory.Exists(npmPrefix)) AddUserPath(npmPrefix);
                }

                AppendLog("[install] dsh installed via npm (全局指令已就绪)");
                Report("环境安装完成");
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                AppendLog("[install] error: " + error);
                return false;
            }
        }
    }
}
