# v0.2作業再開位置（2026-10-11）

1. 正本: https://github.com/mleuatd/screen-reader-assist の main。
2. Ctrl+Alt+Rを専用読み上げモード起動に変更。タッチ/マウス範囲入力は最前面の SelectionForm、1回タップは最大360×160pxの周辺をOCRする。
3. GitHub Actions Windowsビルド/Logic smoke と Webテスト成功を確認し、失敗時はコードを修正・再実行。
4. v0.2.0-preview.1のGitHub Releasesを新規配布し、既存の v0.1.0-preview.1 と混同しない。
5. README・Webサイトの最新版ZIPリンクを確認。
6. Windows 11タッチPCで、通常アプリへマウス/touch操作が届かないこと、低DPI/高DPI、排他的フルスクリーン、OCR音声を確認する。未検証の場合はそのまま報告。
7. 旧グローバル3回クリックフックは無効。Windows拡大は Alt+Shift+Z の試験操作として維持。
