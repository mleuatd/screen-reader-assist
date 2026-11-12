# C#／.NET 10 LTS移行と読み上げ速度・安定性監査（2026-11-12）

## 今回の変更（移行候補、まだ正式採用前）
- Windows Formsアプリとテストを `net10.0-windows10.0.19041.0` へ更新。
- `System.Speech` を 10.0.15 へ更新。
- Windowsビルド／Release用GitHub ActionsのSDKを10.0.xに変更。
- 読み上げ要求から音声エンジンの非同期受付までの時間を記録。**実際に音が出始めるまでの時間ではない**。
- OCRが250ms以上かかった場合だけ、エンジン初期化・画像準備・文字認識それぞれの処理時間（ms）を記録する。**OCRテキスト・画面画像・マウス座標は記録しない**。
- 低負荷時に全OCR結果を記録するとログ出力自体が負担になるため、250ms未満の細分化ログは省く。

## 監査対象（必ずWindows 11実機で評価）
1. **文字認識**：範囲選択→画面キャプチャ→画像準備→OCR→発声受付の各時間を10回以上測る。日本語の短文・長文・画像内文字を区別する。
2. **音声**：発声受付までと、実際の音が出始めるまで（スピーカー出力）は別計測。音が出始めるまでの時間は現時点で**未測定**。
3. **安定性**：1/2/3画面・モニター抜き差し・異なるDPI・スナップ変更・長時間稼働・強制終了・日本語音声なし・日本語OCRなし。
4. **反応速度**：測定値の平均値だけでなく中央値（p50）・95パーセンタイル（p95）・最悪値を確認。連続して読ませても古い読み上げが残らないかを確認。
5. **非干渉**：Alt＋クリックを使う既存アプリ、クリック・ドラッグ・マウスカーソル座標・Windows標準拡大鏡との競合を確認。

## 公開サンプルの比較候補（現時点では転用なし）
- Microsoft [Windows-classic-samples / Magnification](https://github.com/microsoft/Windows-classic-samples/tree/main/Samples/Magnification)（MIT）。窓型拡大と全画面拡大のAPI使用方法の参考。
- [robmikh/Win32CaptureSample](https://github.com/robmikh/Win32CaptureSample)（MIT）。Windows.Graphics.CaptureのWin32/C++サンプル。画面取得の低遅延化を検討する際の参考。
- Microsoft [WindowsAppSDK-Samples](https://github.com/microsoft/WindowsAppSDK-Samples)（ライセンスや個別例の条件を確認）。文字抽出APIの候補調査。

**禁止**：出所やライセンスを確認せずコードを複製すること、測定せずに既存のOCR・拡大実装を丸ごと交換すること。
**現状**：既存のC#実装（Windows.Media.Ocr＋System.Speech）を維持し、計測情報を追加した段階。新しいキャプチャ実装へは差し替えていない。

## 合格条件と安全な切替
- Windows CIのビルド、ロジックテスト、配布ZIP生成が成功する。
- 実機で日本語OCR・日本語音声・3画面拡大のGUI動作を確認する。
- 変更前後で同条件で速度・CPU・メモリを測り、結果が悪化する変更は取り消せること。
- レガシープレビュー v0.6.5-preview.1 と今回の.NET 10候補を混同しない。
- 高速化のために画像内容、認識した文章、認証情報を診断ログへ保存しない。

## 参考
- Microsoft .NETサポート方針： https://dotnet.microsoft.com/ja-jp/platform/support/policy
- NuGet System.Speech 10.0.15： https://www.nuget.org/packages/System.Speech/10.0.15
