# 再開位置：正式GitHub Pages公開経路（2026-10-11）

1. 本体ソースは`mleuatd/screen-reader-assist`。Webデモは`site/demo.html`。
2. 既設ドラムPagesに読み上げ体験用の`site/screen-reader-assist/`を追加した。公開URL：https://mleuatd.github.io/dtx-drum-flow/screen-reader-assist/demo.html
3. 公開は成功済み：`deploy-site` 実行 `38119463235` 成功、`Verify Screen Reader Assist Pages mirror` 実行 `38119529084` 成功。今後の改修後もこの2つで確認する。
4. 新しい`screen-reader-assist`リポジトリ単体のPagesは初期有効化できていない。後に個別ドメイン`mleuatd.github.io/screen-reader-assist/`を使いたい場合、所有者権限でGitHub Pagesを有効にすること。
5. 今後のWeb機能修正時には正本とドラムPagesミラー双方の8静的ファイルを同期する。ドラムの実行プログラムは変更しない。
6. Windows試用版のCIビルドは成功、実際のGUIテスト未実施のため完成版としない。
