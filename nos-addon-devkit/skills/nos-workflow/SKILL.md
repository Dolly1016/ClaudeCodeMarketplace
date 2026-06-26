---
name: nos-workflow
description: NoSアドオンの開発サイクルとバージョンアップ・リリースの手順を示します。
---

# NoSアドオン開発ワークフロー

## 背景知識

**NoS (Nebula on the Ship)** は Unity 製ゲーム Among Us の Mod です。
Among Us は IL2CPP ビルドされており、BepInEx を利用して Mod を開発します。

リポジトリ **Nebula-Public** の構成:
- `NebulaPluginNova/` — NoS 本体。ゲームロジック、役職定義などが含まれる
- `NebulaAPI/` — アドオン開発者向けの公開 API。アドオンスクリプトから参照できる型・インターフェースが定義されている

## 開発サイクル

1. `Scripts/` フォルダの `.cs` ファイルを編集する
2. テキストが必要な場合は `Language/` フォルダに翻訳ファイルを追加する（詳細 → [language.md](language.md)）
3. `nos_check` ツールで構文・型エラーを確認する
   - 引数には `addon.meta` を直接含むディレクトリの絶対パスを渡す
   - エラーがあれば内容を説明し、修正を提案する
4. 動作確認を行う（任意）
   - `<AddonName>/` の内容を ZIP に圧縮し、`{game_dir}/Addons/` に配置する

## バージョンアップ・リリース手順

アドオンを更新してリリースする際は毎回以下を行う。

### 1. addon.meta の更新

`<AddonName>/addon.meta` の `Version` と `Build` を更新する。
`Build` は整数値をインクリメントする。

### 2. コミット・プッシュ

```bash
git add .
git commit -m "<コミットメッセージ>"
git push
```

### 3. ZIP の作成

`<AddonName>/` フォルダの内容を ZIP に圧縮する。

**重要**: ZIP 内のエントリのパス区切りはスラッシュ `/` でなければならない。
PowerShell の `Compress-Archive` はバックスラッシュを使うため、System.IO.Compression API を使うこと。

```powershell
Add-Type -AssemblyName System.IO.Compression.FileSystem
$src = "<絶対パス>/<AddonName>"
$dest = "<絶対パス>/<AddonName>.zip"
if (Test-Path $dest) { Remove-Item $dest }
$zipStream = [System.IO.File]::Open($dest, [System.IO.FileMode]::Create)
$zip = New-Object System.IO.Compression.ZipArchive($zipStream, [System.IO.Compression.ZipArchiveMode]::Create, $false)
Get-ChildItem -LiteralPath $src -Recurse -File | ForEach-Object {
    $relPath = $_.FullName.Substring($src.Length + 1).Replace('\', '/')
    $entry = $zip.CreateEntry($relPath, [System.IO.Compression.CompressionLevel]::Optimal)
    $es = $entry.Open(); $fs = [System.IO.File]::OpenRead($_.FullName)
    $fs.CopyTo($es); $fs.Dispose(); $es.Dispose()
}
$zip.Dispose(); $zipStream.Dispose()
```

### 4. GitHub Release の作成

```bash
gh release create <タグ名> "<AddonName>.zip" --title "<タイトル>"
```

- タグ名はリリースごとに一意であれば自由に決めてよい
- `--prerelease` や `--draft` フラグをつけないこと（ゲームが最新 Release として認識できなくなる）
- マーケットプレイスにこのアドオンを登録したプレイヤーの元に ZIP が自動配信される
