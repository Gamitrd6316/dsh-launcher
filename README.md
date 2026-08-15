# dsh-launcher

> The fool-proof launcher for DeepSeek Harness (dsh): **double-click to run, one click to install, update and maintain** — Node.js, dsh and plugins all taken care of, with China mirror fallbacks.

[English](README.md) | [中文](README.zh.md) | [日本語](README.ja.md) | [한국어](README.ko.md) | [Русский](README.ru.md)

[![dsh-launcher](https://img.shields.io/badge/dsh--launcher-%E2%9C%93-4D6BFE?style=flat-square)](https://github.com/topics/dsh-launcher)
[![Windows](https://img.shields.io/badge/Windows-10%2B-blue?style=flat-square&logo=windows)](../../releases)
[![License](https://img.shields.io/badge/License-MIT-green?style=flat-square)](./LICENSE)
[![Releases](https://img.shields.io/github/v/release/loudMore/dsh-launcher?style=flat-square)](../../releases)

## Screenshots

| Overview (EN) | Plugins (EN) |
|---|---|
| ![Overview](./docs/images/overview-en.png) | ![Plugins](./docs/images/plugins-en.png) |

| Updates (EN) | Settings (EN) |
|---|---|
| ![Updates](./docs/images/updates-en.png) | ![Settings](./docs/images/settings-en.png) |

| Plugin Store (EN) | Logs (EN) |
|---|---|
| ![Plugin Store](./docs/images/store-en.png) | ![Logs](./docs/images/logs-en.png) |

**Other languages:** [中文界面](./docs/images/overview.png) · [日本語](./docs/images/overview-ja.png) · [한국어](./docs/images/overview-ko.png) · [Русский](./docs/images/overview-ru.png)

## Why this launcher

Setting up dsh normally means installing Node.js, running npm commands, dealing with registries; updating means digging out commands; plugins mean manual `git pull`. **Too much hassle.**

This launcher packs all of it into **one exe**:

```
double-click → splash → click "Install" → grab a coffee → click "Start" → browser opens
```

| You are | Your pain | Our answer |
|---|---|---|
| 🐣 Beginner | No Node.js, afraid of the CLI | One-click install: Node.js auto-downloaded (official → China mirror fallback), dsh installed via npm (same fallback) |
| 🚀 Daily user | Open / update / plugins are annoying | A context-aware big button + one-click plugin maintenance |
| 🔧 Power user | Version chaos, broken deps | 3-way version board (launcher / dsh / plugins) + logs + dependency repair |

## Features

- 🎨 **Modern WPF UI** — GPU-composited rendering, PerMonitorV2 DPI-aware, dark/light dual themes with instant hot-switching, rounded cards with subtle gradients & glow, fluent page transitions, slim adaptive scrollbars
- 🎯 **One-click Install** — auto-detect environment → install Node.js if missing (mirror fallback, **custom install path supported**, persisted into user PATH) → npm install dsh (mirror fallback)
- ▶️ **One-click Start** — the big button morphs by state: *Install* / *Start* / *Open Browser* + stop / restart; a resident service-state watcher keeps the UI always in sync with the real port status
- 🔄 **3-way Update Strategy** — launcher (GitHub `version.txt`), dsh (npm), plugins (git) all show **current / latest** with animated checking indicator; fetched automatically on launch and every 3 hours; state-aware buttons (`✓ already latest` disabled) refresh right after updates
- 🧩 **Plugin Manager** — install via **git URL** or **npm package name**; per-plugin smart update (only pulls when the remote truly has new commits — no false "failed" on up-to-date repos), remove, enable/disable, one-click maintain (update all + fix deps)
- 🛍️ **Plugin Store** — standalone store window aggregating **GitHub multi-keyword search + npm official registry + awesome lists**: stars / language / last-update per card, fuzzy search, sort by stars or name, language filter, installed plugins auto-marked ✓ (non-clickable), one-click install
- 🌉 **Auto proxy** — detects Clash / v2rayN and friends automatically (config → environment → Windows system proxy → common-port scan) and routes npm / git / updates through it; China-friendly mirror fallbacks for npm & Node.js
- 🌐 **Multi-language UI** — follows the system language by default, manual **zh / en / ja / ko / ru / fr / de / es** switch with flag icons
- 🖱️ **Modern system tray** — WPF floating rounded-card context menu (open launcher / start / stop / restart / browser / store / theme switch / exit), close-to-tray, single instance
- 🧩 **Modern dialogs** — all prompts/confirmations replaced with theme-aware rounded glass dialogs
- 📄 **Log viewer** — terminal-style log page with source switch (launcher.log / dsh.log), real-time filter, copy & clear
- 🛡️ **Robust** — crash.log on unhandled exceptions, actionable error hints, atomic smooth page rendering
- 📐 **Hi-DPI responsive** — runtime DPI scaling; borderless resize & maximize (native WindowChrome)

## Quick start

1. Download `DeepSeekHarness.exe` from [Releases](../../releases) — **no build, no install**
2. Double-click → click **Install** (skip if dsh is already set up)
3. Click **Start** → done

All settings live in the Settings page and persist to `launcher.json` next to the exe.

## Self-update & mirrors

- The launcher checks `version.txt` in this repo and offers a one-click download of new releases
- npm falls back to `https://registry.npmmirror.com` when the official registry fails
- Node.js falls back to `https://npmmirror.com/mirrors/node/`
- Both are configurable in Settings

## Development

WPF source (code-only, no XAML toolchain) in [`wpf/`](./wpf/README.md) — double-click `build.bat` to compile (no Visual Studio needed, ships with .NET Framework 4.8's compiler + GAC WPF assemblies).

```
dsh-launcher/
├─ wpf/
│  ├─ WpfApp.cs           # shell UI: titlebar/sidebar/6 pages/tray/splash/dialogs
│  ├─ Logic.cs            # config / env / proxy / service / plugins / store / updates / lang
│  ├─ StoreWindow.cs      # plugin store window
│  ├─ build.bat           # one-click build (csc + GAC WPF)
│  └─ app.manifest        # PerMonitorV2 Hi-DPI aware
├─ version.txt            # version source for self-update
└─ .github/workflows/     # tag → auto build exe → publish Release
```

**Release flow**: bump the version strings in `WpfApp.cs` → sync `version.txt` → push & tag (`v*`) → CI builds and publishes the exe.

## Contributing

Issues & PRs welcome: feature ideas, translations (the `Lang` dictionary), bug fixes.

## Notes

- Official DeepSeek logo assets are not included in the repo — supply your own same-named files under `wpf/` (`build.bat` builds fine without them)
- Not an official DeepSeek product; just a handy launcher/maintainer for dsh

## License

[MIT](./LICENSE)
