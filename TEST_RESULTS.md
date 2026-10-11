# テスト結果 / 2026-10-11

| テスト | 結果 | 証拠 |
|---|---|---|
| Web設定 JSON defaults roundtrip | PASS | `node --test site/settings.test.mjs` |
| Web設定 異常値 clamp | PASS | 同上 |
| Web設定 未対応version拒否 | PASS | 同上 |
| Web設定 長い音声名を制限 | PASS | 同上 |
| Windowsアプリ C# ビルド | NOT RUN | このLinux環境にdotnetなし |
| Windows 設定・漫画ロジック試験 | NOT RUN | Windows向けコンソールテストは実装済み |
| Windows OCR（横書き） | NOT RUN | 実画像でのテスト未実施 |
| Windows OCR（縦書き） | NOT RUN | 実画像でのテスト未実施 |
| キーボードのみで範囲指定 | NOT RUN | Windows GUI環境なし |
| 常駐・ホットキー動作 | NOT RUN | Windows GUI環境なし |
| 音声再生・音声切替 | NOT RUN | Windowsと日本語音声なし |
| 漫画吹き出し認識精度 | NOT RUN | 実漫画での検証未実施 |
| 拡大・トリプルクリックの非干渉 | NOT RUN | Windows GUI環境なし |
| GitHub Actions のWindowsビルド | NOT RUN | 対象リポジトリ未作成 |
| ChromiumブラウザでのWeb実操作 | BLOCKED | Playwrightは起動したが、この環境ではローカルURLへのアクセスが管理者制限で拒否された |
| 公開ページ・ダウンロード | NOT RUN | 対象リポジトリ未作成 |

Windows向け自動テストは`tests/ScreenReaderAssist.Tests/`に存在するが、未実行のため合格とは扱わない。
