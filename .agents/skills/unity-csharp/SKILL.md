# Unity C# Skill

Activate when writing or reviewing C# code for Unity projects.

## Applicable Contexts

- Creating or editing `.cs` files in a Unity project
- Reviewing MonoBehaviour lifecycle, Update loops, async patterns
- Evaluating performance and GC pressure in gameplay code
- Setting up Input System, dependency injection, or assembly definitions

## Instructions

Follow the guidelines in `Docs/AI_Docs/unity-csharp-guidelines` for:
- MonoBehaviour discipline (§2)
- Update loop rules (§3)
- Performance and GC avoidance (§4)
- Structs vs classes (§5)
- Async patterns — UniTask/Awaitable/coroutines (§6)
- Input System (§8)
- SOLID principles for gameplay (§9)
- Anti-patterns to avoid (§10)
- C# language baseline — C# 9 (§11)
- Assembly definitions (§12)

### Project-Specific Overrides

This project uses a custom Service Locator with `[Inject]` attribute instead of VContainer.
See the root `AGENTS.md` for the actual DI patterns used.

### Key Rules

1. **Fake-null trap**: Compare `UnityEngine.Object` with `==`/`!=` only — never `is null`, `?.`, `??`
2. **No coroutines**: Use UniTask for all async work
3. **Cache component refs**: In `Awake()` or `[SerializeField]` — never in Update
4. **Remove empty Update/FixedUpdate/LateUpdate**: They still incur native→managed call overhead
5. **Pool everything**: Use `PoolService` — never `Instantiate`/`Destroy` per frame
