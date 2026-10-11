# Screen Reader Assist — Windows 11 視覚障害支援アプリ（試作 v0.3）

**[インストール不要でブラウザ体験（スマホ・PC）](https://raw.githack.com/mleuatd/screen-reader-assist/main/site/demo.html)** | [Web設定画面（GitHub経由）](https://raw.githack.com/mleuatd/screen-reader-assist/main/site/index.html)


画面の任意範囲からWindowsの日本語OCRで文字を取り出し、インストール済みの音声合成エンジンで読み上げる、常駐型オープンソースアプリの**試作ソースコード**です。

> **検証状況（2026-10-11）:** GitHub Actions のWindowsビルド・ロジックテストとWeb設定テストが成功。`win-x64` の配布アーティファクト生成を確認済み。v0.3のWindowsビルドと16件のロジックテストにも成功。Windows GUIでの操作、ゲーム・漫画OCR・タッチ入力などの実機検証は未実施。GitHub Releases に v0.3.0-preview.1 をプレリリース公開済み。GitHub Pagesは初期有効化待ちですが、無料のGitHubファイル表示サービスで公開体験版を利用可能。試作版につき未検証機能に注意。

## 主な機能（実装済みコード／動作未検証）

- 通知領域に常駐。起動時は設定ウィンドウを隠します。
- `Ctrl + Alt + R`: マウスドラッグまたは矢印キーで範囲指定してOCR・読み上げ。
- `Alt + Shift + M`: 吹き出し内部をクリックし、明るい連続領域を推定してOCRする **実験的** 漫画モード。
- `Alt + Shift + U`: フォーカス中のUI Automation要素の文字取得。
- `Alt + Shift + S`: 読み上げ停止。`Alt + Shift + P`: 一時停止・再開。
- `Alt + Shift + Z`: Windows Magnification APIによる一時的な画面拡大切り替え（実験的）。
- 他アプリへの干渉を避けるため、グローバル3回クリック検出を廃止。範囲読み上げでは専用の最前面画面でのみ操作を受け付けます。
- 音声の種類・速度（-10〜10）・音量（0〜100）・倍率を設定画面から変更。
- 設定JSONの入出力、Web設定サイト（オフラインでも静的配信可能）。

## 必要環境

- Windows 11 Home x64
- OSにインストール済みのOCR言語（日本語が推奨）
- Windowsにインストール済みのSAPI対応読み上げ音声（日本語推奨）
- ソースから開発する場合は .NET 8 SDK とインターネット経由の公開NuGet取得（`System.Speech` 8.0.0）

## ビルド

```powershell
dotnet restore src/ScreenReaderAssist/ScreenReaderAssist.csproj
dotnet build src/ScreenReaderAssist/ScreenReaderAssist.csproj -c Release
dotnet run --project tests/ScreenReaderAssist.Tests/ScreenReaderAssist.Tests.csproj -c Release
dotnet publish src/ScreenReaderAssist/ScreenReaderAssist.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
```

`publish/` 以下の成果物を配布します。リポジトリにプッシュするとGitHub Actionsの`windows-build.yml`が自動ビルド・スモークテストを行い、成功した場合にZIPがワークフローのアーティファクトとして添付されます。`v*` タグを作成した際は`release.yml`がGitHub Releasesを作成します（権限設定等が必要）。

## 操作方法

1. アプリを起動。通知領域にアイコンが出る想定です。
2. `Ctrl + Alt + R` を押す。
3. 元画面のスクリーンショットを表示する専用画面が前面になり、その中を指またはマウスでドラッグして離す。1回タップならタップ周辺（最大360×160px）を読む。キーボードでは矢印キーで移動、Shift+矢印キーで範囲を拡大・縮小、Enterで確定、Escでキャンセル。
4. OCR結果が読み上げられる。読み上げ停止は `Alt + Shift + S`。
5. 設定の変更は通知領域アイコンをダブルクリック。

一部のゲーム、保護コンテンツ、UAC画面、排他的フルスクリーンでは画面の取得に失敗する可能性があります。ホットキーも他のアプリと競合する場合があります。

## 設定Webサイト

`site/index.html`を静的配信することで使用可能です。データはブラウザ上で生成され外部送信されません。Webサイトで「設定JSONをダウンロード」し、アプリの「設定を取り込む」から読み込みます。Webサイトからアプリへ自動反映する方式は**未実装**です。

音声速度は`rate: -10〜10`（System.Speech API）であり、倍率ではありません。設定形式の詳細は [`REQUIREMENTS.md`](REQUIREMENTS.md)。

## 開発上の重要な制約

- 漫画の検出アルゴリズムは**白っぽい領域の連結成分推定**で、吹き出し境界を機械学習で検出していません。縦書き漫画OCRの品質も未測定です。失敗した場合は通常の範囲選択を使ってください。
- Windows標準OCRには最大画像サイズや言語の制約があり、画像の縮小処理を実装しています。縦書き読み順の補正は未実装です。
- 拡大中の入力座標を補正する`MagSetInputTransform`は未実装。通常クリックとの競合やマルチモニター時の動作に注意してください。3回クリック機能は既定で無効です。
- 音声モデルの追加や声質合成機能は未実装です。本人の声の許諾のないクローン音声の学習は行いません。
- 文字・画面画像をクラウド送信するコードはありません（アプリのビルド時NuGet取得は別）。
- Windowsの`System.Speech`にはWindowsに対応した音声が必要です。

## コード構成

- `src/ScreenReaderAssist/`: Windows Forms / OCR / UIA / マウス入力 / 拡大 / 音声
- `tests/ScreenReaderAssist.Tests/`: Windows向け設定・簡易吹き出し検出のスモークテスト
- `site/`: GitHub Pages用Web設定画面とNode.jsの単体テスト
- `.github/workflows/`: Windowsビルド・静的サイトテスト・Pagesデプロイ・Release生成
- `*.md`: 要件、設計、進捗、問題一覧、検証、再開手順

## ライセンス

このリポジトリに新規作成したソースコードはMITライセンス（`LICENSE`参照）。Windows SDKと`System.Speech`には各提供元のライセンス・利用規約が適用されます。第三者の漫画画像や声優の音声データは含んでいません。

## 試験版を入手する

- [Windows 11 x64向け試験版 ZIP (v0.3.0-preview.1)](https://github.com/mleuatd/screen-reader-assist/releases/download/v0.3.0-preview.1/ScreenReaderAssist-win-x64.zip)
- **注意**: 実機GUI検証は未実施。初回起動時のSmartScreen警告の可能性あり。Windows標準のOCR言語と読み上げ音声が必要です。

## ブラウザ体験版（アプリのインストール不要）

- [操作体験デモ](https://raw.githack.com/mleuatd/screen-reader-assist/main/site/demo.html) — サンプル画面の範囲読み上げ、漫画の吹き出し読み上げ、3回タップの拡大を試せます。
- 利用者のブラウザの SpeechSynthesis によって実際の音声合成を行います（発声可否は音声環境に依存）。
- この体験版は画像OCRを実行せず、サンプルに紐付く文章を取得する模擬動作です。Windows内の別アプリの画面は読み取れません。
- 漫画の吹き出し検出を自動実行するものではありません。サンプル吹き出しはあらかじめ用意されています。
- GitHub Pagesの初期有効化が必要な場合、Settings → Pages → Build and deployment → Source: GitHub Actions を選んでください。

### Web体験版の公開形態

- **公開済みの体験版**: [https://raw.githack.com/mleuatd/screen-reader-assist/main/site/demo.html](https://raw.githack.com/mleuatd/screen-reader-assist/main/site/demo.html)。アプリ本体は不要です。
- HTML/CSS/JSをGitHubに配置し、raw.githack.comから配信する方式です。外部サービスの確認画面が最初に1回表示される場合があります。
- GitHub Actions `38118586141` で必要な8ファイルのHTTP 200とコンテンツを確認済み。実機音声品質や他のブラウザでの全操作までは保証していません。
- 漫画文字は予め設定した文章のサンプル、拡大は画面の一部のみです。実際のOCRはWindows用アプリが担当します。
- 将来GitHub Pagesの初期有効化が可能になれば、同じソースから専用URLへ公開できます（初期設定のみ未完了）。

### 追加のGitHub Pages公開経路

- ドラム用リポジトリ`mleuatd/dtx-drum-flow`の既設GitHub Pagesに独立の`site/screen-reader-assist/`ディレクトリをミラー。ブラウザ体験版のURL：https://raw.githack.com/mleuatd/screen-reader-assist/main/site/demo.html
- 設定ページのURL：https://raw.githack.com/mleuatd/screen-reader-assist/main/site/index.html
- コードの正本は`mleuatd/screen-reader-assist/site/`。Pagesのミラーは更新時に同期が必要。ドラム本体コードへの変更はない。
- 専用リポジトリのGitHub Pages初回有効化については別の設定が必要。ミラーはこの制約を回避する公開経路。

## 公開サイトの独立管理について（2026-10-11）

- ドラム用GitHub Pagesに一時追加した8ファイルは削除済み（ドラム側コミット`b6c7b56`）。サイトの正本は本リポジトリです。
- [インストール不要の独立ブラウザ体験版](https://raw.githack.com/mleuatd/screen-reader-assist/main/site/demo.html) は無料のraw.githack.comを利用しています。
- GitHub Pages専用URL `mleuatd.github.io/screen-reader-assist/` は**まだ公開されていません**。新しいリポジトリのPages初期有効化が必要ですが、現在のChatGPT GitHub連携にはその操作権限がありません。上記独立URLは動作確認済みの代替経路です。

## v0.2 入力操作変更（Ctrl+Alt+R）

- `Ctrl+Alt+R`の一度の同時押しで専用の全面選択フォームを起動。キーを押し続ける必要はありません。直前の画面を画像として表示し、指・マウス・キーの入力は選択フォームで受け取ります。
- ドラッグして離すと囲んだ座標をOCR。短いタップの場合は周辺（最大360×160ピクセル）をOCR。Escで中止。
- 選択中は通常アプリにクリック操作が伝わらないことを意図した構造ですが、UACセキュアデスクトップ、排他的フルスクリーン、他のTopMostウィンドウ等の特例があります。実機での入力非干渉テストは未完了。
- 以前の設定ファイルの`magnifier.enableTripleClick:true`は読み込み時に無効化。グローバル3連クリックフックは使用しません。
- 対応バージョンは次のプレビューリリース以降です。旧`v0.3.0-preview.1`はホットキーが違います。

## v0.3 カーソルを止めて読み上げ（試作）

- `Ctrl+Alt+R`で元画面の画像を表示する専用の選択モードに入る。ブラウザ・YouTubeのリンクの上をクリックしても、リンク操作は下層へ送らない設計。
- カーソルを約850ミリ秒止めると、周辺画像のOCR結果から位置の近い文字行を読み上げる。
- 短いクリック/タップは近い一行、ドラッグは囲んだ範囲の全文を読み上げてモード終了。
- 読み取りモード中の`H`で自動読み上げON/OFF、`Esc`で終了。モードを終了すれば通常操作へ戻る。
- 移動中に完了した古いOCR結果は破棄し、同じ文章を連続して読み上げない。ホバーだけでクリップボードは書き換えない。
- **制限**: モード開始時の静止画像を読んでおり、実リンク先URLの取得ではない。1行判定なので折り返した長い動画タイトルは途中までになる場合がある。Windows GUI上の安全性は実機未検証。
