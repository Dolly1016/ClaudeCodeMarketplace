# プリプロセス

ゲーム起動時、NoS は各アセンブリをスキャンして `[NebulaPreprocess]` 属性が付いたクラスを収集し、指定されたフェーズに従って順番に実行します。

## 実行されるもの

```csharp
[NebulaPreprocess(PreprocessPhase.PostFixStructure)]
public class MySetup
{
    // 1. 静的コンストラクタが実行される（常に）
    static MySetup()
    {
        // DIManager への登録など
    }

    // 2. 静的 Preprocess メソッドがあれば実行される（オプション）
    static void Preprocess(NebulaPreprocessor preprocessor)
    {
        // NebulaPreprocessor を使った初期化
    }
}
```

## PreprocessPhase

フェーズは起動処理の順序を表します。アドオンが使用できる主なフェーズ:

| フェーズ | タイミング |
|---------|-----------|
| `PostLoadAddons` | アドオン読み込み直後。DIManager へのモジュール登録はここ以降 |
| `Roles` | 役職を追加する |
| `PostRoles` | 役職追加直後 |
| `PostFixStructure` | 共有可能変数などのデータ構造確定直後。最も汎用的 |

## NebulaPreprocessor で作成できるもの

`Preprocess(NebulaPreprocessor preprocessor)` の引数から以下を生成できます。
これらは**プリプロセス中にしか生成できません**。

| メソッド | 生成されるもの |
|---------|--------------|
| `preprocessor.RegisterCommunicableText(key)` | `CommunicableTextTag` — RPC で送受信できるテキストタグ |
| `preprocessor.CreateTeam(key, color, revealType)` | `RoleTeam` — 役職の陣営 |
| `preprocessor.CreateEnd(name, color, priority)` | `GameEnd` — ゲーム終了条件 |
| `preprocessor.CreateExtraWin(name, color)` | `ExtraWin` — 追加勝利 |
| `preprocessor.RegisterAssignable(assignable)` | 役職・モディファイアをゲームに登録 |
| `preprocessor.DIManager` | `DIManager` へのアクセス（モジュール登録など） |

## 実際のコード例

### 例1: モジュールを DI 登録する（静的コンストラクタのみ）

```csharp
// CrewmateGameRule.cs より
[NebulaPreprocess(PreprocessPhase.BuildNoSModule)]
public class CrewmateGameRule : AbstractModule<IGameModeStandard>, IGameOperator
{
    static CrewmateGameRule() => DIManager.Instance.RegisterModule(() => new CrewmateGameRule());
    public CrewmateGameRule() => this.RegisterPermanently();

    void CheckWins(PlayerCheckWinEvent ev) => /* ... */;
}
```

静的コンストラクタの中で `DIManager.Instance.RegisterModule` を呼ぶのが基本パターンです。
`RegisterModule` に渡したファクトリが、対象コンテナ生成のたびに呼ばれてインスタンスが注入されます。

### 例2: Preprocess メソッドで DI 登録する

```csharp
// NebulaGameEventListeners.cs より
[NebulaPreprocess(PreprocessPhase.BuildNoSModule)]
internal class NebulaGameEventListeners : AbstractModule<Virial.Game.Game>, IGameOperator
{
    static void Preprocess(NebulaPreprocessor preprocessor)
    {
        preprocessor.DIManager.RegisterModule(() => new NebulaGameEventListeners().RegisterPermanently());
    }
}
```

`preprocessor.DIManager` 経由での登録も同義です。

## SchedulePreprocess

`Preprocess` の中から、別フェーズの処理を予約できます。

```csharp
static void Preprocess(NebulaPreprocessor preprocessor)
{
    preprocessor.SchedulePreprocess(PreprocessPhase.PostFixStructure, () =>
    {
        // 後のフェーズで実行したい処理
    });
}
```
