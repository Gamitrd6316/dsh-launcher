# dsh-launcher

> The fool-proof launcher for DeepSeek Harness (dsh): **double-click to run, one click to install, update and maintain** — Node.js, dsh and plugins all taken care of, with China mirror fallbacks.

[English](README.md) | [中文](README.zh.md)

[![dsh-launcher](https://img.shields.io/badge/dsh--launcher-%E2%9C%93-4D6BFE?style=flat-square)](https://github.com/topics/dsh-launcher)
[![Windows](https://img.shields.io/badge/Windows-7%2B-blue?style=flat-square&logo=windows)](../../releases)
[![License](https://img.shields.io/badge/License-MIT-green?style=flat-square)](./LICENSE)
[![Releases](https://img.shields.io/github/v/release/loudMore/dsh-launcher?style=flat-square)](../../releases)

## Screenshots

| Overview | Plugins |
|---|---|
| ![Overview](./docs/images/overview.png) | ![Plugins](./docs/images/plugins.png) |

| Updates | Settings |
|---|---|
| ![Updates](./docs/images/updates.png) | ![Settings](./docs/images/settings.png) |

| Plugin Store |
|---|
| ![Plugin Store](./docs/images/store.png) |

## Why this launcher

> 🧪 **WPF 重构版进行中**：GPU 渲染 + 原生动画的下一代版本见 [`wpf/`](./wpf/README.md)（源码已入库，功能与 v1.5.0 等价）。

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

- 🎯 **One-click Install** — auto-detect environment → install Node.js if missing (mirror fallback) → npm install dsh (mirror fallback), with progress guidance
- ▶️ **One-click Start** — the big button morphs by state: *Install* / *Start* / *Open Browser* + stop / restart; browser auto-opens at `http://127.0.0.1:8099`
- 🔄 **3-way Update Strategy** — launcher (GitHub `version.txt`), dsh (npm), plugins (git) all show **current / latest**; fetched automatically on launch and every 3 hours — no manual clicking
- 🧩 **Plugin Manager** — remote URL / branch / update badges per plugin; install via **git URL** or **npm package name**; per-plugin update, remove, one-click maintain (update all + fix deps)
- 🛍️ **Plugin Store** — standalone store window listing GitHub `topic:dsh-plugin` plugins, **paginated up to 500 repos**: stars / language / last-update per card, multi-keyword fuzzy search, sort by stars or name, language filter, installed plugins auto-marked ✓, one-click install and a browse button that opens the repo page; list is pre-fetched at launch, cached locally (official JSON serializer) and auto-refreshed in the background; lazy rendering keeps hundreds of rows smooth, slim custom scrollbar
- 🌉 **Auto proxy** — detects Clash / v2rayN and friends automatically (config → environment → Windows system proxy → common-port scan) and routes npm / git / updates through it; China-friendly mirror fallbacks for npm & Node.js
- 🌐 **Bilingual UI** — follows the system language by default, manual zh/en switch with flags; vector icons across nav & settings
- 🛡️ **Robust** — crash.log on unhandled exceptions, actionable error hints, dark themed dialogs, atomic smooth page rendering (no banding)
- 🖥️ **System tray** — close-to-tray, single instance
- 📐 **Hi-DPI responsive** — runtime DPI scaling; window size/position computed from the actual screen; borderless resize & maximize

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

Single-file source `Launcher.cs` — double-click `build.bat` to compile (no Visual Studio needed, ships with .NET Framework 4.0's compiler).

```
dsh-launcher/
├─ Launcher.cs          # the whole app, one file
├─ build.bat            # one-click build (auto-detects icon assets)
├─ app.manifest         # Hi-DPI aware
├─ version.txt          # version source for self-update
└─ .github/workflows/   # tag → auto build exe → publish Release
```

**Release flow**: bump `LauncherVersion` in `Launcher.cs` → sync `version.txt` → push & tag (`v*`) → CI builds and publishes the exe.

## Contributing

Issues & PRs welcome: feature ideas, translations (the `Lang` dictionary), bug fixes.

## Notes

- Official DeepSeek logo assets are not included — supply your own same-named files (`build.bat` builds fine without them)
- Not an official DeepSeek product; just a handy launcher/maintainer for dsh

## License

[MIT](./LICENSE)
