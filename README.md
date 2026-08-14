# DeepSeek Harness 启动器 (dsh-launcher)

> 傻瓜式 Windows 启动器：**双击即用**。一键安装 / 启动 / 更新 / 维护 DeepSeek Harness (dsh) 与插件。

![DeepSeek](https://img.shields.io/badge/DeepSeek-Harness-blue) ![Platform](https://img.shields.io/badge/Windows-7%2B-blueviolet) ![License](https://img.shields.io/badge/License-MIT-green)

---

## ✨ 功能特性

| 模块 | 说明 |
|---|---|
| 🚀 一键启动 | 首页大按钮随状态自动变身：未安装→「一键安装」、未启动→「一键启动」、运行中→「打开浏览器」+ 停止/重启 |
| 📦 一键安装 | 自动检测并安装 **Node.js**（官方源失败自动回退国内镜像），再通过 **npm** 安装 dsh（同样带镜像兜底） |
| 🔄 更新策略 | **三维更新**：启动器自身（GitHub version.txt）、dsh（npm）、插件（git pull），当前版本/最新版本直观显示；启动时与每 3 小时自动后台获取，无需手动点 |
| 🧩 插件管理 | 扫描插件目录，显示仓库地址/分支/可更新徽标；支持 **git URL 克隆安装** 与 **npm 包名安装**、单个更新、卸载、一键维护（全部更新+修复依赖） |
| 🌐 中英切换 | 设置页一键切换简体中文 / English，重启生效 |
| 🖥️ 系统托盘 | 关闭窗口最小化到托盘，双击托盘图标随时唤回；单实例防重开 |
| 🎨 品牌视觉 | DeepSeek 蓝渐变 + 深色卡片式 UI，启动过渡动画（弹性缩放 + 光晕 + 进度条），自绘圆角按钮 |
| 📐 高 DPI 响应式 | 启动时自动读取系统缩放系数，所有尺寸等比计算；窗口按屏幕工作区自动居中/收缩，适配任意分辨率 |

---

## 📥 下载与使用（小白路线）

1. 到右侧 [Releases](../../releases) 下载最新 `DeepSeekHarness.exe`（**无需编译**）。
2. 双击运行：
   - 没装 Node.js / dsh？点击首页大按钮「**一键安装**」，全程自动（约 1-3 分钟）。
   - 已装好？点击「**一键启动**」，服务就绪后自动打开浏览器 `http://127.0.0.1:8099`。
3. 之后点「更新」页即可查看各组件版本与升级。

> 配置文件 `launcher.json` 会在 exe 同目录自动生成，所有设置也可在「设置」页修改。

---

## 🔧 参与开发

### 环境要求
- Windows 7+，**.NET Framework 4.0+** 自带编译器（无需装 Visual Studio！）
- 源码就是单文件 `Launcher.cs`

### 编译
双击 `build.bat` 即可（自动检测图标资源，缺失也能构建）：
```bat
build.bat
```

### 目录结构
```
dsh-launcher/
├─ Launcher.cs        # 全部源码（单文件）
├─ build.bat          # 一键编译脚本
├─ app.manifest       # 高 DPI 感知 + 通用控件 v6
├─ version.txt        # 启动器版本号（自更新源指向此文件）
└─ .github/workflows/release.yml   # 打 tag 自动构建 exe 并发布 Release
```

### 发布新版本（维护者）
1. 修改 `Launcher.cs` 中 `LauncherVersion` 常量。
2. 更新 `version.txt` 为相同版本号。
3. 推送并打 tag（`v1.4.0` 格式）→ GitHub Actions 自动构建 exe 并挂到 Release。
4. 已安装用户启动器会**自动后台发现新版本**并提示下载。

---

## 🌍 自更新与镜像说明

- **启动器自更新**：`launcher.json` 的 `launcherUpdateUrl` 默认指向本仓库
  `https://raw.githubusercontent.com/loudMore/dsh-launcher/main/version.txt`。
- **npm 镜像**：官方源失败自动回退 `https://registry.npmmirror.com`；也可在设置中指定其它源。
- **Node.js 镜像**：官方源失败自动回退 `https://npmmirror.com/mirrors/node/`。
- 无外网环境：请配置代理或局域网镜像后重试「一键安装」。

## 🤝 贡献
欢迎提 Issue / PR：功能建议、翻译（`Lang` 字典）、bug 修复。代码结构简单，核心逻辑集中在 `LauncherForm`。

## ⚠️ 说明
- 图标资源（DeepSeek 官方 Logo 等）未包含在本仓库，请自行放入同名文件或使用 `build.bat` 的无图标模式。
- 本工具非 DeepSeek 官方出品，仅作为 dsh 的便捷启动/维护器。

## 📄 License
MIT
