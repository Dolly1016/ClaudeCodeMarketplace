# 翻訳ファイル（Language）

アドオンに翻訳テキストを含めるには、ZIP 内の `Language/` フォルダに `.dat` ファイルを置きます。

## ディレクトリ構成

```
<AddonName>/
├── addon.meta
├── Scripts/
└── Language/
    ├── English.dat     ← 必須。フォールバック言語
    ├── Japanese.dat    ← 任意
    └── ...             ← その他の言語
```

英語ファイルは必ず用意してください。現在の言語のファイルが存在しないか、キーが見つからない場合は英語にフォールバックします。

## サポートされている言語名

| ファイル名 | 言語 |
|-----------|------|
| `English.dat` | 英語 |
| `Japanese.dat` | 日本語 |
| `Korean.dat` | 韓国語 |
| `SChinese.dat` | 簡体字中国語 |
| `TChinese.dat` | 繁体字中国語 |
| `French.dat` | フランス語 |
| `German.dat` | ドイツ語 |
| `Spanish.dat` | スペイン語 |
| `Italian.dat` | イタリア語 |
| `Russian.dat` | ロシア語 |
| `Dutch.dat` | オランダ語 |
| `Brazilian.dat` | ブラジルポルトガル語 |
| `Portuguese.dat` | ポルトガル語 |
| `Latam.dat` | ラテンアメリカスペイン語 |
| `Filipino.dat` | フィリピン語 |
| `Irish.dat` | アイルランド語 |

## ファイル形式

`.dat` ファイルは UTF-8 のテキストファイルです。

```
# コメント行（#で始まる行）
"role.my-role.name":"My Role"
"role.my-role.description":"This is my role."

$extra
```

- `"key":"value"` — 翻訳エントリ。キーと値を `"..."` で囲み `:` で区切る
- `#` で始まる行はコメント
- `$<name>` で別ファイルを取り込む（`Language/<言語名>_<name>.dat` が読み込まれる）
- 3文字未満の行は無視される

## 複数ファイルへの分割

ファイルが大きくなる場合、`Language/<LangName>/` ディレクトリを作り複数の `.dat` ファイルに分割できます。ディレクトリ内のすべての `.dat` ファイルが読み込まれます。

```
Language/
├── English/
│   ├── roles.dat
│   └── options.dat
└── Japanese/
    ├── roles.dat
    └── options.dat
```

## スクリプトからの参照

```csharp
using Virial.Media;

string text = Localization.Translate("role.my-role.name");
```

`Localization.Translate(key)` は現在の言語で翻訳されたテキストを返します。キーが見つからない場合は `*key` の形式で返ります。
