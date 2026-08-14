# 🐋 DeepSeek Harness 启动器 (dsh-launcher)

<div align="center">

**双击即用 · 一键傻瓜式安装 · 全方位维护 DeepSeek Harness**

[![Windows](https://img.shields.io/badge/Windows-7%2B-blue?style=flat-square&logo=windows)](../../releases)
[![.NET](https://img.shields.io/badge/.NET%20Framework-4.0-512BD4?style=flat-square&logo=dotnet)](../../)
[![License](https://img.shields.io/badge/License-MIT-green?style=flat-square)](./LICENSE)
[![Releases](https://img.shields.io/github/v/release/loudMore/dsh-launcher?style=flat-square)](../../releases)
[![Stars](https://img.shields.io/github/stars/loudMore/dsh-launcher?style=social)](../../)

> 让 **dsh 的安装、启动、更新、插件维护** 变成「点一下」的事。

</div>

---

## ✨ 为什么你需要它

装 dsh 要装 Node.js、要敲 npm 命令、要折腾镜像源；更新要查命令、插件要手动 git pull……
**太麻烦了。**

这个启动器把这一切打包成**一个 exe**：双击 → 过渡动画 → 主界面 → 点「**一键安装**」→ 喝杯水回来 → 点「**一键启动**」→ 浏览器自动打开。从此 dsh 的日常 = 双击一个图标。

| 你的身份 | 你的痛 | 我们的解 |
|---|---|---|
| 🐣 纯小白 | 没装 Node.js，不会命令行 | 「一键安装」自动下载 Node.js（带国内镜像兜底）并 npm 安装 dsh，全程无脑 |
| 🚀 日常用户 | 打开/更新/换插件麻烦 | 首页一个会变身的大按钮 + 插件页一键更新/维护 |
| 🔧 折腾党 | 版本混乱、依赖报错 | 三维版本看板（启动器/dsh/插件）+ 日志 + 修复依赖 |

## 🖼️ 界面预览

| 概览 | 插件管理 |
|---|---|
| ![概览](./docs/images/overview.png) | ![插件](./docs/images/plugins.png) |

| 更新中心 | 设置 |
|---|---|
| ![更新](./docs/images/updates.png) | ![设置](./docs/images/settings.png) |

## 🚀 核心功能

- **🎯 一键安装**：自动检测环境 → 无 Node.js 则自动下载解压（官方源失败自动切国内镜像）→ npm 安装 dsh（同样镜像兜底）→ 全程进度提示
- **▶ 一键启动**：大按钮随状态自动变身——未安装→「一键安装」、未启动→「一键启动」、运行中→「打开浏览器」+ 停止/重启，服务就绪自动打开 `http://127.0.0.1:8099`
- **🔄 三维更新策略**：启动器（GitHub）、dsh（npm）、插件（git）的当前/最新版本一目了然；启动时与每 3 小时**自动后台获取**，无需手动点
- **🧩 插件管理**：显示仓库地址/分支/可更新徽标；支持 git URL 克隆安装、npm 包名安装、单个更新、卸载、**一键维护**（全部更新 + 修复依赖）
- **🌐 中英切换**：默认跟随系统语言，也可手动切换；左侧导航/配置项带矢量图标，语言选项带地球图标与国旗
- **🛡️ 稳健容错**：崩溃自动记录 crash.log；操作失败给出行之有效的提示（如缺依赖 → 一键修复）；深色主题对话框
- **🖥️ 系统托盘**：关窗不退出，托盘常驻；单实例防重开
- **📐 高 DPI 响应式**：运行时读取系统缩放系数，窗口按屏幕工作区自动居中/收缩

## 📥 快速开始

1. 到 [Releases](../../releases) 下载 `DeepSeekHarness.exe`（**无需编译、无需安装**）
2. 双击运行 → 首页点击「**一键安装**」（已装好可跳过）
3. 点击「**一键启动**」→ 完事

> 所有设置都在「设置」页可视化修改，配置自动保存为 exe 同目录的 `launcher.json`。

## 🌍 自更新与镜像

- 启动器自更新指向本仓库 `version.txt`，发现新版本提示一键下载
- npm 官方源失败自动回退 `https://registry.npmmirror.com`
- Node.js 官方源失败自动回退 `https://npmmirror.com/mirrors/node/`
- 均可在设置中自定义

## 🔧 参与开发

单文件源码 `Launcher.cs`，双击 `build.bat` 即可编译（**无需 Visual Studio**，.NET Framework 4.0 自带编译器）。

```
dsh-launcher/
├─ Launcher.cs          # 全部源码（单文件）
├─ build.bat            # 一键编译（自动检测图标资源）
├─ app.manifest         # 高 DPI 感知
├─ version.txt          # 版本号（自更新源）
└─ .github/workflows/   # 打 tag 自动构建 exe 发布 Release
```

**发布流程**：改 `LauncherVersion` → 同步 `version.txt` → 推送打 tag（`v*`）→ CI 自动出 Release。

## 🤝 贡献

欢迎 Issue / PR：功能建议、多语言词条（`Lang` 字典）、bug 修复。

## ⚠️ 说明

- DeepSeek 官方 Logo 等图标资源未包含在本仓库，请自备同名文件（build.bat 支持无图标模式构建）
- 本工具非 DeepSeek 官方出品，仅作为 dsh 的便捷启动/维护器

## 📄 License

[MIT](./LICENSE)
