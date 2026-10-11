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
