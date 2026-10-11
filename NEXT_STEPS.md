# 再開手順

1. GitHubの本人アカウントでpublicリポジトリ`mleuatd/screen-reader-assist`を作成（既存のドラムや株ラボを変更しない）。リポジトリ作成時にREADMEの自動生成を無効にすると初回のプッシュが容易。
2. このZIPを展開し、フォルダ内容を新しいリポジトリへプッシュする。`git init`→`git add .`→`git commit -m "Initial accessible screen reader prototype"`→`git branch -M main`→`git remote add origin ...`→`git push -u origin main`。GitHub連携の権限があればチャットアシスタントも既存repoにコミット可能。
3. Actions > `Build and smoke test Windows app`の実行結果を確認する。初回失敗した場合はジョブログを見てコンパイルエラーを修正し、スモークテスト成功まで再実行。
4. `Website settings tests`が4件合格することを確認。Repository Settings > Pages > Build and deployment > Source にGitHub Actionsを選択して、`Publish website`を起動。
5. **Windows版ビルド成功と利用者の明示的な公開可否確認後に** `v0.1.0` タグでRelease用ワークフローを起動。試作版には必ずExperimental/未検証の注意書きを付ける。
6. Windows 11の試験環境にアーティファクトを展開し、起動・通知領域・グローバルホットキー・日本語OCR/音声・拡大の復元を検証。検証結果を`TEST_RESULTS.md`へ追記。
7. 漫画の吹き出し境界・縦書き認識・音声モデル拡張を段階的に進める。

**重要**: このチャットの作業はバックグラウンドで無制限継続しません。再開時には上記とGitHubの最新コミットを基準に現状確認し、重複実装を避けます。
