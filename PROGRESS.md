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
- [ ] 公開実URLのHTTP 200を確認する検証ワークフローを新設、実行結果を確認する。
