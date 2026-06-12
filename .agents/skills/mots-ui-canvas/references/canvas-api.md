# Canvas API Reference — Mystery of the Scarab

## BaseCanvas (abstract)

Namespace: `Core.Systems.Navigation.Canvases`

### Inspector Fields
| Field | Type | Notes |
|---|---|---|
| `animated` | bool | If true, open/close use animation path |
| `onOpen` | UnityEvent | Fired after open animation completes |
| `onClose` | UnityEvent | Fired before close animation starts |
| `toggleGameObjects` | List<GameObject> | Enabled on open, disabled on close |

### Key Properties
- `bool IsOpen` — read-only current state

### Public API
```csharp
void Initialize()                             // call once after service injection
void InjectServices(IServiceScope scope)      // must be called before Initialize

// Synchronous (fire-and-forget internally)
void Open(bool animate)
void Close(bool animate)

// Async (awaitable, with CancellationToken support)
UniTask OpenAsync(bool animate, CancellationToken ct = default)
UniTask CloseAsync(bool animate, CancellationToken ct = default)

// Navigation-aware helpers
bool OpenIfPossible(NavigationService nav, out string reason)
UniTask<bool> OpenIfPossibleAsync(NavigationService nav, CancellationToken ct = default)
bool CloseIfDisallowed(NavigationService nav, out string reason)
```

### Abstract / Virtual hooks for subclassing
```csharp
protected abstract void OnInitialize();
protected abstract void DoOpenInstant();
protected virtual void DoCloseInstant() { }
protected virtual UniTask DoOpenAnimatedAsync(CancellationToken ct)   // uses IUIAnimationComponent if present
protected virtual UniTask DoCloseAnimatedAsync(CancellationToken ct)  // plays backwards

// Legacy callback hooks (used only when no IUIAnimationComponent is present)
protected virtual void OnOpenAnimated(Action onComplete)
protected virtual void OnCloseAnimated(Action onComplete)
```

### Pooling
Implements `IPoolable`:
- `OnReturnToPool()` — cancels active animations
- `OnTakeFromPool()` — re-initializes if needed

---

## ScreenCanvas

Extends `BaseCanvas`.

### Inspector Fields
| Field | Type | Notes |
|---|---|---|
| `definition` | ScreenDefinition | Required — drives NavigationService registration |

### Key API
```csharp
void NavigateToThis()   // calls navService.NavigateToScreen(definition, ButtonPress)
```

### Subclassing pattern
```csharp
public class MyScreen : ScreenCanvas
{
    protected override void OnServicesInjected()
    {
        // Bind buttons, subscribe to events after services available
    }
}
```

---

## OverlayCanvas

Extends `BaseCanvas`.

### Inspector Fields
| Field | Type | Notes |
|---|---|---|
| `_definition` (private, serialized) | OverlayDefinition | Accessed via `Definition` property |

### Key API
```csharp
OverlayDefinition Definition { get; }

void ShowOverlay()           // routes through navService if injected
void CloseOverlay()
UniTask ShowOverlayAsync(CancellationToken ct = default)
UniTask CloseOverlayAsync(CancellationToken ct = default)
```

### Subclassing — async animation hooks
```csharp
protected virtual UniTask OnOpenAnimatedAsync(CancellationToken ct)   // default: no-op
protected virtual UniTask OnCloseAnimatedAsync(CancellationToken ct)  // default: no-op
```

---

## PopupOverlayCanvas

Extends `OverlayCanvas`. **Requires `CanvasGroup`.**

### Inspector Fields
| Field | Notes |
|---|---|
| `popupType` | `PopupType` enum (Info / Warning / Error / Confirm) |
| `contentRoot` | RectTransform |
| `titleText` | TMP_Text |
| `messageText` | TMP_Text |
| `closeButton` | Button — auto-wires OnCloseClicked |
| `buttonContainer` | Transform — parent for dynamic buttons |
| `animationDuration` | float (legacy — prefer UIAnimationComponent) |
| `canvasGroup` | CanvasGroup |

### Key API
```csharp
void Setup(string title, string message, PopupType type = Info)

// INavigationPopup
void Show()
void AddOnAccept(Action callback)
void AddOnCancel(Action callback)
```

### Callback cleanup
`DoCloseInstant` and `DoCloseAnimatedAsync` both call `CleanupCallbacks()` which nulls accept/cancel callbacks and destroys dynamic buttons.

---

## WorldSpaceCanvas

Extends `OverlayCanvas`. Requires `UIDocument`, `MeshFilter`, `MeshRenderer`.

### Inspector Fields
| Field | Type | Notes |
|---|---|---|
| `mode` | `WorldSpaceMode` | `Native` or `RenderTexture` |
| `panelWidth` / `panelHeight` | int | RT resolution |
| `panelScale` | float | UIToolkit scale |
| `pixelsPerUnit` | float | Controls world-space physical size |
| `visualTreeAsset` | VisualTreeAsset | Assigned to UIDocument |
| `panelSettingsTemplate` | PanelSettings | Cloned at runtime for RT mode |
| `renderTextureTemplate` | RenderTexture | Descriptor used for RT creation |

### Behaviour differences from OverlayCanvas
- The UGUI `Canvas` component is **disabled** — WorldSpaceCanvas owns the UGUI Canvas slot (required by BaseCanvas) but immediately disables it.
- In `RenderTexture` mode a `Quad` mesh + runtime `Material` + runtime `RenderTexture` are created at first `Initialize()` and destroyed on `OnDestroy`.
- `UpdateContentVisibility(bool)` toggles `MeshRenderer.enabled` (RT) or `rootVisualElement.style.display` (Native).

---

## CanvasServiceInjector

Utility component that auto-injects services into all `BaseCanvas` instances.

### Inspector Fields
| Field | Notes |
|---|---|
| `injectOnAwake` | bool (default true) |
| `injectOnStart` | bool |
| `canvases` | List<BaseCanvas> — leave empty for auto-discovery |

### What it does
1. Creates a `ServiceScope` from `ServiceLocator.Global`.
2. Calls `canvas.InjectServices(scope)` on each canvas.
3. Calls `canvas.Initialize()`.
4. Registers `ScreenCanvas` and `OverlayCanvas` instances with `NavigationService`.

---

## OverlayDefinition — Field Reference

```csharp
bool allowedForAllScreens;              // HUDs: true
bool opensAutomatically;               // auto-shown by NavigationService
bool canBeClosedByUser;                // false = forced (loading, cutscene)
InputReader.InputMode inputMode;       // KeepPrevious | UIOnly | GameAndUI | GameOnly
List<NavigationCondition> conditions;  // guard conditions
TransitionDefinition openTransition;   
TransitionDefinition closeTransition;
```

---

## ScreenDefinition — Field Reference

```csharp
InputReader.InputMode inputMode;
AllowedScreensMode allowedMode;        // AllowOnlySpecific | AllowAllExcept
List<IncomingScreenSetup> incomingScreens;  // { screen, transition }
List<NavigationCondition> conditions;
List<OverlayDefinition> allowedOverlays;   // overlays allowed on this screen
UnityEvent onOpen;
UnityEvent onClose;
```
