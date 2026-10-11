# v0.6 ポータブルINI / ZIP再編集
1. 正本はGitHub main。INI・ZIPサポートを追加、従来の読み上げモードは維持。
2. Windows CI `38153717247` 成功、53件のINIなどのロジックテスト成功。ZIP前のEXE/INI同階層確認成功。
3. Web CI `38153717226` 成功、26件のINI/JSZipテスト成功。
4. GitHub Releases `v0.6.0-preview.1` 公開成功（Workflow `38153717250`、ZIP 74,582,881バイト）。Web設定画面のURLはraw.githack.comで公開。
5. Windows実機でsettings.iniのUTF-8・日本語音声名・起動読込・設定保存・再起動反映を確認。
6. Webが公式GitHub ZIPを直接取得できない場合は公式ZIPを手動ダウンロードし、ドラッグ＆ドロップしてから設定済みZIPを作成する。
7. 公式GitHub Pagesの初期有効化は未完了。ブラウザ体験版と設定ページはraw.githack.com経由。
