# ILifespan — 寿命オブジェクト

`ILifespan` は **`IGameOperator`（作用素）がいつまで有効か** を表すオブジェクトです。

`IGameOperator` を `GameOperatorManager` に登録する際、必ず `ILifespan` を紐づけます。
`IsDeadObject` が `true` を返すようになった時点で、紐づけられた作用素はゲームから自動的に削除されます。

```csharp
public interface ILifespan
{
    bool IsDeadObject { get; }
    bool IsAliveObject { get => !IsDeadObject; }
}
```

## 主な実装クラス

| クラス | 有効期間 |
|--------|----------|
| `SimpleLifespan` | `Release()` が呼ばれるまで |
| `FunctionalLifespan` | 述語関数が `true` を返している間（一度 `false` になったら復活しない） |
| `DependentLifespan` | `Bind(parent)` で設定した親 `ILifespan` が生きている間 |
| `FlexibleLifespan` | 親が死ぬ OR 手動 `Release()` のどちらかが起きるまで |

`FunctionalLifespan.GetTimeLifespan(float duration)` で、指定した秒数だけ有効な lifespan を手軽に作れます。

## IReleasable

`Release()` で能動的に有効期間を終わらせられるインターフェースです。
`SimpleLifespan` と `FlexibleLifespan` が実装しています。

```csharp
var lifespan = new SimpleLifespan();
new MyOperator().Register(lifespan); // 作用素を登録
// ...
lifespan.Release(); // ここで作用素がゲームから削除される
```

## INestedLifespan

`Bind(ILifespan parent)` で親の有効期間に束縛できます。
`DependentLifespan` と `FlexibleLifespan` が実装しています。

`Register(lifespan)` を呼んだとき、登録する作用素自身が `INestedLifespan` を実装していれば、渡した lifespan を自動的に親として `Bind` します。

## RegisterSelf()

作用素自身が `ILifespan` も実装している場合（例: `AbstractPlayerAbility` は `DependentLifespan` を継承）、`RegisterSelf()` を使うと自分自身を lifespan として登録できます。
この場合、外部から `Bind(parent)` で有効期間を注入するまでは死亡しません。

```csharp
// AbstractPlayerAbility は DependentLifespan を継承しているので RegisterSelf() が使える
var ability = new MyAbility(player);
ability.RegisterSelf(); // ability 自身が lifespan として機能する
// 後から ability.Bind(someLifespan) で有効期間を設定
```
