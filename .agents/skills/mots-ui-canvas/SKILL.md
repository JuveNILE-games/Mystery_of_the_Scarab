---
name: mots-ui-canvas
description: >
  Mystery of the Scarab (JuveNILE Games) project-specific skill for setting up
  UI canvases using the Navigation + Juice/UIAnimation architecture. Use this
  skill whenever Michael asks to build or set up any UI element in the Scarab
  project: menus, HUDs, overlays, popups, in-world panels, pause screens,
  inventory screens, dialogue boxes, ability wheels, boss-health bars, or any
  other screen/overlay. The skill decides the correct canvas type (ScreenCanvas,
  OverlayCanvas, PopupOverlayCanvas, WorldSpaceCanvas), wires up the matching
  Definition ScriptableObject, and configures UIAnimationComponent or
  UIToolkitAnimationComponent for open/close animation. Trigger even for vague
  requests like "add a HUD" or "make an inventory screen" — always read this
  skill before producing any canvas-related code or setup instructions.
---

# MoTS UI Canvas Setup Skill

## Architecture at a Glance

All canvases extend `BaseCanvas` (`Core.Systems.Navigation.Canvases`), which
provides the async open/close lifecycle, animation integration, service
injection, and pooling hooks. The canvas type and its paired Definition
ScriptableObject determine how the NavigationService manages it.

```
BaseCanvas
├── ScreenCanvas         – full-screen replacement (MainMenu, Gameplay, Pause)
│   └── paired with: ScreenDefinition
└── OverlayCanvas        – layered on top of a screen
    ├── paired with: OverlayDefinition
    ├── PopupOverlayCanvas  – modal dialog / confirmation popup
    └── WorldSpaceCanvas    – in-world UIDocument panel (signs, nameplates)
```

---

## Step 1 — Choose the Canvas Type

Answer these questions in order; stop at the first match.

| Question | Yes → Canvas Type |
|---|---|
| Does it **replace the entire screen**? (main menu, gameplay screen, pause screen, credits) | `ScreenCanvas` |
| Is it **always visible** regardless of screen? (HUD, minimap, health bar, stamina bar) | `OverlayCanvas` — `allowedForAllScreens = true`, `inputMode = KeepPrevious` |
| Does it **block input** and must be explicitly closed? (inventory, map, options modal) | `OverlayCanvas` — `inputMode = UIOnly` (or `GameAndUI`) |
| Is it a **yes/no / confirmation dialog**? | `PopupOverlayCanvas` (extends `OverlayCanvas`) |
| Is it **attached to a point in the game world**? (interactable prompt, NPC nameplate, puzzle sign) | `WorldSpaceCanvas` (extends `OverlayCanvas`) |

When in doubt between Overlay types, ask: *does the user see this while still playing?* → HUD-style. *Does it stop play to ask something?* → modal or popup.

---

## Step 2 — Create the Definition ScriptableObject

### ScreenDefinition (`Core/Navigation/Screen Definition`)
Key fields to configure:
- `inputMode` — usually `GameAndUI` for gameplay screen, `UIOnly` for menus.
- `allowedOverlays` — list every `OverlayDefinition` that can appear on top of this screen.
- `incomingScreens` + `allowedMode` — which screens can navigate here and optional transition.

### OverlayDefinition (`Core/Navigation/Overlay Definition`)
Key fields:
- `allowedForAllScreens` — true for HUD-style overlays.
- `inputMode` — `KeepPrevious` (HUD), `UIOnly` (modal), `GameAndUI` (non-blocking panels).
- `canBeClosedByUser` — false for forced overlays (cutscene bars, loading screen).
- `opensAutomatically` — true if NavigationService should auto-show it.
- `conditions` — optional `NavigationCondition` assets guarding open logic.

---

## Step 3 — Canvas Component Setup

### 3a. ScreenCanvas Prefab

```
[GameObject]
  ├── Canvas (Render Mode: Screen Space - Camera, sort order as needed)
  ├── CanvasScaler (Scale With Screen Size, reference 1920×1080)
  ├── GraphicRaycaster
  ├── ScreenCanvas               ← assign ScreenDefinition here
  │     animated = true/false
  ├── UIAnimationComponent       ← if UGUI animated (see Step 4)
  └── [UI hierarchy]
```

Minimal code subclass (only if custom logic needed):
```csharp
public class PauseScreenCanvas : ScreenCanvas
{
    protected override void OnServicesInjected()
    {
        base.OnServicesInjected();
        // bind UI buttons, etc.
    }
}
```

### 3b. OverlayCanvas Prefab

```
[GameObject]
  ├── Canvas (Render Mode: Screen Space - Overlay, higher sort order than screens)
  ├── CanvasScaler
  ├── GraphicRaycaster
  ├── OverlayCanvas              ← assign OverlayDefinition here
  │     animated = true/false
  ├── UIAnimationComponent       ← if animated
  └── [UI hierarchy]
```

Opening/closing from code:
```csharp
// Via navigation service (preferred)
navService.ShowOverlay(myOverlayDefinition);
navService.CloseOverlay(myOverlayDefinition);

// Via the canvas component directly (e.g. from a button)
overlayCanvas.ShowOverlay();
overlayCanvas.CloseOverlay();

// Async (awaitable)
await overlayCanvas.ShowOverlayAsync(cancellationToken);
```

### 3c. PopupOverlayCanvas

Extends `OverlayCanvas`. Add `CanvasGroup` (required). Subclass it:
```csharp
public class ConfirmPopup : PopupOverlayCanvas
{
    public void ShowConfirm(string title, string msg, Action onYes, Action onNo)
    {
        Setup(title, msg, PopupType.Warning);
        AddOnAccept(onYes);
        AddOnCancel(onNo);
        ShowOverlay();
    }
}
```

### 3d. WorldSpaceCanvas

```
[GameObject]  ← position in world
  ├── Canvas (kept but disabled by WorldSpaceCanvas — don't remove it)
  ├── MeshFilter
  ├── MeshRenderer
  ├── UIDocument                 ← assign VisualTreeAsset and PanelSettings
  ├── WorldSpaceCanvas
  │     mode: Native | RenderTexture
  │     panelWidth / panelHeight / pixelsPerUnit
  │     assign OverlayDefinition
  └── UIToolkitAnimationComponent  ← for animated open/close
```

Use **Native** mode for simple overlays that don't need depth/transparency tricks.
Use **RenderTexture** mode when the panel must render behind other 3D objects or needs custom shading.

---

## Step 4 — Wiring UIAnimation

### Which component?

| Canvas renders via… | Animation Component |
|---|---|
| UGUI (Canvas + `Image`, `Text`, etc.) | `UIAnimationComponent` |
| UIDocument / UIToolkit | `UIToolkitAnimationComponent` |

Both implement `IUIAnimationComponent`. `BaseCanvas.animationComponent` is auto-resolved via `GetComponent<IUIAnimationComponent>()` in `Initialize()`.

### Enabling animation on a canvas

1. Set `BaseCanvas.animated = true` on the canvas component.
2. Add `UIAnimationComponent` **or** `UIToolkitAnimationComponent` to the **same GameObject**.
3. Create a `UIAnimationDefinition` asset (`Core/UI/Animation Definition`).
4. Assign it to the component's `animationDefinition` field.
5. Leave `playOnAwake` and `playOnEnable` **false** — the canvas drives playback via `IUIAnimationComponent.PlayAsync(playBackwards)`.

### Configuring UIAnimationDefinition

A definition holds a list of `UIAnimationStep`s, each with one or more `UIAnimationClip`s.

Common open animations (forward play):
```
Step: "Fade In"  →  Clip: CanvasGroupFade  targetAlpha=1  duration=0.25  ease=OutQuad
Step: "Scale Up" →  Clip: Scale  targetScale=(1,1,1)  duration=0.2  ease=OutBack
```

Close animation uses `playBackwards: true` — the same definition plays in reverse.
For an asymmetric close, override `DoCloseAnimatedAsync` in a subclass.

### UIAnimationTriggerOverride

To set a non-default *start* value (e.g. scale from 0 on open), add a
`ScaleOverride` or `CanvasAlphaOverride` component alongside
`UIToolkitAnimationComponent`. These implement `IVector3Override` /
`IFloatOverride` and are picked up by the animation service before the first
play to set the initial cache state.

---

## Step 5 — Service Injection & Scene Setup

Every canvas must have services injected before `Initialize()` is called.
The easiest way is a **single `CanvasServiceInjector`** in the scene:

```
[CanvasManager] (scene object)
  └── CanvasServiceInjector
        injectOnAwake = true
        canvases = []   ← leave empty to auto-discover via FindObjectsByType
```

If a canvas is spawned at runtime (e.g. from a pool), call manually:
```csharp
canvas.InjectServices(serviceScope);
canvas.Initialize();
```

---

## Step 6 — Registering with NavigationService

`CanvasServiceInjector.InjectServices()` auto-registers `ScreenCanvas` and
`OverlayCanvas` instances with the `NavigationService`. No extra code needed
as long as the Definition fields are assigned.

Navigating between screens:
```csharp
navService.NavigateToScreen(myScreenDefinition, NavigationTriggerCause.ButtonPress);
```

---

## Common Patterns

### HUD that persists across all gameplay screens
```
OverlayCanvas
  OverlayDefinition: allowedForAllScreens=true, inputMode=KeepPrevious, opensAutomatically=true
  animated = false  (or a subtle fade-in UIAnimationComponent)
```

### Pause menu (opens on P key, blocks game input)
```
OverlayCanvas
  OverlayDefinition: inputMode=UIOnly, allowedForAllScreens=false
    → add to ScreenDefinition.allowedOverlays on the gameplay screen
  UIAnimationComponent: slide-in or scale-in UIAnimationDefinition
```

### Inventory screen (its own full screen, back-button returns to gameplay)
```
ScreenCanvas  
  ScreenDefinition: inputMode=UIOnly
    incomingScreens: GameplayScreen → (optional transition definition)
```

### Confirmation popup ("Are you sure?")
```
PopupOverlayCanvas (+ CanvasGroup required)
  OverlayDefinition: inputMode=UIOnly, canBeClosedByUser=true
  UIAnimationComponent: quick scale punch or fade
```

### In-world interactable prompt (UIToolkit on a quad)
```
WorldSpaceCanvas (mode=RenderTexture)
  OverlayDefinition: allowedForAllScreens=true, inputMode=KeepPrevious
  UIToolkitAnimationComponent: CanvasGroupFade or ScaleOverride=0 → Scale to 1
```

---

## Reference Files

- `references/canvas-api.md` — Full API surface of BaseCanvas, ScreenCanvas, OverlayCanvas, WorldSpaceCanvas
- `references/animation-types.md` — All UIAnimationType values with handler notes

Read these when you need exact field names or are implementing a non-standard pattern.
