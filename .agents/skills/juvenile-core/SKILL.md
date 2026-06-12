# JuveNILE.Core Framework Skill

Activate when working with the JuveNILE.Core framework systems.

## Applicable Contexts

- Registering new services in installers
- Creating new boot phases or modules
- Working with the Service Locator, DI, or service lifecycle
- Adding or modifying Core systems (Audio, Dialogue, StateMachine, Navigation, etc.)
- Debugging service resolution, initialization order, or shutdown

## Instructions

### Service Registration

Always register services through an `IInstaller` implementation:

```csharp
// Interface → Implementation (constructor auto-resolved)
locator.Register<IMyService, MyService>();

// Alias for secondary interface
locator.Register<ISecondary>(s => s.Get<MyService>());

// Factory for config objects
locator.Register<MyConfig>(_ => coreConfig.MyConfig);
```

Never manually instantiate and register: `locator.Register(new MyService())` — this bypasses constructor auto-injection.

### Service Lifecycle

Implement `IInitializableService` for services needing async startup:
- `InitializationPriority` — lower = earlier. Check existing services for priority slots
- `InitializeAsync` — subscribe to events, create pools, load assets
- `ShutdownAsync` — unsubscribe, clear state

Implement `IDisposable` for cleanup:
- Cancel `CancellationTokenSource`
- Unsubscribe from events
- Null out references
- Called during `ServiceLocator.ShutdownAsync()`

### Dependency Injection

**Pure C# services** → Constructor injection with `_underscore` prefix:
```csharp
private readonly ILoggerService _logger;
public MyService(ILoggerService logger) { _logger = logger; }
```

**MonoBehaviours** → `[Inject]` attribute:
```csharp
[Inject] private IMyService _myService;
```
Processed by `MonoBehaviourInjection.InjectAllMonoBehaviours()` on scene load.

### Boot Pipeline

- Phases implement `IBootPhase`, sorted via `[DependsOn(typeof(...))]`
- Modules implement `GameModule` and register in `CoreConfiguration.Modules`
- Never hardcode execution order — declare dependencies explicitly

### Async

- Use `UniTask` everywhere — never `System.Threading.Tasks.Task` or coroutines
- Return `UniTask` / `UniTask<T>`, use `UniTaskVoid` for fire-and-forget
- Pass `CancellationToken` through all async chains
- Catch `OperationCanceledException` separately (it's normal flow)

### Logging

```csharp
_logger?.Log(this, "[SystemName] message");
_logger?.LogWarning(this, "...");
_logger?.LogError(this, "...");
```

Static shorthand: `Log.Info(this, "...")`, `Log.Warning(...)`, `Log.Error(...)`
`Debug.Log` is only acceptable pre-bootstrap (before the logger exists).

### Event Systems

- **Signals** (`IEventBus`): For code-to-code decoupled communication
  ```csharp
  _signalsBus.Publish(new MySignal(data));
  _signalsBus.Subscribe<MySignal>(handler);
  ```
- **Soap SO events**: For inspector-wirable, cross-scene events
  ```csharp
  _myEvent.Raise(payload);  // ScriptableEvent asset
  ```

### Architecture Documentation

Consult the Obsidian vault at `G:\Mijn Drive\Notes\JuveNILE Games\Framework\Core\` before modifying unfamiliar systems. 22 system docs are maintained there.
