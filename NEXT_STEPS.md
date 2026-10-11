# v0.6 ポータブルINI / ZIP再編集
1. 正本はGitHub main。INI・ZIPサポートを追加、従来の読み上げモードは維持。
2. Windows CI `38153717247` 成功、53件のINIなどのロジックテスト成功。ZIP前のEXE/INI同階層確認成功。
3. Web CI `38153717226` 成功、26件のINI/JSZipテスト成功。
4. GitHub Releases `v0.6.0-preview.1` 公開成功（Workflow `38153717250`、ZIP 74,582,881バイト）。Web設定画面のURLはraw.githack.comで公開。
5. Windows実機でsettings.iniのUTF-8・日本語音声名・起動読込・設定保存・再起動反映を確認。
6. Webが公式GitHub ZIPを直接取得できない場合は公式ZIPを手動ダウンロードし、ドラッグ＆ドロップしてから設定済みZIPを作成する。
7. 公式GitHub Pagesの初期有効化は未完了。ブラウザ体験版と設定ページはraw.githack.com経由。

## v0.6.1 設定ファイルの日本語化
- Windows CIとWeb Nodeテストの結果を確認する。失敗した場合は修正する。
- `v0.6.1-preview.1` を公開し、更新されたEXEと日本語INIを同梱したZIPを検証する。
- 現行EXEが起動時にINIを読み設定保存できることはWindows実機で別途検証が必要。旧EXEに新INIをコピーしないこと。

- Actions `38155872150` Windows CI成功（62件）、`38155975477` Web 33件合格、`38155975414` v0.6.1-preview.1 Release成功。
- ユーザー実機では最新v0.6.1-preview.1のEXEとsettings.iniをセットで展開・起動すること。

## v0.6.2 常駐アプリ
- 起動時は設定ウィンドウ非表示、通知領域で自動カーソル読み上げを継続。設定表示・非表示、右クリックメニュー、読み上げモード、完全終了を検証する。
- CIとリリースの成否を確認して`v0.6.2-preview.1` ZIPの公開を確認する。Windows実機で通知アイコンと自動読み上げの共存を検証する。
