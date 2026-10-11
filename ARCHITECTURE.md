# アーキテクチャ

- UI: WinForms `MainForm` と通知領域 `NotifyIcon`; `RegisterHotKey` で呼び出す。独立した全面選択フォーム `SelectionForm`。
- Capture: `Graphics.CopyFromScreen` で範囲取得。保護画面や特定ゲームでは動作不可。
- OCR: `Windows.Media.Ocr.OcrEngine` と `SoftwareBitmap`。対応言語があれば `ja-JP` 優先。
- UIA: Windows UI Automation COMで `GetFocusedElement()`, `TextPattern` または `CurrentName`。
- TTS: `System.Speech.Synthesis.SpeechSynthesizer`。Windowsに存在するSAPI音声を列挙。
- Manga: `BalloonDetector` が白に近い領域をフラッドフィル。面積や端への接触を判定し、不適切なら固定範囲へフォールバック。
- Magnifier: `Magnification.dll` にP/Invoke。拡大前の変換を退避し、終了時に復元する。初期設定でマウスフック無効。
- Persist: `%LOCALAPPDATA%/ScreenReaderAssist/settings.json`。静的サイトと共通構造の JSON v1。
- Website: 配信時のサーバー処理不要。ブラウザ内でJSONを生成、エクスポート・インポート。通信API不要。
- CI: Windowsビルド/Logic Test, Node設定UIテスト、Pages公開、タグ付きRelease公開。

## 将来
OCRとTTSはインターフェース化して個別エンジンを追加可能にする。現在は`OcrService`と`SpeechService`の具象クラスで実装しているため、最終的なプラグイン方式へのリファクタリングが必要。

## 画面拡大の注意
Windows Magnification APIは拡大後のポインター入力と別個の座標変換の関係がある。`MagSetInputTransform`はUIAccessを要する特殊な要件があり、現時点では未採用。既存のWindows拡大鏡との干渉を避けるため、既に拡大中であれば処理を拒否する。実環境で確認するまでは実験機能として扱う。

## v0.2 入力遮断の仕組み
- `RegisterHotKey(MOD_CONTROL|MOD_ALT|MOD_NOREPEAT, VK_R)`を受けると、アプリが直前画面の画像を一度キャプチャする。
- モーダルの最前面・不透明 `SelectionForm` がその画像を表示。通常のタッチがマウスイベントとして合成される環境では、前面フォームが入力を受け取り下層へ通常のクリックを送らない。
- キー押しっぱなしは不要（押して起動→指だけで選択）。短いタップ時は周辺を選ぶ。
- 選択終了後、同じスクリーンショットの該当矩形を直接OCR。UIAなどのバックグラウンドアプリには範囲入力を注入しない。
- 管理者昇格やセキュアデスクトップ、排他的フルスクリーン、Windows特定タッチジェスチャーでは一律の入力遮断を保証できない。
