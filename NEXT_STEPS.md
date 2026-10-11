# v0.6 ポータブルINI / ZIP再編集
1. 正本はGitHub main。INI・ZIPサポートを追加、従来の読み上げモードは維持。
2. Windows CIでINI parserのテスト、アプリをビルドし、ZIPのEXE/INI同階層を確認。
3. NodeでWebのINI/JSZipテストを通す。zipの取り込み・変更・再ZIP化を検証。
4. GitHub Releasesにv0.6.0-preview.1を公開し、Webページの直接ダウンロード導線を確認。
5. Windows実機でsettings.iniのUTF-8・日本語音声名・起動読込・設定保存・再起動反映を確認。
6. Webが公式GitHub ZIPを直接取得できない場合は公式ZIPを手動ダウンロードし、ドラッグ＆ドロップしてから設定済みZIPを作成する。
7. 公式GitHub Pagesの初期有効化は未完了。ブラウザ体験版と設定ページはraw.githack.com経由。
