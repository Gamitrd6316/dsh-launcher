# 🐋 DeepSeek Harness Launcher (dsh-launcher)

<div align="center">

**Double-click to run · One-click install · Full lifecycle management for DeepSeek Harness**

[![Windows](https://img.shields.io/badge/Windows-7%2B-blue?style=flat-square&logo=windows)](../../releases)
[![License](https://img.shields.io/badge/License-MIT-green?style=flat-square)](./LICENSE)
[![Releases](https://img.shields.io/github/v/release/loudMore/dsh-launcher?style=flat-square)](../../releases)

> Installing, launching, updating and maintaining **dsh** — now just one click.

</div>

---

## ✨ Why

Setting up dsh means installing Node.js, running npm commands, fighting registry mirrors; updating means digging out commands; plugins mean manual `git pull`… **Too much hassle.**

This launcher packs it all into **one exe**: double-click → splash animation → click **Install** → come back in a minute → click **Start** → browser opens automatically.

| You are | Your pain | Our answer |
|---|---|---|
| 🐣 Beginner | No Node.js, fear of command line | One-click install downloads Node.js (with China mirror fallback) and installs dsh via npm automatically |
| 🚀 Daily user | Annoying open/update/plugin flows | A context-aware big button + one-click plugin maintenance |
| 🔧 Power user | Version chaos, broken deps | 3-way version board (launcher/dsh/plugins) + logs + dependency repair |

## 🖼️ Screenshots

| Overview | Plugins |
|---|---|
| ![Overview](./docs/images/overview.png) | ![Plugins](./docs/images/plugins.png) |

| Updates | Settings |
|---|---|
| ![Updates](./docs/images/updates.png) | ![Settings](./docs/images/settings.png) |

## 🚀 Features

- **🎯 One-click Install** — auto-detect → auto-download Node.js (mirror fallback) → npm install dsh (mirror fallback) → guided progress
- **▶ One-click Start** — the big button morphs by state: Install / Start / Open Browser + Stop/Restart; browser auto-opens when ready
- **🔄 3-way Update Strategy** — launcher (GitHub), dsh (npm), plugins (git); current/latest versions always visible; **auto-fetched on launch and every 3 hours**
- **🧩 Plugin Manager** — remote URL/branch/update badges; install via git URL or npm package name; per-plugin update, remove, one-click maintain (update all + fix deps)
- **🌐 Bilingual UI** — follows system language by default (zh/en), manual switch with flags; vector icons on nav & settings
- **🛡️ Robust** — crash.log on exceptions; actionable error hints; dark themed dialogs
- **🖥️ System Tray** — close-to-tray, single instance
- **📐 Hi-DPI Responsive** — runtime DPI scaling, auto-centered window per screen

## 📥 Quick Start

1. Download `DeepSeekHarness.exe` from [Releases](../../releases) — **no build, no install**
2. Double-click → click **Install** (skip if already set up)
3. Click **Start** → done

## 🔧 Development

Single-file source `Launcher.cs`; double-click `build.bat` to compile (no Visual Studio needed).

**Release flow**: bump `LauncherVersion` → sync `version.txt` → push & tag (`v*`) → CI builds the exe and publishes a Release automatically.

## 🤝 Contributing

Issues & PRs welcome: feature ideas, translations (the `Lang` dictionary), bug fixes.

## ⚠️ Notes

- Official DeepSeek logo assets are not included; supply your own same-named files (`build.bat` builds fine without them)
- Not an official DeepSeek product — just a handy launcher/maintainer for dsh

## 📄 License

[MIT](./LICENSE)
