# 開発進捗

更新: 2026-10-11

- [x] 要件定義・基本設計
- [x] WinForms / Windows OCR / System.Speech / UIA のソースコード初稿
- [x] 通知領域・選択UI・ショートカットのソース実装
- [x] 漫画モードの白領域抽出アルゴリズム試作
- [x] 画面拡大APIと3回クリックの実験的なソース実装（既定無効）
- [x] 設定サイトHTML/CSS/JS + 設定JSONの入出力
- [x] GitHub ActionsのWindows build / test / Pages / Release定義
- [x] Linux/Node.jsで設定サイトのJSテスト4/4件成功
- [x] XML/HTML/YAML構文検査成功
- [ ] WebブラウザE2Eテスト（環境のURLアクセス制限によりブロック）
- [x] GitHub ActionsのWindows runnerでアプリのコンパイル・ロジックテスト・自己完結型publish成功
- [ ] Windows画面の操作テスト、実機/仮想GUIテスト
- [ ] 漫画OCR精度・縦書き読み順の検証
- [ ] アプリのGUIアクセシビリティ検証
- [x] GitHubリポジトリ新規作成・35ソースファイル反映
- [ ] Pages有効化（所有者操作が必要）
- [x] Windowsビルドアーティファクト生成の実体・サイズ確認
- [x] 実機未検証と明示した`v0.1.0-preview.1`のGitHub Releases公開
- [ ] 音声プラグイン・声質調整実装

新しい公開リポジトリ `mleuatd/screen-reader-assist` にソースコードと4つの常設GitHub Actionsワークフローを反映済み。自動ビルド成功。Pages作成には現在のGitHub Actions権限では足りず、所有者がSettings > PagesからGitHub Actionsを選択する必要がある。

### 2026-10-11 Web体験版
- [x] ブラウザ体験版を追加（`site/demo.html`、`demo.js`、`demo.css`、`demo-logic.mjs`）。
- [x] 模擬範囲選択、漫画吹き出し、3回タップ拡大、Web Speech API読み上げUI。
- [x] ブラウザ非依存のロジック7件と従来の設定4件の合計11件のNodeテストが通過。
- [x] ローカルのheadless Chromiumでスマホ・PC表示と操作イベントを検証。JavaScript例外0件。音声出力の聴感品質は未確認。
- [ ] GitHub Pages有効化・本番サイト配信の確認。

### 2026-10-11 GitHub Pages以外の公開経路を確保
- [x] raw.githack.com で本リポジトリのWeb体験版を公開。追加サーバー・費用・ユーザー側のGitHub Pages設定は不要。
- [x] Actions `38118586141` で8リソースのHTTP 200と非空ファイル・HTMLマーカーを検証（ジョブ成功）。
- [ ] 本家 GitHub Pages URL `mleuatd.github.io/screen-reader-assist/` は別途所有者による初期有効化が必要。

### GitHub Pages実公開準備
- [x] 既設の`dtx-drum-flow` Pagesに`site/screen-reader-assist/`として8つの静的公開リソースを追加（コミット`7140b5c`）。
- [x] ドラム本体のソースコード・挙動は未変更。
- [x] GitHub Pages上の独立URL：https://mleuatd.github.io/dtx-drum-flow/screen-reader-assist/demo.html
- [ ] 新規の試用版更新時にPagesミラーを同期する仕組みは未実装。
- [x] GitHub Pages公開が成功（ドラム側Actions `38119463235`）。独立した公開URL上のHTML・CSS・JavaScriptなど8ファイルをHTTP 200で確認（検証Actions `38119529084` 合格）。

### 2026-10-11 ドラムサイトからの分離
- [x] ドラムリポジトリ `mleuatd/dtx-drum-flow` から一時ミラー8ファイルと検証ワークフローを削除（`b6c7b56`）。
- [x] 音声読み上げのコード正本と独立Web版を `mleuatd/screen-reader-assist` に維持。
- [x] 独立Web体験版のraw.githack.com公開経路はActions `38118586141` で取得確認済み。
- [ ] 個別GitHub Pagesの初期有効化は未対応（現在のGitHub連携権限では不可）。

### 2026-10-11 v0.2 入力操作を変更
- [x] Ctrl+Alt+Rへホットキー変更・MOD_NOREPEAT導入。
- [x] 不透明最前面キャプチャ選択フォームでタッチ/マウスの座標を受け取る。ドラッグ範囲・単発タップ周辺・Esc取消に対応。
- [x] 選択とOCRで同一スナップショットを使用。終了後の再キャプチャによるズレを抑制。
- [x] 旧グローバル3回クリックフックを実行パスから撤去し、設定を無効化。
- [ ] Windows 11実機のマルチモニター/DPI/タッチによる操作と入力非干渉の統合検証。

### v0.2 検証済み・配布
- [x] GitHub Actions Windowsビルド `38122996049` に成功。14件のロジックテストすべて合格。
- [x] GitHub Actions 新プレリリース `38122996060` 成功。`v0.2.0-preview.1` のWindows x64 ZIPを公開。
- [ ] タッチディスプレイでの入力独占・OCR・音声の実機確認は未実施。

### 2026-10-11 v0.3カーソル位置読み取り
- [x] 前面フォームで850msのカーソル静止イベントを検知、近傍OCR行を読み上げる機能を追加。
- [x] クリックは最寄りOCR行、ドラッグは選択範囲全体を読む。Hで自動読み上げの切替。
- [x] OCR結果の鮮度確認と連続同文の重複読み上げ防止を追加。
- [x] Web体験版にもYouTube風タイトルとカーソル静止による模擬読み上げを追加。
- [ ] Windows 11のYouTube実画面、タッチ、フォーカス、OCR精度は実機未検証。

### v0.3配布済み
- [x] GitHub Actions Windows build `38127109047` 成功、16件のロジックテストPASS。
- [x] GitHub Actions試用版配布 `38127109058` 成功。`v0.3.0-preview.1` のZIPを確認（74,556,618バイト）。
- [x] Web設定・体験版JSのテスト `38127109071` 成功。

### v0.4 自動カーソル・モード選択（2026-10-11）
- [x] 標準カーソルモードを追加。Windows UI Automation→必要時OCRで読み上げ。
- [x] 漫画OCRカーソル／範囲選択のみ／自動読み上げ停止を追加。
- [x] 常駐アプリの設定画面および通知領域メニューでモード変更・保存。
- [x] Webの設定JSONにモード、静止待ち時間、OCR補完を追加（旧v1形式とも互換）。
- [ ] 実際のYouTube、Chrome、ゲーム、漫画でUIAの可用性とOCR精度を実機で確認。

- [x] UIAだけで取りこぼすアプリに備え、MSAAアクセシビリティ名の取得フォールバックを追加。
