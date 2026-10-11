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
