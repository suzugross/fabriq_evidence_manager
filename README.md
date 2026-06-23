# fabriq evidence manager

Windows キッティングフレームワーク **fabriq** の公式サブツールです。fabriq がキッティング実行時に出力した `evidence` フォルダ（各種ログ、自動キャプチャ画像、BitLocker 回復キー、manifest など）を読み込み、GUI 上で整理・確認し、顧客への納品用フォーマットとして一括出力（Excel 台帳 + 整理済みエビデンスフォルダ）するための WPF アプリケーションです。

evidence 本体には一切変更を加えず、外側からデータを安全に読み取り・処理する設計になっています。

---

## 主な機能

- **evidence フォルダの自動検出・読み込み**
  `{timestamp}_{PCName}_{Serial}_evidence/evidence/{category}/` 形式のフォルダを走査し、フォルダ名から PC 名・シリアル・収集日を抽出。各 PC の `pc_information/manifest.json`（schemaVersion=1）を起点に各セクションをパースします。
- **PC 一覧表示・検索・フィルタ**
  PC 名 / シリアル / 警告メッセージによるリアルタイム絞り込みに対応。
- **欠損・要確認の検知（ハイライト）**
  - 欠損チェック（MAC / BitLocker / pc_information / auto_capture / チェックリスト / Windows・Office ライセンスなど）
  - 要確認（Caution）判定：PC 名不一致（rename 失敗の可能性）、シリアル番号のフォールバック取得、hostlist 不一致、チェックリスト NG、ベースライン差異、Office サインイン待ち
- **詳細プレビュー**
  選択 PC の自動キャプチャ画像（複数枚送り）、各種パース済み情報（ドメイン状態、ローカルユーザー / グループ、ファイアウォール、ディスク / パーティション、Defender、Windows Update など）の確認。
- **hostlist 突合**
  `hostlist.csv`（NewPCName をキー）と実際の収集値を突合し、期待値と実値の差異を検出。
- **ベースライン比較**
  任意の PC を基準（ベースライン）に設定し、他 PC との差分を比較。比較カテゴリはプラグイン式（実行サマリ / SystemInfo / チェックリスト / デスクトップアプリ / ストアアプリ / ライセンス / ドメイン状態）で、設定ダイアログから ON/OFF 可能。
- **PC 単位メモ**
  各 PC ルートの `manager_memo.json` に管理者メモを保存・読込。
- **納品データ一括出力**
  出力先に `{timestamp}_fabriq_evi` フォルダを作成し、各 PC の整理済みエビデンスと **Excel 台帳（PC 情報一覧表）** を生成。Excel はフリート全体のサマリシートと、PC ごとの詳細シート（`pc_details/` サブフォルダ）に分割されます。
- **自動リロード**
  30 秒間隔で差分マージ再読み込み（選択状態・検索文字列・スクロール位置を維持）。

---

## 動作環境

- Windows（WPF アプリのため Windows 専用 / TargetFramework: `net8.0-windows`）
- .NET 8 SDK（ソースからビルドする場合）

公開ビルドは `win-x64` 向けの自己完結型単一ファイルとして発行されます（.NET ランタイムのインストール不要）。

---

## 使用技術

| 分類 | 採用技術 |
|------|----------|
| ランタイム | .NET 8 (WPF, `net8.0-windows`) |
| MVVM | [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) 8.4 |
| DI | Microsoft.Extensions.DependencyInjection 10 |
| Excel 出力 | [ClosedXML](https://github.com/ClosedXML/ClosedXML) 0.105 |
| テスト | xUnit |

---

## アーキテクチャ

MVVM パターンを厳守し、ビジネスロジック・データ操作はすべて Service 層（Interface 定義 + DI 注入）に集約しています。Code-Behind にはロジックを持ちません。

```
View (XAML)
  └─ ViewModel (CommunityToolkit.Mvvm)
       └─ Service (Interface + DI)
            └─ Model (PcEvidence ほか)
```

### 処理フロー

```
evidence フォルダ選択
  ↓ NestedEvidenceDiscoveryService   … フォルダ走査・PC 検出・manifest 読込
  ↓ EvidenceParserService            … manifest 駆動で各セクションをパース
  ↓ RevalidateEvidence               … 欠損チェック (HasWarning)
  ↓ Caution 判定 (MainWindowViewModel) … hostlist / baseline / checklist / license
  ↓ ExcelExportService               … 納品データ出力
```

### 主要コンポーネント

- **Models**
  `PcEvidence`（1 台分の集約モデル / `ObservableObject`）を中心に、各セクションのモデル群。
- **Services**
  - 検出: `NestedEvidenceDiscoveryService`
  - パース: `EvidenceParserService`（manifest dispatch）+ セクション別専用パーサ（Windows / Office ライセンス、セキュリティベースライン、証明書、バッテリ、ハードウェア識別子、メモリ、環境変数、スタートアップ、PnP、グループポリシー）
  - 突合: `HostlistService` / `EvidenceVerificationService` / `ChecklistParserService`
  - ベースライン: `BaselineService` + `IBaselineComparator` 実装群（プラグイン式）
  - 出力: `EvidenceCollectorService`（フォルダ整理） / `ExcelExportService`（Excel 生成）
  - メモ: `PcMemoService`
- **ViewModels**
  `MainWindowViewModel` / `PcDetailViewModel` / `SettingsViewModel`

### ベースライン比較カテゴリの追加

`IBaselineComparator` を実装したクラスを作成し、`App.xaml.cs` の DI 登録に 1 行追加するだけで新カテゴリを追加できます。

---

## ビルドと実行

プロジェクトルートで以下を実行します。

```bash
# ビルド
dotnet build

# 実行
dotnet run --project FabriqEvidenceManager/FabriqEvidenceManager.csproj

# テスト
dotnet test
```

### 発行（自己完結型単一 EXE）

```bash
dotnet publish FabriqEvidenceManager/FabriqEvidenceManager.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
  -o <出力先パス>
```

---

## 使い方（概要）

1. アプリを起動し、**evidence フォルダを選択**（fabriq が出力したルートフォルダ）。
2. PC 一覧が表示される。警告・要確認のある PC はハイライトされる。
3. 必要に応じて **hostlist.csv の読み込み** / **ベースライン PC の設定** を行うと、突合・差分結果が反映される。
4. 設定ダイアログで欠損チェック項目・ベースライン比較カテゴリの ON/OFF を調整。
5. 各 PC を選択して詳細・キャプチャ画像・突合結果を確認。
6. **納品データ一括出力** で、整理済みエビデンスと Excel 台帳を出力先フォルダに生成。

### hostlist.csv について

ヘッダ行付き・カンマ区切り（BOM 付き UTF-8 を想定）。`NewPCName` 列をキーとして突合します。主な列：`AdminID` / `OldPCName` / `NewPCName` / `EthernetIP` / `EthernetSubnet` / `EthernetGateway` / `WifiIP` ほか / `DNS1`〜`DNS4` / `Pin` / `Printer{N}Name` / `Printer{N}Driver` / `Printer{N}Port`（プリンタは最大 10 件）。

---

## リポジトリ構成

```
FabriqEvidenceManager/          アプリ本体
  ├─ Models/                    データモデル
  ├─ Services/                  サービス層 (Interfaces/ にインターフェース)
  │   └─ Baseline/              ベースライン比較コンパレータ
  ├─ ViewModels/                ViewModel
  ├─ Views/                     XAML (MainWindow / PcDetailWindow / SettingsWindow)
  └─ Helpers/                   定数・コンバータ等
FabriqEvidenceManager.Tests/    xUnit テスト
.tm/                            タスク管理 (TM アプリと共用)
```

> `evidence/`（収集データ）は機密 PC 情報を含むため `.gitignore` で除外されています。

---

## 開発上の注意

- **スモールステップ実装**・**MVVM 厳守**・**共通処理の Service 化（DRY / DI）** を方針としています（詳細は `CLAUDE.md` を参照）。
- evidence のパス構造・CSV フォーマットは推測で決め打ちせず、実データを確認した上で実装します。
- テストは xUnit を採用しています。

---

## ライセンス

社内ツール（fabriq フレームワークの公式サブツール）。
