# UIAnimation Reference — Mystery of the Scarab

## UIAnimationType Enum

```csharp
namespace Core.Systems.Juice.UIAnimation.Enums

// Transform
Scale               // DOTween DOScale — targets Transform.localScale
Position            // DOTween DOMove — world position
LocalPosition       // DOTween DOLocalMove
Rotation            // DOTween DORotate — world
LocalRotation       // DOTween DOLocalRotate

// Fade
Fade                // DOTween DOFade on CanvasGroup, Image, or SpriteRenderer
CanvasGroupFade     // CanvasGroupFadeAnimationHandler — explicit CanvasGroup.alpha

// Color
ImageColor          // Image.DOColor
TextColor           // TMP_Text.DOColor
SpriteColor         // SpriteRenderer.DOColor

// RectTransform
AnchoredPosition    // RectTransform.DOAnchorPos
SizeDelta           // RectTransform.DOSizeDelta

// Effects
PunchScale          // DOTween DOPunchScale
PunchPosition       // DOTween DOPunchPosition
PunchRotation       // DOTween DOPunchRotation
ShakePosition       // DOTween DOShakePosition
ShakeRotation       // DOTween DOShakeRotation

// Special
Custom              // Custom handler via UIAnimationHandlerRegistry
```

---

## UIAnimationClip — Field Reference

```csharp
UIAnimationType animationType;
float duration;               // tween duration in seconds
float delay;                  // delay before this clip within its step
Ease easeType;                // DOTween Ease enum
AnimationCurve customCurve;   // used when easeType = Custom

// Transform
Vector3 targetScale;          // for Scale
Vector3 targetPosition;       // for Position types
Vector3 targetRotation;
bool useLocalPosition;
bool useLocalRotation;

// Fade
float targetAlpha;            // [0,1] for Fade / CanvasGroupFade

// Color
Color targetColor;

// RectTransform
Vector2 targetAnchoredPosition;
Vector2 targetSizeDelta;

// Punch / Shake
Vector3 punchStrength;
int vibrato;
float elasticity;

bool snapping;                // snap to pixel for position tweens
bool relative;                // target is relative to current value
```

---

## UIAnimationStep — Field Reference

```csharp
string stepName;
AnimationExecutionMode executionMode;  // Sequence | Parallel
List<UIAnimationClip> clips;
float delayBefore;
float delayAfter;
```

`Sequence` = clips run one after the other.  
`Parallel` = all clips start simultaneously; step duration = longest clip.

---

## UIAnimationDefinition — Field Reference

```csharp
List<UIAnimationStep> steps;
float delayBeforeStart;
bool ignoreTimeScale;           // useful for pause menus
UpdateType updateType;          // Normal | Late | Fixed | Manual
bool loop;
int loopCount;                  // -1 = infinite
LoopType loopType;              // Restart | Yoyo | Incremental
```

---

## Overrides (initial-state setters)

Overrides implement `IFloatOverride` or `IVector3Override` and tell the
animation service what value to *start from* (rather than the current live
value) when the first `PlayAsync` runs.

| Component | Interface | Field |
|---|---|---|
| `CanvasAlphaOverride` | `IFloatOverride` | `alpha` — sets `CanvasGroup.alpha` before anim |
| `ScaleOverride` | `IVector3Override` | `scale` — sets `Transform.localScale` before anim |
| `PositionOverride` | `IVector3Override` | world position |
| `RotationOverride` | `IVector3Override` | euler angles |

**Usage pattern for a canvas that starts invisible and fades in:**
1. Add `CanvasGroup` to the canvas root.
2. Add `CanvasAlphaOverride`, set `alpha = 0`.
3. Add `UIToolkitAnimationComponent` (or `UIAnimationComponent`).
4. `UIAnimationDefinition`: one step, one clip — `CanvasGroupFade`, `targetAlpha = 1`, duration 0.25.
5. `resetBeforePlay = false` (the override already set the start state).

---

## IUIAnimationComponent Interface

```csharp
bool IsPlaying { get; }
UniTask PlayAsync(bool playBackwards = false, CancellationToken ct = default);
void Play(bool playBackwards = false);   // fire-and-forget
void Stop(bool complete = false);
void ResetToInitialState();
```

`BaseCanvas` auto-resolves this via `GetComponent<IUIAnimationComponent>()`.
`DoOpenAnimatedAsync` calls `PlayAsync(playBackwards: false)`.
`DoCloseAnimatedAsync` calls `PlayAsync(playBackwards: true)`.

---

## UIAnimationComponent vs UIToolkitAnimationComponent

| | `UIAnimationComponent` | `UIToolkitAnimationComponent` |
|---|---|---|
| Target | UGUI elements (Image, TMP, CanvasGroup) | UIDocument root |
| Requires | Nothing extra | `UIDocument` on same GO |
| Initial state timing | Immediate | Waits `WaitForEndOfFrame` (layout resolve) |
| Event system | `IPointerEnter/Exit/Click/Select` hooks | None (canvas drives it) |
| `playOnHover` | Supported | Not supported |

---

## NavigationService — Animation Integration

When `NavigationService.ShowOverlay()` or `NavigateToScreen()` is called, it
delegates to `BaseCanvas.OpenAsync(animated)` or `CloseAsync(animated)`.

The `animated` field on the canvas component is the master switch:
- `true` → `DoOpenAnimatedAsync` → uses `IUIAnimationComponent` if present, else legacy callbacks.
- `false` → `DoOpenInstant` → no tween, just `gameObject.SetActive(true)` + `canvas.enabled = true`.

For overlays with `TransitionDefinition` set on `OverlayDefinition.openTransition`, the `TransitionRunner` handles screen-level transitions separately from the canvas's own animation.
