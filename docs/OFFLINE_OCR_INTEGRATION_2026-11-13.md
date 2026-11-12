# オフライン高精度日本語OCR：本体統合仕様（2026-11-13）

## 目的
Googleレンズで確認できた日本語読み取り精度を、Windows 11 のC#・.NET 10 アプリで**通信ゼロ**のまま目指す。Googleの非公開認識エンジンやクラウドAPIは使用しない。

## 実装（試作・Windows CIで検証する機能）
- `src/ScreenReaderAssist/OfflinePaddleOcr.cs`：PP-OCRv6 small (日本語含む) のONNXを、`models/ppocrv6_det.onnx`／`models/ppocrv6_rec.onnx`から直接ロード。
- C#の`RapidOCRSharpOnnx` + ONNX Runtime CPU + OpenCvSharp5を使用。Pythonを配布・起動しない。
- OCRモデルの読み込みはアプリ起動時に別スレッドで準備し、クリックや画面描画を停止させない。
- 画像の矩形選択→ローカル高精度OCR→既存の`System.Speech`日本語音声、という順序で実行。
- 既存の`OcrService.RecognizeAsync`および全文選択向け`RecognizeWithRegionFallbackAsync(...,manga:true)`で新モデルを優先。カーソル付近で**特定の1行だけ**を読む処理は、座標に合った文字を返す既存のWindows OCRを維持。
- 新モデルが空結果、モデル欠落、またはネイティブ例外で失敗した場合は、ローカルWindows OCRに戻る。
- 設定画面・設定INIの`高精度ローカルOCRを使用`で無効化可能。既定は有効。旧INIとの互換維持。
- 読み上げ前に不確実な文章をAIで推測して改変しない。最初の文字認識結果を優先。

## 配布物をオフラインにする仕組み
`scripts/Bundle-OfflineJapaneseModels.ps1`は**GitHub Actionsなどの開発環境でのみ使用**。PaddlePaddle公式配布先からONNXを取得し、`publish/models/`に決まった名前で同梱する。ライセンスは`publish/licenses/`に同梱。モデルのSHA-256は`models/README.txt`に記録。

エンドユーザーへ配る`ScreenReaderAssist-win-x64.zip`にモデルとONNX Runtimeの依存DLLを含める。ユーザーのPCでモデルをダウンロードする機能は存在しない。

**Google・PaddlePaddle・GitHub・LAN・localhostなどへのネットワーク送信、更新チェック、オンライン認証は禁止。**

## 自動テスト
- WindowsのC#アプリとロジックテストをビルド。
- `scripts/Verify-OfflineRuntime.ps1`で製品コード中の通信API・HTTP URL・テレメトリーを検査。
- Windows CIで日本語ONNXモデルを`publish/models`に同梱。
- `--paddle-ja publish/models`で日本語の短い2行を4種類の表示条件（緑色枠・半透明のゲーム風背景を含む）で全文一致検証。
- 実際の`OcrService`経由でも同じ文章が認識できるか検証。
- モデルが存在しない状態でも自動ダウンロードせずWindows OCRへフォールバックできることを確認。
- Windows用のZIPと緊急画面復旧スクリプトを生成。

## 実機で未検証の範囲
- ユーザー添付のゲーム画面を**Windowsアプリの高精度OCR**で実際に取り込んだ結果。CIの合成画像と区別する。
- Alt＋右ドラッグの独立拡大鏡試作と、.NET10本体での一体化。現時点では別実装。メインアプリでは`Ctrl+Alt+R`から範囲選択。
- 3画面・モニターDPI・排他的フルスクリーン・通常クリックとの非干渉。
- ドラッグを離してからスピーカーの日本語音声が始まるまでの実測時間。
- Windows 11実機でのプロセス通信監視とネットワークアダプター無効での動作試験。

**CIのOCR時間＝音声開始までの時間ではない。** 実機の精度・速度・通信ゼロが未検証なら、そのまま未検証と表示する。

## 第三者ソフトウェア
PaddleOCRとRapidOCRSharpOnnxはApache 2.0。再配布時にライセンスを同梱する。OpenCvSharp5 / ONNX Runtime / Microsoft製コンポーネントはそれぞれのライセンスを確認し、NuGetの依存関係とモデルの容量を監査する。

参照資料：`docs/OFFLINE_RUNTIME_POLICY.md`、`docs/JAPANESE_TEST_POLICY.md`
