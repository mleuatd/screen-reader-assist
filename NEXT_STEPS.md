# v0.4.1 画像の文字らしい領域を探索するOCRフォールバック
1. GitHub mainに `TextRegionDetector.cs` と `OcrService.RecognizeWithRegionFallbackAsync` が追加済み。
2. Windows CI `38128877682` 成功。26件のロジックテストのPASSを確認（`38128757682`）。
3. `v0.4.1-preview.1` はReleasesへ公開済み。Workflow `38128877675` 成功、ZIP 74,565,370バイト。
4. カーソル読み上げ、漫画OCR、手動範囲選択で、通常OCR→候補範囲の切り抜き・補正→再OCRの順序が働くか実機テストする。
5. 極小文字、縦書き、低コントラスト文字、複雑な背景でのOCR認識率を測定。誤読を捏造なしと区別する。
6. UIA/MSAAのデジタル文字を優先し、OCR補完OFFなら画像処理を動かさない仕様を維持。
7. ページ全体の漫画の吹き出し読む順番の自動推定は未実装。GitHub Pagesの初期有効化は未完了。
