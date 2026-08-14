# WPF 重构版 (wpf/)

DeepSeek Harness 启动器的 **WPF (.NET Framework 4.8)** 重构版：

- 🎨 **GPU 合成渲染**（WPF 矢量图形，圆角/渐变/文字走硬件加速，任意 DPI 清晰，PerMonitorV2）
- ✨ **原生动画过渡**（切页淡入、窗口淡入，合成器播放不掉帧）
- 🖱️ **WindowChrome 无边框窗**：四边缩放指针、最大化、Aero 吸附全为系统级
- ⚙️ 功能与 WinForms 版等价：一键安装/启动/停止、环境检测、插件管理与商城（分页 500+）、三维更新、托盘、单实例、中英双语、自更新、全自动代理探测
- 📦 **零依赖单 exe**：仅用 Windows 自带 .NET Framework 4.8 的 csc + GAC WPF 程序集编译，无需安装 Visual Studio

## 源码

```
wpf/
├─ WpfApp.cs        # 界面: 深色主题 / 6 页面 / 托盘 / 单实例
├─ Logic.cs         # 逻辑层: 配置/环境检测/代理/服务/插件/商城/更新/多语言
├─ StoreWindow.cs   # 插件商城窗口
├─ build.bat        # 一键编译 (csc + GAC WPF 引用)
├─ app.manifest     # PerMonitorV2 高 DPI + Win10/11
└─ README.md
```

## 编译

```
build.bat   →  DeepSeekHarness.exe (WPF)
```

## 自检

```
DeepSeekHarness.exe --selftest   →  生成 selftest.txt (全链路回归报告)
DeepSeekHarness.exe --page N     →  启动后自动切到第 N 页 (非侵入验证)
DeepSeekHarness.exe --store      →  启动后自动打开插件商城
```

> 注意：本目录为 WPF 重构版（进行中）；仓库根目录的 `Launcher.cs` 为现行 WinForms 稳定版（v1.5.0）。
