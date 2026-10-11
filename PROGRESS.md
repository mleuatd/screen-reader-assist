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
- [ ] Releases公開
- [ ] 音声プラグイン・声質調整実装

新しい公開リポジトリ `mleuatd/screen-reader-assist` にソースコードと4つの常設GitHub Actionsワークフローを反映済み。自動ビルド成功。Pages作成には現在のGitHub Actions権限では足りず、所有者がSettings > PagesからGitHub Actionsを選択する必要がある。
