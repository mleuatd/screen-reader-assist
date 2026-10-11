# 未解決課題（重大度順）

- **RESOLVED-01 GitHub repo作成・ソース投入**: `mleuatd/screen-reader-assist` を作成し35ファイル投入済み。
- **RESOLVED-02 Windowsビルド**: GitHub Actions `38116579079` でC#ビルド・ロジックテスト・publish・成果物アップロード成功。GUI操作は未検証。
- **HIGH-03 OCRモデル**: WindowsのOCR言語インストール・最大寸法・縦書き文の読み順を検証する。`Manga OCR`など適切なライセンスのローカル認識エンジンは未接続。
- **HIGH-04 UIA対応**: フォーカス中の文字取得のみで、選択した任意範囲のUIA走査は未実装。
- **HIGH-05 3回クリック拡大**: マウス入力の衝突とマルチモニター/DPI/UAC/拡大中の入力変換を実機検証。標準状態では無効。
- **HIGH-06 音声合成拡張**: 声質・ピッチの調整や選択肢の拡張未実装。ライセンス・許諾を含めて調査。
- **MED-07 設定互換**: `reuseLastRegion`予約フィールドを実装する。
- **MED-08 エラー UX**: アプリ起動直後に音声の読み上げが聞こえない場合、設定確認支援が必要。
- **MED-09 実キャプチャのテスト**: ゲーム・漫画・複数ディスプレイでユーザー協力によるテストを行う。

- **MED-10 ブラウザE2Eテスト**: Playwright/Chromiumが実際のWebページへの`file://`と`http://127.0.0.1`遷移を管理者ポリシーにより拒否したため、ブラウザ実操作は未検証。GitHub Pages公開後に再テスト。

- **BLOCKER-11 GitHub Pages初期有効化**: GitHub Actionsの`actions/configure-pages@v5`で404→自動作成は403 `Resource not accessible by integration`。リポジトリ所有者がSettings > Pages > Build and deployment > SourceをGitHub Actionsに設定する必要がある。

- **RESOLVED-12 体験版をWebで開けない問題（代替経路）**: 専用GitHub Pagesとは別に、raw.githack.com がGitHubリポジトリからHTMLを配信する経路を確保。HTTP・リソース検証済み。初回の確認画面と第三者サービスへの依存は残る。

- **OPEN-13 専用GitHub Pages初期設定**：GitHub標準の `https://mleuatd.github.io/screen-reader-assist/` は未公開。リポジトリ設定の管理操作が必要で現在のGitHub連携では不可能。独立したraw.githack.com経由の無料公開は確認済み。ドラム側の間借りは廃止した。

- **OPEN-14 v0.2タッチ入力排他実機検証**：Ctrl+Alt+Rで最前面の選択フォームにアクティブを移し、そこでのみタップ/ドラッグ座標を取得する実装は完了。Windows 11タッチPC上で下層アプリにタッチ操作が伝わらないことやDPI、UAC、ゲームフルスクリーンとの兼ね合いは未検証。
