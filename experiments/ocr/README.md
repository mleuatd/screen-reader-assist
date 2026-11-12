# Googleレンズ級の日本語OCRを目指す比較実験

## なぜGoogleのコードをコピーしないか
Googleレンズの認識エンジンは、公開ソースを自由に複製してWindowsソフトに組み込めるものではない。Google ML Kitの公式日本語OCR SDKはAndroid/iOS向けで、WindowsのC#にそのまま流用できない。Cloud Visionはクラウドに画像を送信するAPIで有料利用が発生し得る。ユーザーのゲーム画面や業務画面を自動送信しない。

## 比較候補
- **PaddleOCR v3.7 / PP-OCRv6 small・medium**（Apache-2.0）。日本語対応のモデルをWindows上でローカル実行。
- **現行Windows.Media.Ocr**。ライセンス追加不要、標準OCR。旧動作を残す。
- 精度比較が必要なら、別途Tesseract日本語モデル等も検討。

## 今回作成した実験
- `experiments/ocr/evaluate_paddleocr_japanese.py`
- `.github/workflows/experiment-paddleocr-japanese.yml`

Windows環境のPython/ONNX Runtimeで、オープンソースのPP-OCRv6 small/mediumを個別起動。日本語2行を含むゲーム風の**合成画面**を3条件生成し、原文一致・文字誤り率・初回処理時間・2回目以降の中央値・初期化時間を測る。実際のユーザー画像はGitHubへ登録しない。

## 採用条件
1. 日本語の認識精度が現在のWindows OCRより同等以上。
2. 初回／連続使用時の発声開始遅延が改善する（OCR単体の速度と、音声開始の時間は区別）。
3. 利用者が操作する画面をクラウドへ自動送信しない。
4. ソフトに必要なモデル・依存関係の容量／メモリが無理なく、Windows 11で安定する。
5. 既存操作・認識結果・3画面拡大・回復処理の回帰テストに合格する。
6. オープンソースのライセンス、モデル再配布条件を確認する。

## 現状
**評価実験であり、メインアプリにはPaddleOCRを組み込んでいない。** Googleレンズと同一の認識精度を保証するものではない。合成画像で勝っただけでは実ゲーム画面の精度証明にならない。Tesseract実測で以前のゲーム画像の日本語2行が正確になった前処理方式も比較対象とする。

資料：
- https://github.com/PaddlePaddle/PaddleOCR
- https://www.paddleocr.ai/main/en/version3.x/pipeline_usage/OCR.html
- https://developers.google.com/ml-kit/vision/text-recognition/v2/android?hl=ja

## .NET 10本体へ統合する際のC#候補
- [meloht/RapidOCRSharpOnnx](https://github.com/meloht/RapidOCRSharpOnnx) v1.4.0：NuGet提供、Apache-2.0、.NET 10対応、PP-OCRv6の日本語モデルをONNX Runtimeで動かせるC#実装。Pythonの常駐を避けられる可能性がある。
- ただし、このパッケージのモデルファイル・OpenCV・ONNX実行環境を含めた容量、CPU/RAM、Windows 11上の初期化と連続認識速度は未測定。NuGetから入るコードと、モデルの利用規約はそれぞれ確認する。
- 本体は当面Windows標準OCRの動作を維持。別実装を採用するのは、同じ日本語画像での精度と反応時間がよくなり、Alt＋ドラッグ・読み上げ・復旧動作が維持できると確認したときに限る。

## 読み上げまでの二段階処理
1. モデルは可能ならアプリ起動時に非同期で読み込んでおき、ドラッグ終了時に初回起動待ちが発生しないようにする。
2. 選択領域だけ切り出し、認識できた最初の日本語文章を速やかに読み上げる。
3. 誤字の疑いがある場合だけ、別モデル・画質補正をバックグラウンドで試す。原文が不確かなまま勝手な文章へ書き換えない。
4. 精度とレイテンシを実測し、遅い上位モデルを最初から必須にしない。

この処理方針は現段階では設計案であり、まだメインアプリの動作変更ではない。
