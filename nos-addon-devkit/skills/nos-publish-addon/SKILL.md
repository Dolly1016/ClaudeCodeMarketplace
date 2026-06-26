---
name: nos-publish-addon
description: 開発したNoSアドオンを公開する方法を示します。
---

# NoSアドオンの公開

アドオンを GitHub Release として公開します。
ゲームはマーケットプレイスから登録されたリポジトリの最新 Release を参照し、
アセット内の `.zip`/`.addon` ファイルを自動ダウンロードします。

## 前提条件

- `gh` (GitHub CLI) がインストール済みで `gh auth login` 認証済みであること
- `/nos-new-addon` の手順でディレクトリ構成が作成済みであること

## ディレクトリ構成の確認

```
<AddonsDirectory>/
└── <RepoName>/             ← git リポジトリのルート（gh コマンドはここで実行）
    └── <AddonName>/        ← ZIP の中身
        ├── addon.meta
        └── Scripts/
```

## 手順

### 1. バージョンの確認

`<AddonName>/addon.meta` を読み、`Version` フィールドを確認する。

### 2. GitHub リポジトリの準備（初回のみ）

`<RepoName>/` が GitHub リポジトリとしてまだ登録されていなければ作成する。

```bash
cd <RepoName>
git init          # まだの場合
gh repo create <RepoName> --public --source=. --remote=origin --push
```

リポジトリを公開（public）にすること。
ゲームは GitHub API 経由でリリースを取得するため、private では動作しない。

すでにリモートが設定済みの場合は `git push origin main`（または `master`）でプッシュする。

### 3. ZIP の作成

`<AddonName>/` フォルダの内容を ZIP に圧縮する。

**重要**: ZIP 内のエントリのパス区切りはスラッシュ `/` でなければならない。
PowerShell の `Compress-Archive` はバックスラッシュを使うため、System.IO.Compression API を使うこと。

PowerShell での ZIP 作成例:

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
gh release create <タグ名> "<AddonName>.zip" \
  --title "<タイトル>" \
  --repo <owner>/<RepoName>
```

- タグ名はユーザが自由に決めてよい
- アセットとして ZIP ファイルを添付する（ファイル名は `.zip` または `.addon` で終わること）
- マーケットプレイスでこのアドオンを登録したプレイヤーの元に ZIP がダウンロードされる

### 5. バージョンアップ時の手順

アドオンを更新してリリースする際は毎回以下を行う。

1. `addon.meta` の `Version` と `Build` を更新する（`Build` は整数値をインクリメント）
2. スクリプトの変更を git でコミット・プッシュ
3. 手順 3〜4 を繰り返す（新しいバージョンタグで Release を作成）

## 注意

- GitHub Release のタグ名が重複するとエラーになる。バージョンアップのたびに新しいタグを使うこと
- `releases/latest` は最新の Release（ドラフト・プレリリースを除く）を指す。`--prerelease` や `--draft` フラグをつけないこと
- NoS マーケットプレイスへの登録は別途 `/nos-marketplace-publish` を参照
