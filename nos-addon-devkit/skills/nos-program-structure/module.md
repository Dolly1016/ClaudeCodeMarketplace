# IModule / DIManager — モジュールシステム

`Virial.DI` 名前空間が提供する DI（依存性注入）システムです。

## 基本概念

**コンテナ**: `IModuleContainer` を実装するオブジェクト（ゲーム、ゲームモード、プレイヤーなど）。モジュールを保持し `GetModule<T>()` で取得できます。  
**モジュール**: `IModule` を実装するオブジェクト。コンテナに注入されてその機能を拡張します。

```csharp
public interface IModule { }

public interface IModuleContainer
{
    T? GetModule<T>() where T : class, IModule;
}
```

## モジュールの強み — static クラスの代替として

ゲームが始まるたびに新しいインスタンスを自動で生成してくれるのがモジュールの大きな利点です。

- ゲーム全体に新しい機能を追加したい → **ゲームにモジュールを追加**
- プレイヤーごとに状態を持ちたい → **プレイヤーにモジュールを追加**

「`static` クラスを用意して、ゲーム開始のたびに手動で初期化する」という実装を考えている場合は、モジュールの使用を強く推奨します。モジュールはゲームやプレイヤーが生成されるたびに自動でインスタンス化されるため、**初期化忘れや前のゲームの状態が残るといった問題が起きません**。

## IGenericModule と AbstractModule

コンテナへの参照が必要なモジュールは `IGenericModule<Container>` を実装します。
`AbstractModule<Container>` を継承すると、注入時に `MyContainer` プロパティが自動設定されます。

```csharp
// プレイヤーに注入されるモジュールの例
public class MyPlayerModule : AbstractModule<Virial.Game.Player>, IModule
{
    protected override void OnInjected(Virial.Game.Player container)
    {
        // このプレイヤーへの注入直後に呼ばれる
        // MyContainer でいつでもプレイヤーを参照できる
    }
}
```

## DIManager による登録と自動注入

`DIManager.Instance` に対してモジュールファクトリを登録します。
コンテナが生成されると、そのコンテナが実装するインターフェースを起点に、対応するモジュールが自動注入されます。

```csharp
// アドオンの初期化時（PreprocessPhase.PostLoadAddons 以降）
DIManager.Instance.RegisterModule<Virial.Game.Player>(
    () => new MyPlayerModule()
);
```

- `RegisterModule<Container>(supplier)` — `IGenericModule<Container>` を返すファクトリを登録します
- `RegisterGeneralModule<Container>(supplier)` — `IModule` を返す汎用版

## モジュールの取得

コンテナから任意のタイミングでモジュールを取得できます。

```csharp
var module = someContainer.GetModule<MyPlayerModule>();
```

型が一致する最初のモジュールが返ります。見つからなければ `null`。
