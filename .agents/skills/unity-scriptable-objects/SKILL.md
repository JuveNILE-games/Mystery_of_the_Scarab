# Unity ScriptableObjects Skill

Activate when creating, editing, or reviewing ScriptableObject usage in Unity.

## Applicable Contexts

- Creating new ScriptableObject types (configs, variables, event channels, runtime sets)
- Reviewing SO lifecycle and serialization patterns
- Debugging domain reload / play-mode state leaks
- Evaluating whether something should be an SO vs MonoBehaviour vs plain C#

## Instructions

Follow the guidelines in `Docs/AI_Docs/unity-scriptable-objects-guidelines.md` for:
- When to use an SO (§1)
- Anti-cargo-cult — avoiding overuse (§2)
- Configuration asset pattern (§3)
- Variable asset pattern (§4)
- Event channel / Hipple pattern (§5)
- Runtime set pattern (§6)
- Lifecycle pitfalls — OnEnable timing, domain reload, editor state leaks (§7)
- Decision tree for choosing the right pattern (§8)
- Anti-patterns (§9)
- Editor support and CreateAssetMenu conventions (§10)
- Testing SOs in isolation (§11)

### Project-Specific Patterns

This project uses specific SO conventions:

1. **Menu path**: `[CreateAssetMenu(menuName = "Core/<Category>/<Name>")]`
2. **Read-only props**: `[field: SerializeField] public T Prop { get; private set; }`
3. **Inspector organization**: Use `[Header("...")]` and `[Tooltip("...")]` on config fields
4. **Soap events**: The project uses the Soap package for SO event channels — `ScriptableEventString`, `ScriptableEventInt`, etc.
5. **Public fields on data classes**: Acceptable on `[Serializable]` DTOs (`SoundData`, `AudioServiceConfig`) but NOT on services or MonoBehaviours
6. **No scene references from SOs**: Use registration patterns or service locator instead
