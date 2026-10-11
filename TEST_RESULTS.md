# テスト結果 / 2026-10-11

| テスト | 結果 | 証拠 |
|---|---|---|
| Web設定 JSON defaults roundtrip | PASS | `node --test site/settings.test.mjs` |
| Web設定 異常値 clamp | PASS | 同上 |
| Web設定 未対応version拒否 | PASS | 同上 |
| Web設定 長い音声名を制限 | PASS | 同上 |
| Windowsアプリ C# ビルド | PASS | Actions `38116579079` Windows runner のビルド成功 |
| Windows 設定・漫画ロジック試験 | PASS | Actions `38116579079` `dotnet run` 成功、詳細個別件数は要ログ追跡 |
| Windows OCR（横書き） | NOT RUN | 実画像でのテスト未実施 |
| Windows OCR（縦書き） | NOT RUN | 実画像でのテスト未実施 |
| キーボードのみで範囲指定 | NOT RUN | Windows GUI環境なし |
| 常駐・ホットキー動作 | NOT RUN | Windows GUI環境なし |
| 音声再生・音声切替 | NOT RUN | Windowsと日本語音声なし |
| 漫画吹き出し認識精度 | NOT RUN | 実漫画での検証未実施 |
| 拡大・トリプルクリックの非干渉 | NOT RUN | Windows GUI環境なし |
| GitHub Actions のWindowsビルド | PASS | `https://github.com/mleuatd/screen-reader-assist/actions/runs/38116579079` |
| ChromiumブラウザでのWeb実操作 | BLOCKED | Playwrightは起動したが、この環境ではローカルURLへのアクセスが管理者制限で拒否された |
| GitHub Pages公開 | BLOCKED | 初期有効化未完了、Actions 403 |

Windows向け自動テストは`tests/ScreenReaderAssist.Tests/`に存在するが、未実行のため合格とは扱わない。

- 2026-10-11: `38116579079` import / web-test / windows-build の3ジョブがすべて成功。自己完結型Windows ZIPアーティファクトの存在とサイズ74,134,083バイトをAPIで確認。実機画面・音声出力を検証したわけではない。

| Windowsプレリリース公開 | PASS | `v0.1.0-preview.1` GitHub Releasesに74,555,912バイトの配布ZIPを公開確認、Actions `38116822637` 成功 |

### 2026-10-11 ブラウザ体験版
- Node v22 `node --test *.test.mjs`: **11 passed / 0 failed**（既存設定4、デモロジック7）。
- Python Playwright + Chromium headless（HTML/CSS/JSをインライン化して評価、ローカルHTTPへのネットワークアクセス制限のため）: desktop 1280px / mobile 390px で漫画タップ・モード切替・範囲ドラッグ・3クリック拡大・フォーム入力、JavaScript例外0。ページ配信環境の統合試験は未実施。
- Web Speech API: `speechSynthesis.speak()`呼び出しと状態表示を検証。実音声の聴感品質は検証していない。
- 画像OCR、実漫画の自動吹き出し抽出、PC全体の拡大: Webデモでは**未実装（模擬表示）**。

### 2026-10-11 代替公開先HTTPスモークテスト
- [x] GitHub Actions `38118586141`: raw.githack.com 経由で demo.html / demo.css / style.css / demo.js / demo-logic.mjs / index.html / app.js / settings.js のHTTP 200と非空・HTMLマーカー確認すべて成功。
- [ ] 読み上げの実音声、実端末の3回タップ、外部プロキシのブラウザ確認画面からの実遷移は環境未検証。

### ドラム側のGitHub Pagesに独立プレビューデモを作成
- 既存 Pages の配下に8つの静的ファイルを追加。専用リポジトリ内の更新は自動的には反映されない。
- 公開導線 https://mleuatd.github.io/dtx-drum-flow/screen-reader-assist/demo.html
- Pages専用検証: `mleuatd/dtx-drum-flow/.github/workflows/verify-screen-reader-pages.yml` にHTTPテストを追加。公開実URL確認は実行結果を見て記録する。
