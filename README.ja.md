# dsh-launcher

> DeepSeek Harness (dsh) のらくらくランチャー：**ダブルクリックで起動、ワンクリックでインストール・更新・メンテナンス**。Node.js・dsh・プラグインすべてお任せ、中国ミラー自動フォールバック付き。

[English](README.md) | [中文](README.zh.md) | [日本語](README.ja.md) | [한국어](README.ko.md) | [Русский](README.ru.md)

[![dsh-launcher](https://img.shields.io/badge/dsh--launcher-%E2%9C%93-4D6BFE?style=flat-square)](https://github.com/topics/dsh-launcher)
[![Windows](https://img.shields.io/badge/Windows-10%2B-blue?style=flat-square&logo=windows)](../../releases)
[![License](https://img.shields.io/badge/License-MIT-green?style=flat-square)](./LICENSE)

## スクリーンショット

| 概要 (Overview) |
|---|
| ![概要](./docs/images/overview-ja.png) |

## なぜこのランチャーか

dsh をセットアップするには Node.js のインストール、npm コマンド、レジストリ設定が必要。更新もプラグイン管理も手間がかかります。**このランチャーは dsh の管理を GUI にまとめた初心者向けツールです：**

- 🎯 **環境検出 + ワンクリックインストール**：Node.js が無くても自動インストール（カスタム先対応）、dsh もワンクリック
- 🔄 **ワンクリックメンテナンス**：dsh の更新・全プラグイン更新・依存修復を一括実行
- 🧩 **ビジュアルなプラグイン管理**：ストアから選択してインストール、依存を自動修復、壊れたプラグインは自動隔離
- 🖥️ **すぐ使える**：ダブルクリック → 「起動」→ ブラウザが開く。コマンドライン不要

## 主な機能

- 🎨 **モダン WPF UI** — GPU 合成レンダリング、PerMonitorV2 高 DPI 対応、ダーク/ライト双テーマ即時切替、角丸カード・微光・なめらかなアニメーション
- 🎯 **ワンクリックインストール** — 環境自動検出 → Node.js 自動ダウンロード（公式→中国ミラー）、**カスタムインストール先対応**（PATH に永続化）
- ▶️ **ワンクリック起動** — 状態に応じてボタンが変化（インストール/起動/ブラウザを開く + 停止/再起動）、常駐モニターで UI と実状態を常時同期
- 🔄 **3方向アップデート** — ランチャー（GitHub version.txt）/ dsh（npm）/ プラグイン（git）を現在・最新で表示、スピナー付き確認アニメーション
- 🧩 **プラグイン管理** — git URL / npm パッケージ名でインストール、スマート更新（リモートに新コミットがある時のみ pull、最新なら誤って失敗表示しない）、ワンクリックメンテナンス
- 🛍️ **プラグインストア** — GitHub 複数キーワード + npm 公式 + Awesome リストを集約、星/言語/更新日表示、インストール済みは ✓ 表示
- 🌉 **自動プロキシ** — Clash / v2rayN 等を自動検出（設定→環境変数→システムプロキシ→ポートスキャン）
- 🌐 **多言語対応** — 中文 / English / 日本語 / 한국어 / Русский / Français / Deutsch / Español（国旗アイコンで切替）
- 🖱️ **モダントレイメニュー** — WPF フローティングカードメニュー、閉じるとトレイへ、単一インスタンス
- 📄 **ログビューア** — ターミナル風ログ、フィルタ・コピー・クリア対応

## クイックスタート

1. [Releases](../../releases) から `DeepSeekHarness.exe` をダウンロード（ビルド不要・インストール不要）
2. ダブルクリック → **インストール**（既に dsh がある場合はスキップ）
3. **起動** → 完了

設定は「設定」ページで管理され、exe と同じフォルダの `launcher.json` に保存されます。

## ライセンス

[MIT](./LICENSE)
