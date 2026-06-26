# IGameOperator / イベントシステム

## 全体の流れ

1. `IGameOperator` を実装するクラスに、`Virial.Events.Event` を継承したイベントを引数に取るメソッドを定義する → これがリスナになる
2. `GameOperatorManager` に登録する（寿命 `ILifespan` と紐づける）
3. イベントが発火すると登録済みのリスナが呼び出される。寿命が尽きたリスナは自動削除される

## リスナの定義

メソッド名は任意。引数の型がイベントの型と一致すれば自動でリスナとして認識されます。

```csharp
public class MyOperator : IGameOperator
{
    // GameStartEvent のリスナ
    void OnGameStart(GameStartEvent ev)
    {
        // ゲーム開始時の処理
    }

    // プレイヤー死亡イベントのリスナ
    void OnPlayerDead(PlayerDieEvent ev)
    {
        var player = ev.Player;
    }
}
```

リフレクションで検出されるルール:
- 引数が 1 つだけで、その型が `Virial.Events.Event` を継承していること
- `<` で始まるメソッド名（コンパイラ生成の匿名関数）は除外される

## 登録方法

```csharp
// ゲーム全体と同じ寿命で登録
new MyOperator().Register(NebulaAPI.CurrentGame!);

// ゲーム終了まで削除されない登録
new MyOperator().RegisterPermanently();

// 自分自身が ILifespan でもある場合（例: AbstractPlayerAbility）
new MyAbility(player).RegisterSelf();
```

## リスナ属性

メソッドに属性を付けることで発火条件を絞れます。

| 属性 | 効果 |
|------|------|
| `[EventPriority(int)]` | 実行優先度。高いほど先に呼ばれる。定数: `VeryHigh`(1000), `High`(100), `Default`(0), `Low`(-100), `VeryLow`(-1000) |
| `[OnlyMyPlayer]` | `IBindPlayer` 実装クラスのみ有効。イベントの `Player` が `MyPlayer` と一致するときだけ発火 |
| `[OnlyLocalPlayer]` | イベントの `Player` がローカルプレイヤーのときだけ発火 |
| `[Local]` | `IBindPlayer` 実装かつ `AmOwner == true` の場合のみリスナを登録する（登録自体がスキップされる） |
| `[OnlyHost]` | ホストのみ発火 |

```csharp
public class MyPlayerOperator : AbstractPlayerAbility
{
    public MyPlayerOperator(Player player) : base(player) { }

    // 自分のプレイヤーが死亡したときだけ発火
    [OnlyMyPlayer]
    void OnDead(PlayerDieEvent ev) { /* ... */ }

    // 優先度を上げて先に処理する
    [EventPriority(EventPriority.High)]
    void OnUpdate(GameUpdateEvent ev) { /* ... */ }
}
```

## ラムダによる登録

クラスを定義せずにラムダで登録することもできます。

```csharp
GameOperatorManager.Instance?.Subscribe<GameStartEvent>(ev =>
{
    // ゲーム開始時の処理
}, someLifespan);
```

一度だけ発火させる場合は `SubscribeSingleListener` を使います。

## RecyclableEvent

`[RecyclableEvent]` 属性が付いたイベントはインスタンスが再利用されます。
リスナの処理が終わった後に値が変わるため、**イベントインスタンスをキャッシュしてはいけません**。

## イベントの継承と retroactive

`GameOperatorManager.Run<E>(ev, retroactive: true)` を指定すると、イベントの基底クラスに登録されたリスナも連鎖して呼び出されます（通常は `false`）。
