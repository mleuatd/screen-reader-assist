## 追加操作なしで閲覧できる代替プレビュー

[ブラウザ体験版を開く](https://raw.githack.com/mleuatd/screen-reader-assist/main/site/demo.html)

[設定Webページを開く](https://raw.githack.com/mleuatd/screen-reader-assist/main/site/index.html)

この公開経路はGitHub Pagesの初期有効化を必要としません。外部プロキシの確認画面が出る場合は、公開先ドメイン・内容を確認してから進んでください。将来的な正式なGitHub Pagesサイトへの移行は未完了です。

# 再開手順（2026-10-11更新）

1. 公開リポジトリ `https://github.com/mleuatd/screen-reader-assist` とActions実行結果を確認。ソース35ファイルは反映済み。
2. Windowsビルド・ロジックテスト・Web設定テストはGitHub Actions `38116579079` で成功。最新mainのActionsも確認する。
3. GitHub Pages 初期有効化だけは所有者操作が必要：Repository **Settings → Pages → Build and deployment → Source: GitHub Actions**。初回サイト作成はActionの権限で403拒否。
4. Web公開成功後、`https://mleuatd.github.io/screen-reader-assist/` の実アクセスと設定JSONの入出力を検証。
5. GitHub Releasesで`v0.1.0-preview.1`のプレリリースを公開済み。正式版ではないことを明示。
6. Windows 11のGUI環境で範囲選択、日本語OCR、日本語音声、クリック拡大、フォーカス取得の統合テスト。ゲーム・漫画の認識品質も測定。
7. 失敗項目を修正してActions再実行。改善の事実と残る課題を本リポジトリに記録。

**注記**: 一回のチャットで無期限のバックグラウンド作業を継続することはできません。毎回このリポジトリの最新mainと作業記録から再開してください。
