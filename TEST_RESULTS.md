# テスト結果 / 2026-10-11

| テスト | 結果 | 証拠 |
|---|---|---|
| Web設定 JSON defaults roundtrip | PASS | `node --test site/settings.test.mjs` |
| Web設定 異常値 clamp | PASS | 同上 |
| Web設定 未対応version拒否 | PASS | 同上 |
| Web設定 長い音声名を制限 | PASS | 同上 |
| Windowsアプリ C# ビルド | PASS | Actions `38116579079` Windows runner のビルド成功 |
| Windows 設定・漫画ロジック試験 | PASS | Actions `38116579079` `dotnet run` 成功、詳細個別件数は要ログ追跡 |
| Windows OCR（横書き） | NOT RUN | 実画像でのテスト未実施 |
| Windows OCR（縦書き） | NOT RUN | 実画像でのテスト未実施 |
| キーボードのみで範囲指定 | NOT RUN | Windows GUI環境なし |
| 常駐・ホットキー動作 | NOT RUN | Windows GUI環境なし |
| 音声再生・音声切替 | NOT RUN | Windowsと日本語音声なし |
| 漫画吹き出し認識精度 | NOT RUN | 実漫画での検証未実施 |
| 拡大・トリプルクリックの非干渉 | NOT RUN | Windows GUI環境なし |
| GitHub Actions のWindowsビルド | PASS | `https://github.com/mleuatd/screen-reader-assist/actions/runs/38116579079` |
| ChromiumブラウザでのWeb実操作 | BLOCKED | Playwrightは起動したが、この環境ではローカルURLへのアクセスが管理者制限で拒否された |
| GitHub Pages公開 | BLOCKED | 初期有効化未完了、Actions 403 |

Windows向け自動テストは`tests/ScreenReaderAssist.Tests/`に存在するが、未実行のため合格とは扱わない。

- 2026-10-11: `38116579079` import / web-test / windows-build の3ジョブがすべて成功。自己完結型Windows ZIPアーティファクトの存在とサイズ74,134,083バイトをAPIで確認。実機画面・音声出力を検証したわけではない。

| Windowsプレリリース公開 | PASS | `v0.1.0-preview.1` GitHub Releasesに74,555,912バイトの配布ZIPを公開確認、Actions `38116822637` 成功 |

### 2026-10-11 ブラウザ体験版
- Node v22 `node --test *.test.mjs`: **11 passed / 0 failed**（既存設定4、デモロジック7）。
- Python Playwright + Chromium headless（HTML/CSS/JSをインライン化して評価、ローカルHTTPへのネットワークアクセス制限のため）: desktop 1280px / mobile 390px で漫画タップ・モード切替・範囲ドラッグ・3クリック拡大・フォーム入力、JavaScript例外0。ページ配信環境の統合試験は未実施。
- Web Speech API: `speechSynthesis.speak()`呼び出しと状態表示を検証。実音声の聴感品質は検証していない。
- 画像OCR、実漫画の自動吹き出し抽出、PC全体の拡大: Webデモでは**未実装（模擬表示）**。

### 2026-10-11 代替公開先HTTPスモークテスト
- [x] GitHub Actions `38118586141`: raw.githack.com 経由で demo.html / demo.css / style.css / demo.js / demo-logic.mjs / index.html / app.js / settings.js のHTTP 200と非空・HTMLマーカー確認すべて成功。
- [ ] 読み上げの実音声、実端末の3回タップ、外部プロキシのブラウザ確認画面からの実遷移は環境未検証。

### ドラム側のGitHub Pagesに独立プレビューデモを作成
- 既存 Pages の配下に8つの静的ファイルを追加。専用リポジトリ内の更新は自動的には反映されない。
- 公開導線 https://mleuatd.github.io/dtx-drum-flow/screen-reader-assist/demo.html
- Pages専用検証: `mleuatd/dtx-drum-flow/.github/workflows/verify-screen-reader-pages.yml` にHTTPテストを追加。公開実URL確認は実行結果を見て記録する。

### 2026-10-11 正式GitHub Pages上の公開確認
- ドラム側GitHub Pagesの自動デプロイ `38119463235`：**成功**。
- 実公開URL `https://mleuatd.github.io/dtx-drum-flow/screen-reader-assist/demo.html`：HTTP取得成功。
- 自動検証 `38119529084`：**成功**。index.html / demo.html / demo.js / demo.css / demo-logic.mjs / app.js / settings.js / style.css の8ファイルのHTTP 200、非空、重要な表示文字列の確認完了。
- 実際の読み上げ音声、画面の操作性はこのHTTPテストの対象外。以前の11件のWebロジックテストは成功済み。

### 2026-10-11 独立化の作業結果
- GitHubドラム側コピー8ファイル、独立デモ検証ワークフローの削除コミット：`b6c7b56`。ソースコード本体は専用リポジトリに維持。
- 専用raw.githack.comのHTTP取得テスト成功履歴：`38118586141`。GitHub標準の専用Pagesサイトは未公開。

### v0.2 Ctrl+Alt+Rと専用選択フォーム
- 既存のWindows Logic smoke testsに、方向逆ドラッグ、端タップ、小画面タップ、負座標マルチモニター座標、範囲外選択、旧グローバル3回クリック設定の無効化を追加。
- Windows Actions実行前時点では単体テスト結果は **未確認**。公開フローの実行結果を確認してから更新。
- 実機でのタッチ操作のイベント排他、フォーカス・音声の確認は **未実施**。

### v0.2 Windows CI / 公開検証
- [x] Windows build/Logic smoke Actions `38122996049` **成功**。
- [x] 14個の `PASS:` がWindowsログで確認できた：設定5・吹き出し3・領域座標5・3連クリック無効化1。
- [x] 配布処理 Actions `38122996060` **成功**、`v0.2.0-preview.1` のファイルサイズ74,554,485バイトをGitHub APIで確認。
- [ ] WinForms画面へのタッチ入力、ゲーム/ブラウザを下層アプリとして動かした場合の非干渉は未検証。

### v0.3リンク上の読み取り専用モード
- Windowsのビルド・単体テストが成功した場合でも、実際のYouTubeタイトルの長文・折り返しへのOCR品質は実機未検証。
- Windows 11実機で、元のアプリにマウス/タッチイベントが到達しないことは未検証。
- 座標ロジックテストにカーソル近傍の矩形と画面端の切り取り2件を追加。

### 2026-10-11 v0.3正式なCI検証結果
- [x] Windows Actions `38127109047` **成功**。ログから16件のPASSを確認（キャプチャ・実音声は除く）。
- [x] Webテスト `38127109071` **成功**。
- [x] Windows実験版ZIP `v0.3.0-preview.1` のGitHub Releases公開成功。Actions `38127109058`、74,556,618バイト。
- [ ] Windowsのタッチ実機、YouTubeリンク、OCRテキスト選択、SAPI音声品質は未検証。

### v0.4 モード切替の検証項目
- Windowsロジックテスト：既定のcursor/ocrFallback、旧version1の設定、異常mode/dwellMilliseconds、manga/selection/off判定を追加。
- Web設定テスト：既定値、変更保存、旧JSON互換、異常値補正を追加。
- Windows GUI実機：カーソルからUIAを取得できるか、OCRに自動で切り替わるか、漫画吹き出しを正しく認識するかは未確認。

### 最新：v0.4.0-preview.2（2026-10-11）
- Windows CI `38128336948`：**成功**（22件のロジックテストがPASS）。
- Webサイト/設定 `38128336957`：**成功**（Nodeテスト16/16件PASS）。
- GitHub Releases: `https://github.com/mleuatd/screen-reader-assist/releases/tag/v0.4.0-preview.2`（Windows ZIP 74,551,895 bytes）。
- Windowsの実画面でのUIA / MSAA COM値の取得、OCR精度、音声出力、マウス非干渉は**未検証**。自動テスト成功を実機検証済みとはみなさない。

### v0.4.1 自動領域検出・OCR補完
- 追加した試験：空白画像から文字を捏造しないこと、描画された文字付近の候補矩形、自動領域の座標、画像拡大とコントラスト補正の画素寸法を検査。
- Windowsの自動ビルドとロジックテストの結果はワークフローで確認する。実画像OCRの精度と音声再生は未実施。

### v0.4.1 リリース検証結果
- Windows CI `38128757682`：26件のPASSログを確認。
- 最新Windows CI `38128877682`：**成功**。
- Web設定テスト `38128877670`：**成功**。
- 試用版の配布 `38128877675`：**成功**、タグ`v0.4.1-preview.1`、`ScreenReaderAssist-win-x64.zip` 74,565,370バイト。
- 実画像のOCRが読めることや日本語漫画の検出精度・音声の実機結果は**未検証**。

### v0.5 音声処理の処理能力・ベンチマーク設計
- Fake engine（音声波形は生成しない）：1000件連続文字入力で音声一覧取得1回、音声選択1回、話速・音量の代入各1回となることを検査。10,000件送信処理時間もログ出力する。
- System.Speech実音声エンジン（CIのWindows runner）：`--benchmark-tts`を指定すると、音声をスピーカーに再生せず、WAVをメモリへ合成する時間と生成バイト数、文字数/秒を出力。
- WAVテストは一部のCIランナーでは対応音声や対話セッション不足により測れない可能性があるためbest-effort扱い。GUIの聴感品質や再生開始時刻の実測とは区別する。

### v0.5 文字列から音声への処理能力（GitHub Actions, Windows runner）
- `38129725171`：Windowsアプリビルドとロジックテスト **成功**。Fakeエンジンへの10,000件の送信は8.11ms。これは音声データの生成時間ではない。
- `38129971723`：最終最適化後のWindowsビルドとロジックテスト **成功、43件PASS**。
- 同ランナーで実音声をWAVへ合成：SAPI `Microsoft David Desktop`（英語）。
- 86文字の初回合成754.5ms、同文字数の2回目18.4ms、260文字50.7ms。WAVサイズは順に250,022 / 250,022 / 750,022 bytes。
- 同じ音声と86文字をそれぞれ5回ずつ試験した比較：`GetInstalledVoices + SelectVoice` を毎回呼ぶ条件は平均17.91ms、同一音声選択を使い回す条件は平均20.26ms。**この測定で音声生成計算が短縮したとは主張できない**。キャッシュによって重複設定呼び出しは削減された。
- 1万件の送信の再計測10.65msは偽エンジンのみ。文字が出るまでの待機時間・GUI初期化・音声が耳に聞こえるまでの遅延とは異なる。
- **日本語SAPI声での生成速度およびエンドユーザーWindows 11での音声再生開始時間は未計測**。

### 2026-10-11 日本語音声2系統の試験（重要）
- 専用Actions `38130885005`：WindowsランナーでSAPI日本語音声0。利用可能音声は Microsoft David Desktop（en-US）とMicrosoft Zira Desktop（en-US）。`Language.TextToSpeech~~~ja-JP~0.0.1.0`はNotPresentで、追加は5分タイムアウト、最後の状態もNotPresent。**日本語SAPIでの音声合成速度は未測定**。
- 専用Actions `38131159339`：WinRT OneCore英語3種類（David, Zira, Mark）、日本語音声0。**OneCore日本語での音声合成速度は未測定**。
- 上記workflowのジョブ成功表示は「英語しかないことを調べられた」ことを意味する。内部の日本語測定ステップは利用不可のため不合格/未測定であり、日本語性能の合格ではない。
- v0.5.1では設定画面に日本語SAPI音声をローカル測定する診断ボタンを追加。Windowsビルド/テストは別途記録する。

### v0.5.1 日本語音声診断ボタン実装・配布結果（2026-10-11）
- Windowsロジックテスト Actions `38131522337`：**成功、46件PASS**。日本語診断文章3種類の存在、診断結果ファイル作成、実音声の有無の報告を確認。
- 最新ソース Windows CI `38131598189`：**成功**。
- リリース Actions `38131598185`：**成功**。試用版 `v0.5.1-preview.1` ZIP 74,585,175バイト公開済み。
- OneCore Actions `38131159339`：日本語音声0、SAPI Actions `38130885005`：日本語音声0。**日本語での波形生成時間・発音は未計測**。

### v0.6 INI/ZIPテスト
- Windows C#ロジックテストにINIの日本語音声名、全モード、OCR設定、旧グローバル3クリック無効化、バージョン検査、UTF-8読み書きを追加。
- Web NodeテストにINI往復・コメント・不明キー・数値丸め／限界値・旧設定の扱いを追加。
- Web NodeテストでJSZipの実ZIPを生成し、EXE内容と追加ファイルを保持しつつ、INIだけ変更して再エクスポートすることを検証する。
- 配布ZIPにEXEとINIが同じ場所にあることをGitHub Windows Actionsで検査する。
- 外部GitHub ZIPのブラウザ直接fetchはCORS/配布CDNに依存しうるため実機Webでの検証が別途必要。
