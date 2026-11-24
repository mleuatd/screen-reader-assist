# 連続読書モード 実装設計（2026-11-24）

## 目的と対象

利用者自身の Windows 11 パソコン（WinForms / .NET 10）で、TXT や読めるデジタル文字、画像化された PDF / ブラウザー上の電子資料を日本語で連続朗読する。画像しか読めないビューアでは、OCR → 朗読完了検知 → 利用者が事前に選んだ次ページ操作 → ページ変更確認 → 次の OCR を反復する。

既存の `src/ScreenReaderAssist/` を拡張する。旧 `prototypes/Win11MagnifierBase/` の3画面試作は独立維持し、勝手に置き換えない。**本書は設計であり、ここに記載する機能は未実装・未検証。**

## 調査で確認した現行コード

- `MainForm.cs`: `ReadSelection`, `ReadBitmap`, `ReadCursorAsync`, `_cursorTimer`、通知領域メニューとホットキー。
- `OcrService.cs`: `RecognizeAsync(Bitmap, preferJapanese)`（同梱の高精度ローカル OCR を優先して Windows OCR にフォールバック）、`RecognizeWithRegionFallbackAsync`。
- `SpeechService.cs`: `Speak` は既存の発声を `CancelAll` して次のテキストを送る。標準の `ISpeechEngine` は `SpeakAsync(string)` のみで完了イベントを呼び出し側に公開していない。`WindowsSpeechEngine` には内部 `SpeechSynthesizer.SpeakCompleted` があるがログ記録だけ。**ページ送りを即座に開始するのは不可**。
- `ScreenCapture.Capture(Rectangle)` は `MainForm` ですでに利用中。範囲選択は専用の静止画オーバーレイ。

## データソースの優先順位

1. 利用者が正規に取得した UTF-8 TXT などのローカル文字データ。ページ切り替えは不要で、章・段落単位で音声化する。
2. アクセシブルなアプリから取得できる表示テキスト（Windows UI Automation、許可された PDF の文字層等）。非対応なら自動的に OCR にフォールバック。
3. 画像のみの電子資料を表示しているアプリの**可視部分だけ**をメモリ内でキャプチャし OCR。画面の内部ファイルを解析・取得しない。

国立国会図書館「みなサーチ」の全文テキストデータは TXT ダウンロードが可能なものがある。対象利用者の登録と資料ごとの提供条件に従う。個人向けデジタル化資料送信サービスは本人の端末から閲覧できる資料があるが、**利用規約・アクセス制限・複写条件に従う**。館内専用閲覧端末へのアプリ導入、サービスのセキュリティ回避、保護画像の一括抽出、ログイン自動化を実装しない。

出典:
- https://mina.ndl.go.jp/help/4-3
- https://www.ndl.go.jp/support/send
- https://www.ndl.go.jp/use/digital_transmission_individuals

## 画面／操作設計

通知領域メニュー「連続読書モード」と、独立した大きな操作パネルを追加する。操作パネルは拡大・高コントラスト、キーボード操作と AccessibleName を必須とする。

1. 「資料／画面を選ぶ」: `TXTファイル` / `アプリの画面` を選択。
2. 画面読書の場合: 対象ウィンドウと本文矩形を**最初の1回だけ指定**。ページ切替で書誌情報やツールバーが入り込まないよう本文だけを囲む。拡大率・画面DPIが変わったら再指定。
3. 「読む順序」: 横書き左→右 / 縦書き右→左 / 見開き（右ページ→左ページ）の手動選択を MVP で提供。正確な自動判定は後続。
4. 「次のページ」: UIAで「次のページ」ボタンを選んで InvokePattern 実行（対象UIが対応する場合）、または利用者が明示的に選んだキー（例: Right, PageDown 等）を選択。**サイトごとの実際のキーは推測・固定しない**。設定ウィザードで1回だけ試験し、画面が変わることを確認してから有効化。
5. 開始・一時停止／再開・停止・前ページ・次ページ・現在ページを再読・速度調整。既存 `Alt+Shift+P` と `Alt+Shift+S` を読書中も整合させる。新規の開始ショートカット案は `Ctrl+Alt+Shift+B`、衝突時は無効化する。
6. 画面中央の文字拡大、発声部分の強調、現在ページ・認識状態の大きな文字表示。

## 内部クラスと役割

- `ContinuousReadingController.cs`: 1セッションを管理する状態機械。処理は非同期・キャンセル可能。二重開始禁止。UIスレッド以外で OCR し、画面操作は正しいスレッドに戻す。
- `Readers/IReadablePageSource.cs`: `Task<ReadPageResult> ReadCurrentAsync(CancellationToken)`。ローカルテキストと画面 OCR を差し替え可能。
- `Readers/WindowPageSource.cs`: ターゲット HWND を固定して可視矩形を読み取る。Window の存在、前面/非最小化、矩形の有効範囲、表示倍率/ディスプレイ配置の変化を検査。OCR は `OcrService` を使い、画像は処理後 `Dispose()`。
- `Readers/ReadingOrderNormalizer.cs`: 段組み・縦書き・見開きを処理。`OcrService.RecognizeAsync` は文字列のみ返すため、行・単語の座標を返す別のメタデータ付き API の追加が必要。縦書き自動判定を既成機能と表示しない。
- `Readers/IPageTurner.cs`: `TryTurnNextAsync` と `CanTurnNextAsync`。UI Automation のボタン起動を第一候補。キー送信は利用者の明示選択後にのみ、対象ウィンドウが最前面であることを再確認して `SendInput`。別アプリへ誤送信しない。
- `Readers/PageChangeDetector.cs`: 前後ページの画像特徴と、文字正規化後の短いハッシュを使って切替検知。装飾・カーソル点滅だけの変化を除くため、切替後2回の安定キャプチャを検査する。
- `SpeechService.cs`: `SpeakPageAsync(text, settings, token)` 相当の**正常完了を await できる API** を既存音声エンジンの上に追加する。SAPI `SpeakCompleted` と対象 `Prompt`/要求 ID を対応付け、Cancelled/Error は正常完了に数えない。現行 `Speak` の挙動は維持。
- `MainForm.cs`: 読書パネルの表示、既存ホバー読み上げの一時抑止、読書停止時の元設定復元、通知領域操作、終了時の CancellationTokenSource Dispose。
- `AppSettings` / `PortableIni`: `[連続読書]` の設定を追加し、旧INIを壊さず読み込む。既定は **自動ページ送りOFF**。

## 非同期状態機械

`Idle -> Acquiring -> Narrating -> (PlaybackCompleted) -> Turning -> WaitingForStablePage -> Acquiring`

他に `Paused`, `Stopped`, `Failed`, `Completed` を明示する。停止と一時停止はすべての状態に割り込める。`Cancelled` 発声イベントでは絶対にページを送らない。

画面の OCR が空なら原則停止して「文字を認識できません」と表示。ユーザー設定でのみ「空白ページを最大1回スキップ」を許す。

再生中のホバー自動読み上げ、範囲選択、音声テスト等が同じ `SpeechService` を使ってページ音声を取り消さないよう、読書モードが音声を排他的に所有する。手動停止は最優先。

### 擬似コード

```csharp
while (!ct.IsCancellationRequested && pageCount < maxPages)
{
    RequireTargetVisibleAndUnchanged();
    var page = await source.ReadCurrentAsync(ct);
    if (!page.HasReliableText) return StopWithReason("読取失敗");

    var result = await speech.SpeakPageAsync(
        SplitIntoJapaneseClauses(page.Text), settings, ct);
    if (result != PlaybackResult.Completed) return;

    if (!autoPageTurn) return WaitForUserNext();
    if (!await turner.TryTurnNextAsync(ct)) return FinishOrStop();

    var change = await detector.WaitUntilChangedAndStableAsync(page, ct);
    if (change != PageChangeResult.Changed) return StopWithReason("ページ切替未確認");
    pageCount++;
}
```

段落は目安 200〜500文字ごとに分割し、`。`・改行を優先して切り、停止の応答性を確保。段落末尾まで読み終わった後にだけページ送りへ進む。

## 初期パラメータ（実機調整が必要）

- ページ遷移後 200ms ごとに画面変化を検査し、同じ安定状態を2回確認。最大待機 5 秒で安全停止。
- 最大連続ページ数は初期 100 ページ。利用者が上限変更可能。停止・エラー後に無制限再試行しない。
- OCR 認識 0文字、同一画像のまま、ターゲット消失、別アプリ前面、音声がキャンセルされた場合、**ページ送り禁止**。
- 外部通信、ログイン、テレメトリーなし。本文画像・本文テキストを診断ログに保存しない。
- 「前回の続き」は明示許可後にページ番号・書籍識別用の非可逆ハッシュのみローカル保存。無効時は記録しない。

## 実装の順序

**第1段階（最小構成）**: 既存 SAPI に完了通知 API、ホバー排他、TXT を段落ごとに自動朗読、一時停止・停止を実装。ビルドとロジックテスト。

**第2段階（画像読書）**: 対象ウィンドウ・本文領域の記憶、画面 OCR、ユーザー操作による手動ページ送り、画面変化判定。まだ自動ページ送り OFF。

**第3段階（自動ページ送り）**: ページ送りUIA/キーを手動設定・テストし、朗読完了→画面遷移→安定→OCRのループを有効化。誤作動検出後の停止。

**第4段階（精度改善）**: 縦書き座標補正、右開き見開き、段組み、ルビ、ページ見出し除去、PDF文字層／EPUBの直接読書。読み進めた位置の復元。

## 最低限の自動テスト

- 音声正常完了で1ページだけ送る / キャンセル・一時停止・エラーでは送らない。
- OCR空白・重複ページ・切替タイムアウト・次ページ操作なしで無限ループしない。
- アクティブウィンドウ変更や解像度・DPI変化で誤クリック・誤キー送信しない。
- 読書中ホバー・他の発声要求がページ再生を破壊しない。
- 縦書き／横書き／見開きの日本語人工テストデータで読み順を検査。
- ページ末尾、空白ページ、重複する見出し、画像が切り替わったが同じOCR文言のケースを区別する。
- 中断→再開、終了→再起動、旧設定の読み込み、文字内容がログに残らないこと。
- Win11 x64 / 3画面混合DPI / 日本語SAPI の**実機試験を別途行う**。GitHub Windows CI で GUI 操作・日本語音声・閲覧サイトとの互換性が検証済みとは扱わない。

## 未確定の事項

実際の国立国会図書館デジタルコレクションの「次へ」ボタンのUIA公開状況、キーボードショートカット、画面形状は対象資料・ブラウザーで確認が必要。現状これらに対して特定キーが動作すると断定しない。自動ページ送りの既定を無効に保ち、利用者が対象画面で検証した操作だけ使う。

将来の実装ではビルドに成功しても「Windows実機・日本語読み上げの動作未確認」と明示する。
