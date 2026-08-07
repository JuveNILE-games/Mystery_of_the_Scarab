# Agent Coding Guidelines — Mystery of the Scarab

You should always call me by my name (Michael) when speaking to me

## Build / Lint / Test

- Build: Use Unity Editor (Unity 6 / 6000.x, C# 9 baseline, IL2CPP)
- Lint: No dedicated linter — rely on IDE inspections and compiler warnings
- Tests: Unity Test Framework
  - Run single test: Unity Test Runner window
  - Run all tests: `Unity.exe -runTests -projectPath . -testResults results.xml`

## Project Structure

```
Packages/
├── com.juvenile.games.core/     ← git submodule (JuveNILE.Core framework, shared across projects)
└── com.juvenile.games.netcore/  ← git submodule (JuveNILE.NetCore multiplayer layer)

Assets/
├── Game/           ← Mystery of the Scarab game-specific code
│   ├── AI/         ← Companion behavior trees, puzzle observer
│   ├── Installers/ ← Game-specific IGameServiceInstaller implementations
│   ├── Story/      ← Yarn dialogue files, narrative scripts
│   ├── Systems/    ← Game systems (PuzzleSystem, etc.)
│   └── UI/         ← Game UI views and presenters
├── Plugins/        ← Third-party packages (PurrNet, Odin, Soap, etc.)
└── Scenes/
```

Core and NetCore are embedded UPM packages (physically inside `Packages/`, not referenced via `manifest.json`), not plain `Assets/` folders — this is what makes their `package.json`/asmdef dependency resolution actually work.

Core namespace convention: `Core.Systems.<SystemName>`, `Core.Installers`, `Core.Boot`, `Core.Definitions`, `Core.Utility`. Some root-level interfaces (`IServiceLocator`, `IControllable`) exist without a namespace for historical reasons — do not add new ones.

## Documentation

Architecture documentation lives in the Obsidian vault at `G:\Mijn Drive\Notes\JuveNILE Games\`:
- `Framework/Core/` — 22 system docs (Service Locator, Boot Pipeline, Dialogue, StateMachine, etc.)
- `MysteryOfTheScarab/Technical Documentation/` — 11 game docs (Puzzle System, Companion AI, etc.)

Consult these before modifying any system you are unfamiliar with.

---

## Code Style

### Formatting
- **Indentation**: 4 spaces (no tabs)
- **Brace style**: Mixed in existing code (Allman and K&R both exist). **Prefer Allman (next-line braces) for new files**. When editing existing files, match the surrounding style.
- **Expression-bodied members**: Use for single-expression properties, operators, and short methods (see `DialogueService`, `SoapDialogueEventBus`).

### Naming Conventions
- **Classes / Methods / Properties / Events**: PascalCase (`DialogueService`, `HandleInput`, `IsRunning`)
- **Private fields**: `_camelCase` with underscore prefix for injected/readonly dependencies (`_logger`, `_inputReader`, `_eventBus`). Legacy code may omit the prefix — always use underscore in new code.
- **Local variables / parameters**: camelCase (`targetState`, `soundEmitter`)
- **Constants**: PascalCase (C# convention). UPPER_SNAKE_CASE is acceptable but not the dominant pattern.
- **Interfaces**: `I` prefix (`IServiceLocator`, `IDialogueService`, `IInitializableService`)

### Fields & Properties
- `[SerializeField] private` for inspector-exposed MonoBehaviour fields
- `[field: SerializeField] public T Prop { get; private set; }` for read-only inspector properties
- Public fields are acceptable on `[Serializable]` data transfer classes (`SoundData`, `AudioServiceConfig`) but not on services or MonoBehaviours
- Use `[Header("...")]` and `[Tooltip("...")]` on ScriptableObject configuration fields

---

## Architecture Patterns

### Dependency Injection (Two Mechanisms)

**1. Constructor injection** — for pure C# services (non-MonoBehaviour):
```csharp
public class DialogueService : IDialogueService, IInitializableService, IDisposable
{
    private readonly ILoggerService _logger;
    private readonly InputReader _inputReader;

    public DialogueService(ILoggerService logger, InputReader inputReader, ...)
    {
        _logger = logger;
        _inputReader = inputReader;
    }
}
```

**2. `[Inject]` attribute** — for MonoBehaviours (cannot use constructors):
```csharp
public class DialogueBoxPresenterAdapter : MonoBehaviour
{
    [Inject] private IDialogueServiceConfig _config;
    [Inject] private ILineResolver _lineResolver;
    [Inject] private ILoggerService _logger;
}
```
`MonoBehaviourInjection.InjectAllMonoBehaviours()` processes these on scene load via `Bootstrapper.OnSceneLoaded`.

### Service Registration (Installer Pattern)

Services are registered via `IInstaller.Install(IServiceLocator)`:
- `CoreServicesInstaller` — framework services (logging, audio, pooling, theming, etc.)
- `GameServicesInstaller` — game services (save, dialogue events, etc.)
- `NavigationInstaller`, `CommandInstaller` — domain-specific
- `IGameServiceInstaller` — tagging interface for game-assembly installers discovered by Bootstrapper

Registration styles:
```csharp
locator.Register<ILoggerService, UnityLoggerService>();        // Interface → Implementation
locator.Register<TaskRunner, TaskRunner>();                      // Concrete self-registration
locator.Register<IAssetManagementService>(s => s.Get<AssetManagementService>()); // Alias
locator.Register<ThemeConfig>(_ => coreConfig.Theme);           // Factory/lambda
```

### Service Lifecycle

Services implement `IInitializableService`:
```csharp
public interface IInitializableService
{
    int InitializationPriority { get; }  // Lower = earlier
    UniTask InitializeAsync(IProgress<float> progress = null);
    UniTask ShutdownAsync();
}
```
Initialization order is driven by `InitializationPriority`. Cleanup via `IDisposable.Dispose()`.

### Boot Pipeline

`Bootstrapper` → `BootPipeline.Run()` → ordered `IBootPhase` implementations → scene loading → `MonoBehaviourInjection`. The Bootstrapper is a `[DefaultExecutionOrder(-1000)]` singleton with `DontDestroyOnLoad`.

### State Machine

Generic hierarchical state machine: `StateMachine<TOwner, TState>`. States implement `IState<TOwner>` with `OnEnter`/`OnExit`/`OnUpdate`/`OnFixedUpdate` + async variants. Transitions are priority-ordered.

### Event Systems (Dual)

**1. Signal bus** — type-safe C# events for cross-system communication:
```csharp
_signalsBus.Publish(new DialogueStartedSignal(nodeName));
_signalsBus.Subscribe<DialogueEndedSignal>(handler);
```

**2. Soap ScriptableObject events** — inspector-wirable, scene-surviving:
```csharp
_dialogueStarted.Raise(nodeName);  // ScriptableEventString asset
```
Both systems coexist. Use signals for code-to-code; Soap for designer-wired/inspector-visible events.

---

## Async & Threading

- **Use UniTask** (Cysharp) for all async/await — not `System.Threading.Tasks.Task`, not coroutines
- Return `UniTask` / `UniTask<T>` from async methods, `UniTaskVoid` for fire-and-forget
- Use `.Forget()` for fire-and-forget calls from sync contexts
- Pass `CancellationToken` through async chains. Use `CancellationTokenSource.CreateLinkedTokenSource()` to compose cancellation
- `TaskRunner` orchestrates multi-step async sequences — it is not a replacement for `async/await`
- Do not use coroutines in new code

---

## Logging

Use injected `ILoggerService`, not `Debug.Log`:
```csharp
_logger?.Log(this, "[Dialogue] Started node...");
_logger?.LogWarning(this, "...");
_logger?.LogError(this, "...");
```
- Static shorthand (when no injected logger): `Log.Info(this, "...")`, `Log.Warning(...)`, `Log.Error(...)`
- `Debug.Log` is only acceptable in `Bootstrapper` pre-initialization paths (before the logger service exists)

## Error Handling

- Guard with early returns + logging, not deep exception nesting
- Use `try/catch` for async operations, especially around `UniTask` awaits
- Catch `OperationCanceledException` separately (normal flow, not an error)
- Implement `IDisposable` on services: cancel CTS, unsubscribe events, null out references
- Compare `UnityEngine.Object` with `==`/`!=` only — never use `is null`, `?.`, `??` (fake-null trap)

---

## ScriptableObject Conventions

- `[CreateAssetMenu(menuName = "Core/<Category>/<Name>", fileName = "<Name>")]`
- Configuration SOs are read-only at runtime — use `[field: SerializeField] public T Prop { get; private set; }`
- SO event channels (Soap): used for inspector-wired cross-scene events
- Split serialized initial values from `[NonSerialized]` runtime state to prevent editor leaks
- Do not reference scene objects from SOs

---

## Dialogue System (Yarn Spinner)

- Core's `DialogueService` drives Yarn Spinner via `DialogueRunner`. Game wiring is in `MysteryOfTheScarabInstaller.InstallDialogue()`
- **Role-based resolution**: `DialogueContext` carries `Initiator` (who pressed interact) and `Companion` (the other character). `DialogueYarnFunctions` exposes these as Yarn functions for `when:` condition gating in Node Groups
- **Transient variables**: `$initiator_name`, `$companion_name`, `$is_multiplayer` are injected before each conversation and cleaned after to prevent save pollution
- **DialogueParticipant**: MonoBehaviour on players/NPCs implementing `IDialogueParticipant`. Auto-detects Player (live `Bindable<CharacterData>`) vs NPC (inspector-assigned) mode
- **PlayerSlotProvider**: Wraps `IControllableRegistry` for multiplayer-aware Initiator/Companion slot assignment
- **Custom commands**: Registered via `IDialogueCommandRegistry` — includes `<<emotion>>`, `<<play_audio>>`, `<<stop_audio>>`, `<<waitDialogue>>`
- **Presenter pattern**: `IDialoguePresenter` (open/close lifecycle), `DialogueBoxPresenterAdapter` bridges UI Toolkit `DialogueBox` to the service
- **Event flow**: Dual — `IDialogueEventBus` (Soap SO events for inspector wiring) + `IEventBus` signals (`DialogueStartedSignal`, `DialogueEndedSignal`) for code-to-code

## Puzzle System

- **PuzzleDefinition** (ScriptableObject): Describes conditions, logic gate tree, prerequisites, rewards, optional fail timer
- **PuzzleComponent** (abstract MonoBehaviour): Base for physical puzzle elements (levers, buttons, pressure plates). Implements `IPuzzleCondition` with `ConditionId`, `IsMet`, `OnConditionChanged`
- **LogicGate tree**: `ILogicNode` interface with `LogicGate` (AND/OR/NOT/XOR/NAND/NOR) and `LogicLeaf` (references a `PuzzleComponent` by `conditionId`)
- **PuzzleController**: Runtime controller — resolves conditions via `PuzzleComponentRegistry`, subscribes to changes, evaluates root node
- **PuzzleRewardExecutor**: Translates `PuzzleReward` data into actions: `UnlockDoor`, `TriggerEvent`, `PlayDialogue`, `SpawnItem`, `Custom`
- **Ability gating**: `RequiresAbility` on components restricts interaction to characters with matching `abilityId`
- **Companion AI integration**: `CompanionPuzzleObserver` evaluates puzzle targets for AI — feeds `HasActionablePuzzleTarget` and `TargetComponent` into the Behavior Graph blackboard
- **Companion AI**: Uses Unity Behavior Graph + NavMesh. `CompanionAIAdapter` (IAIController) manages lifecycle. `AIMovementBridge` feeds `NavMeshAgent.desiredVelocity` into the same `PlayerStateMachine` as human input — NavMeshAgent never writes to `transform.position` directly

---

## Multiplayer

- **PurrNet** for networking (`NetworkBehaviour`, `NetworkManager`)
- NetCore submodule provides generic sync abstractions: `INetworkService` (connection lifecycle —
  `StartFromLobby`, `Stop`, `State`), `INetworkStateSource<T>` / `INetworkStateSink<T>` (capture
  local state / apply remote state), `INetworkOwnershipGate` (owner vs non-owner checks)
- `PurrNetService` implements `INetworkService`. Adapters extend `PurrNetStateSyncAdapterBase`
  (owner captures + broadcasts via `[ObserversRpc]`, non-owners apply the latest received state
  each `FixedUpdate`) — e.g. `PurrNetPlayerStateSyncAdapter` for player position/input,
  `NetworkPuzzleRoomController` for puzzle-solve confirmation
- RPCs are direct PurrNet attributes on `NetworkBehaviour` subclasses — `[ServerRpc(requireOwnership:
  false)]`, `[ObserversRpc(excludeOwner: true)]` — not a bus/wrapper indirection layer
- Dialogue system supports multiplayer via `DialogueContext.Mode`, role-based resolution (Initiator/Companion), and transient Yarn variables
- Puzzle state is server-confirmed, not purely client-authoritative: every client evaluates
  `PuzzleController` locally for responsive feedback, but reward-firing/progression subscribes to
  `PuzzleController.OnSolvedConfirmed`, which only fires once the server has confirmed the solve
  via `NetworkPuzzleRoomController`. This is a deliberate simplicity/responsiveness tradeoff for a
  co-op game with no anti-cheat requirement, not a security boundary.

---

## Input System

- Unity's new Input System package (not legacy `Input.GetKey`)
- `InputReader` class wraps the generated input actions
- Subscribe via `_inputReader.SubscribePerformed("ActionName", callback)` in init
- Unsubscribe via `_inputReader.UnsubscribePerformed(...)` in shutdown/dispose
- `InputReader.InputMode` for mode switching (Gameplay, UI, etc.)