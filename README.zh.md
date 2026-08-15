# 🐋 dsh-launcher — DeepSeek Harness 启动器

> **给小白用的 DeepSeek Harness 管理工具——管理 dsh、插件、环境，一键全搞定。**

| 🇨🇳 中文 | 🇺🇸 English | 🇯🇵 日本語 | 🇰🇷 한국어 | 🇷🇺 Русский |
|---|---|---|---|---|
| [README.zh.md](README.zh.md) | [README.md](README.md) | [README.ja.md](README.ja.md) | [README.ko.md](README.ko.md) | [README.ru.md](README.ru.md) |

[![dsh-launcher](https://img.shields.io/badge/dsh--launcher-%E2%9C%93-4D6BFE?style=flat-square)](https://github.com/topics/dsh-launcher)
[![Windows](https://img.shields.io/badge/Windows-10%2B-blue?style=flat-square&logo=windows)](../../releases)
[![License](https://img.shields.io/badge/License-MIT-green?style=flat-square)](./LICENSE)
[![Releases](https://img.shields.io/github/v/release/loudMore/dsh-launcher?style=flat-square)](../../releases)

---

## ✨ 这是什么

**DeepSeek Harness (dsh) 的傻瓜式桌面管理工具**，把 dsh 的安装、启动、更新、插件维护全部收进一个图形界面，**不用敲一行命令**。

### 🎯 核心亮点

| | 说明 |
|---|---|
| 🧩 **管理 dsh & 插件** | 图形化管理所有插件：安装、更新、修复依赖、启用/禁用、一键维护；缺依赖自动修复，坏插件自动隔离不拖垮服务 |
| 🔄 **一键更新 & 维护** | 启动器 / dsh / 插件 三维更新看板，自动检查更新，一键全部升级 |
| ⚡ **一键安装 dsh** | 没装 Node.js？自动装好（支持自定义目录）；没装 dsh？一条命令的事 |
| 🔍 **环境检测** | 自动检测 Node / npm / Git / dsh，缺什么一目了然 |
| 🐣 **小白友好** | 双击即用，全程图形界面，无需接触命令行 |
| 🛍️ **插件商城** | 聚合 GitHub + npm + Awesome 数百插件，带星标/语言/更新日期 |
| 🌉 **自动代理** | 自动探测代理 + 国内镜像兜底，网络再差也能装 |
| 🎨 **美观现代** | 深/浅双主题、8 种语言、GPU 渲染 WPF 界面 |

---

## 界面预览

| 概览 | 插件管理 |
|---|---|
| ![概览](./docs/images/overview.png) | ![插件](./docs/images/plugins.png) |

| 更新中心 | 设置 |
|---|---|
| ![更新](./docs/images/updates.png) | ![设置](./docs/images/settings.png) |

| 插件商城 | 日志 |
|---|---|
| ![插件商城](./docs/images/store.png) | ![日志](./docs/images/logs.png) |

**其他语言界面：** [English](./docs/images/overview-en.png) · [日本語](./docs/images/overview-ja.png) · [한국어](./docs/images/overview-ko.png) · [Русский](./docs/images/overview-ru.png)

## 为什么需要它

装 dsh 要装 Node.js、要敲 npm 命令、要折腾镜像源；更新要查命令；插件要手动 `git pull`……**太麻烦了。**

这个启动器**就是给小白准备的 dsh 管理工具**——把 dsh 的安装、启动、更新、插件维护全部收进一个图形界面：

- 🎯 **环境检测 + 一键安装 dsh**：没装 Node.js？自动帮你装好（支持自定义目录）；没装 dsh？一条命令的事
- 🔄 **一键维护**：dsh 升级、所有插件更新、依赖修复，点一下全搞定
- 🧩 **图形化插件管理**：装插件不用敲命令，商城挑、按钮点，缺依赖自动修复，坏插件自动隔离不拖垮服务
- 🖥️ **开箱即用**：双击启动 → 点「一键启动」→ 浏览器自动打开，全程无需接触命令行

| 你是谁 | 你的痛点 | 我们的答案 |
|---|---|---|
| 🐣 新手 | 没装 Node.js、怕命令行 | 一键安装：Node.js 自动下载（官方源→国内镜像兜底），npm 装 dsh（同样兜底） |
| 🚀 日常用户 | 开关、更新、插件都麻烦 | 会变身的智能大按钮 + 一键插件维护 |
| 🔧 折腾党 | 版本混乱、依赖损坏 | 三维版本看板（启动器 / dsh / 插件）+ 日志 + 依赖修复 |

## 功能特性

- 🎨 **现代 WPF 界面** — GPU 合成渲染、PerMonitorV2 高分屏适配、深色/浅色双主题实时热切换、圆角微渐变卡片、丝滑切页动画、极简自适应滚动条
- 🎯 **一键安装** — 自动检测环境 → 缺失则自动装 Node.js（镜像兜底，**支持自定义安装目录**，自动持久化写入 PATH）→ npm 装 dsh（镜像兜底）
- ▶️ **一键启动** — 大按钮随状态变身：*安装* / *启动* / *打开浏览器* + 停止/重启；常驻服务状态监控器让 UI 永远与真实端口状态同步
- 🔄 **三维更新策略** — 启动器（GitHub version.txt）、dsh（npm）、插件（git）都显示 **当前/最新**，带转圈检查动画；启动时与每 3 小时自动检查；按钮状态感知（已最新自动置灰 ✓），更新完成后立即刷新
- 🧩 **插件管理** — 支持 **git 地址** 或 **npm 包名** 安装；单插件智能更新（仅当远程确有新提交才拉取——已最新绝不误报失败）、卸载、启用/禁用、一键维护（全部更新 + 修复依赖）
- 🛡️ **启动时插件自检** — 启动即扫描全部插件依赖，缺失自动从共享依赖池补齐；**修不好的坏插件自动隔离**（挂 .disabled）保证服务照常启动——同时弹窗明确指出哪个插件有问题，**附上可直接粘贴给 dsh 的修复命令**（`npm install -g <依赖>` / `cd <插件目录> && npm install`）
- 🛍️ **插件商城** — 独立商城窗口，聚合 **GitHub 多关键词 + npm 官方包源 + Awesome 列表**：每卡片显示星标/语言/最近更新，模糊搜索、按星标或名称排序、语言筛选、已安装插件自动标记 ✓（不可再点）、一键安装
- 🌉 **自动代理** — 自动探测 Clash / v2rayN 等（配置 → 环境变量 → 系统代理 → 常见端口扫描），npm / git / 更新全走代理；npm 与 Node.js 带国内镜像兜底
- 🌐 **多语言界面** — 默认跟随系统，支持**中 / 英 / 日 / 韩 / 俄 / 法 / 德 / 西**八种语言手动切换（带国旗图标）
- 🖱️ **现代系统托盘** — WPF 悬浮圆角卡片菜单（打开启动器/启动/停止/重启/浏览器/商城/主题切换/退出），关闭最小化到托盘、单实例
- 🧩 **现代化弹窗** — 全部提示/确认替换为深浅主题自适应的圆角微光对话框
- 📄 **日志查看器** — 终端式日志页：源切换（launcher.log / dsh.log）、实时过滤、复制、清空
- 🛡️ **健壮可靠** — 未处理异常写 crash.log、可操作错误提示、页面原子渲染不闪烁
- 📐 **高分屏适配** — 运行时 DPI 缩放；无边框窗口原生缩放与最大化（WindowChrome）

## 快速开始

1. 从 [Releases](../../releases) 下载 `DeepSeekHarness.exe` — **免安装、免构建**
2. 双击 → 点 **安装**（已装过 dsh 可跳过）
3. 点 **启动** → 完成

所有设置都在「设置」页，持久化到 exe 旁的 `launcher.json`。

## 自更新与镜像

- 启动器检查本仓库 `version.txt`，发现新版本提供一键下载
- npm 官方源失败自动回退 `https://registry.npmmirror.com`
- Node.js 回退 `https://npmmirror.com/mirrors/node/`
- 两者都可在设置里自定义

## 开发

WPF 源码（纯代码式、无 XAML 编译链）在 [`wpf/`](./wpf/README.md)——双击 `build.bat` 即可编译（无需 Visual Studio，用系统自带 .NET Framework 4.8 编译器 + GAC WPF 程序集）。

```
dsh-launcher/
├─ wpf/
│  ├─ WpfApp.cs           # 壳层 UI：标题栏/侧栏/6页面/托盘/闪屏/弹窗
│  ├─ Logic.cs            # 配置 / 环境 / 代理 / 服务 / 插件 / 商城 / 更新 / 多语言
│  ├─ StoreWindow.cs      # 插件商城窗口
│  ├─ build.bat           # 一键构建（csc + GAC WPF）
│  └─ app.manifest        # PerMonitorV2 高分屏感知
├─ version.txt            # 自更新版本源
└─ .github/workflows/     # tag → 自动构建 exe → 发布 Release
```

**发布流程**：改 `WpfApp.cs` 中版本号 → 同步 `version.txt` → push 并打 tag（`v*`）→ CI 自动构建并发布 exe。

## 参与贡献

欢迎提交 Issue 与 PR：功能建议、翻译（`Lang` 词典）、bug 修复。

## 说明

- 仓库不内置 DeepSeek 官方 logo 素材——自行在 `wpf/` 下放置同名文件（`build.bat` 无 logo 也能正常构建）
- 非 DeepSeek 官方产品，仅为 dsh 编写的便捷启动器/维护器

## 许可证

[MIT](./LICENSE)
