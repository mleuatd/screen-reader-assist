# v0.4 複数モード（2026-10-11）
1. 正本は mleuatd/screen-reader-assist。標準はカーソル静止時にUIA→OCR補完。manga/selection/offの設定も実装済み。
2. Windows Actions `38128336948` は成功、22件のロジックテスト合格。
3. Web設定テスト `38128336957` は成功、16件合格。
4. Windowsプレリリース v0.4.0-preview.2 を公開済み。READMEとWeb設定画面は最新版ZIPへリンク済み。
5. 実機でUIA ElementFromPointを検証。文字データの無いリンクや画像の場合のOCR補完と、その認識精度も検証。
6. 漫画はカーソル近傍の吹き出しOCRにとどまる。自動コマ順判定は未実装。
7. GitHub Pages初期有効化は未完了、Web体験版はGitHub-backed外部配信。
