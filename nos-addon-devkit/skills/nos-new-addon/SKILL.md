---
name: nos-new-addon
description: NoS(Nebula on the Ship)で使用するアドオンを新規作成する方法を示します。
---

# NoSの新規アドオン追加方法

NoS (Nebula on the Ship) アドオンの新規開発環境をセットアップし、開発の流れを案内します。

## 背景知識

**NoS (Nebula on the Ship)** は Unity 製ゲーム Among Us の Mod です。
Among Us は IL2CPP ビルドされており、BepInEx を利用して Mod を開発します。

リポジトリ **Nebula-Public** の構成:
- `NebulaPluginNova/` — NoS 本体。ゲームロジック、役職定義などが含まれる
- `NebulaAPI/` — アドオン開発者向けの公開 API。アドオンスクリプトから参照できる型・インターフェースが定義されている

## 手順

### 1. ゲームディレクトリの確認

`nos_set_game_dir` ツールで設定済みかどうかを確認する。
未設定であれば、NoS がインストールされている Among Us のディレクトリ (`Among Us.exe` があるフォルダ) をユーザに尋ね、設定する。

### 2. Nebula-Public のクローン

API の型情報を参照するために Nebula-Public リポジトリが必要です。
履歴は不要なのでシャロークローンを推奨します。

ユーザにクローン先ディレクトリを確認する。指定がなければワークスペース直下を提案する。

```bash
git clone --depth 1 https://github.com/Dolly1016/Nebula-Public.git
```

### 3. アドオンのディレクトリ構成を作成

アドオンはワークスペース直下に直接置いてはいけません。
必ず次の階層構造にしてください。

```
<WorkspaceRoot>/
└── <AddonsDirectory>/          # アドオン群をまとめるフォルダ（例: MyAddons）
    └── <RepoName>/             # このアドオンの git リポジトリのルート（例: AwesomeRole）
        └── <AddonName>/        # ZIP の中身になるフォルダ（例: AwesomeRole）
            ├── addon.meta      # アドオンのメタ情報
            └── Scripts/        # C# スクリプト置き場
```

- `<WorkspaceRoot>` はユーザが指定した場所、または現在のワークスペース
- `<AddonsDirectory>` はアドオン群をまとめるディレクトリ（プロジェクト名など）
- `<RepoName>` は GitHub リポジトリのルートとなるディレクトリ。`git init` はここで行う
- `<AddonName>` が ZIP に圧縮される単位。このフォルダの内容がそのままゲームに読み込まれる

`<RepoName>` と `<AddonName>` は通常同じ名前で構わない。

ユーザにアドオン名と配置先を確認してからディレクトリを作成し、`<RepoName>/` で `git init` する。

### 4. addon.meta の作成

`<RepoName>/<AddonName>/addon.meta` を作成する。フォーマット:

```json
{
    "Id": "addon-id-kebab-case",
    "Name": "表示名",
    "Author": "作者名",
    "Description": "アドオンの説明",
    "Version": "1.0.0",
    "Build": 1,
    "Dependency": []
}
```

- `Id` は小文字ケバブケース。ゲーム内でアドオンを一意に識別する
- `Dependency` には依存する他アドオンの `Id` を列挙する

### 5. スクリプトの開発

`Scripts/` フォルダに `.cs` ファイルを作成してスクリプトを実装する。

### 6. スクリプトのチェック

`nos_check` ツールで構文・型エラーを確認する。
引数には `<AddonName>/` フォルダ（`addon.meta` を直接含むディレクトリ）の絶対パスを渡す。
エラーがあれば内容を説明し、修正を提案する。

### 7. ゲームへのデプロイ（任意）

開発・チェックが一段落したら、実際のゲームで試せるよう ZIP をゲームの `Addons/` フォルダに配置できます。
ただしこれは任意なので、**適切なタイミングで一度だけ**ユーザに確認してから実施する。

ZIP の作成と配置:
- `<AddonName>/` フォルダの内容を ZIP に圧縮する（エントリのパス区切りはスラッシュ `/`）
- 出力先: `{game_dir}/Addons/{AddonName}.zip`

## 引数

```
/nos-new-addon
/nos-new-addon <アドオン名>
```
