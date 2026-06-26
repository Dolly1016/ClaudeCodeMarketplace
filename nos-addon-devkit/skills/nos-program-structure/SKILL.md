---
name: nos-program-structure
description: NoSプログラムの構造と重要コンポーネント（IModule・ILifespan・イベントシステム）の概要を説明します。
---

# NoSプログラム構造

NoSのコードは `Virial` 名前空間（NebulaAPI）が公開 API を、`Nebula` 名前空間（NebulaPluginNova）が実装を担う構成になっています。
アドオンは Virial の型のみを使用します。

## 便利なコンポーネント

### ILifespan — 寿命オブジェクト

`ILifespan` は `IGameOperator`（作用素）がいつまで有効かを表すオブジェクトです。
作用素を登録する際に `ILifespan` を紐づけ、`IsDeadObject` が `true` を返すと作用素はゲームから自動削除されます。

詳細 → [lifespan.md](lifespan.md)

### IModule / DIManager — モジュールシステム

`IModule` はゲームやプレイヤーといったコンテナに注入されるコンポーネントです。
コンテナが生成されるたびに自動でインスタンス化されます。
静的クラスを用意してゲームのたびにデータを初期化することを考えているなら、代わりにモジュールの使用を検討してください。

詳細 → [module.md](module.md)

### IGameOperator / イベントシステム — リスナ登録

`IGameOperator` はゲームイベントを受け取るリスナを実装するための中心的なインターフェースです。
`Virial.Events.Event` を継承したイベントを引数に取るメソッドを定義するだけでリスナになります。
`ILifespan` と組み合わせて登録し、寿命が尽きると自動的に登録解除されます。

詳細 → [event-system.md](event-system.md)

## システム

### プリプロセス

ゲーム起動時に `[NebulaPreprocess]` 属性付きクラスの静的コンストラクタと `Preprocess` メソッドが実行されます。
`NebulaPreprocessor` を通じて `CommunicableTextTag`・`RoleTeam`・`GameEnd` など、プリプロセス中にしか生成できないオブジェクトを作成できます。

詳細 → [preprocess.md](preprocess.md)

## テクニック

### Il2Cpp オーバーヘッドを避ける

Among Us は IL2CPP ビルドのため、`UnityEngine.Vector2` 等の Unity 型を C# から扱うと Il2Cpp 越しの呼び出しが発生し負荷になります。
頻繁にベクトル演算を行う箇所では以下の代替型を使用してください。いずれも `UnityEngine` の対応型との暗黙的相互変換が定義されています。

| 代替型 | 元の型 |
|--------|--------|
| `Virial.Compat.Vector2` | `UnityEngine.Vector2` |
| `Virial.Compat.Vector3` | `UnityEngine.Vector3` |
| `Virial.Compat.Vector4` | `UnityEngine.Vector4` |
| `Virial.Color` | `UnityEngine.Color` |

## 参考コード

実装の参考にしたい場合は Nebula-Public の以下を参照してください。

- 既存役職の実装: `NebulaPluginNova/Roles/`
