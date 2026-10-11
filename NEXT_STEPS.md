# v0.4 複数モード（2026-10-11）
1. 正本は mleuatd/screen-reader-assist。標準はカーソル静止時にUIA→OCR補完。manga/selection/offの設定も実装済み。
2. Windows Actionsビルドと自動テスト結果を確認する。問題が出たら修正。
3. Web設定画面のNodeテストと旧形式JSONの読み込みを確認。
4. Windowsプレリリース v0.4.0-preview.1 を公開し、WebサイトとREADMEの最新版ZIPリンクを確認。
5. 実機でUIA ElementFromPointを検証。文字データの無いリンクや画像の場合のOCR補完と、その認識精度も検証。
6. 漫画はカーソル近傍の吹き出しOCRにとどまる。自動コマ順判定は未実装。
7. GitHub Pages初期有効化は未完了、Web体験版はGitHub-backed外部配信。
