# DialogueService — Architecture Design

> Researched against the existing codebase: ServiceLocator, SOAP ScriptableEvents,
> Bindables, NavigationService (OverlayDefinition), CharacterData, UIToolkit, UniTask,
> DOTween/UIAnimationHandle, and the existing DialogueBox overlay CSS.

> **Revision notes:** This document has been audited for SOLID, DRY, concurrency, and
> contract correctness. All findings have been resolved in-place. Changed sections are
> marked with a `[revised]` tag in their heading for traceability.

-----

## 1. Goals & Constraints

|Goal                                     |How it's met                                                                               |
|-----------------------------------------|-------------------------------------------------------------------------------------------|
|YarnSpinner-driven content               |`DialogueService` wraps YarnSpinner's `DialogueRunner`                                     |
|Swappable view (Box → Bubble)            |`IDialoguePresenter` strategy pattern                                                      |
|Two-participant conversations (any combo)|`DialogueContext` holds two named seats — Right and Left — regardless of what occupies them|
|Dynamic character data (character switch)|`ISpeakerResolver` chain in `ILineResolver` evaluated lazily per line                      |
|Observer always on the right             |`IsLeft` derived from seat identity via `SeatId` enum, not slot name or network role       |
|Typewriter effect                        |Standalone `TypewriterEffect` behind `ITypewriterEffect` (UniTask + DOTween)               |
|Events                                   |SOAP `ScriptableEvent<T>` assets behind `IDialogueEventBus` + YarnSpinner custom commands  |
|Unity Timeline                           |`DialogueTrack` / `DialoguePlayableAsset`                                                  |
|Fits existing patterns                   |`IInitializableService`, `IInstaller`, registered via ServiceLocator                       |

-----

## 2. High-Level Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                        GAME CODE / TIMELINE                     │
│  dialogueService.StartAsync(nodeId, context)                    │
└────────────────────────────┬────────────────────────────────────┘
                             │
                ┌────────────▼────────────┐
                │     IDialogueService    │  ← registered in ServiceLocator
                │     DialogueService     │
                │                         │
                │  YarnSpinner            │
                │  DialogueRunner ────────┼──► YarnProject (.yarn files)
                │  LocalizedLine          │
                │  OptionSet              │
                │                         │
                │  ILineResolver          │
                │  IDialogueCommandReg.   │
                │  DialogueContext        │
                └────┬──────────┬─────────┘
                     │          │
          ┌──────────▼──┐  ┌───▼──────────────────┐
          │ IDialogue-  │  │  IDialoguePresenter   │
          │ EventBus    │  │  (strategy)           │
          │ (SO assets) │  │  ┌────────────────┐  │
          └─────────────┘  │  │DialogueBoxPres.│  │ ← NOW
                            │  └────────────────┘  │
                            │  ┌──────────────────┐ │
                            │  │SpeechBubblePres. │ │ ← LATER
                            │  └──────────────────┘ │
                            └───────────────────────┘
```

-----

## 3. Data Structures

### 3.1 `DialogueLine`

Thin data-transfer object passed between layers.

```csharp
public readonly struct DialogueLine
{
    public readonly string        RawText;       // after markup stripping
    public readonly string        CharacterName; // from Yarn metadata
    public readonly CharacterData Character;     // resolved CharacterData SO
    public readonly EmotionType   Emotion;       // parsed from Yarn tag e.g. [happy]
    public readonly bool          IsLeft;        // side hint for visual novel layout

    public DialogueLine(
        string        rawText,
        string        characterName,
        CharacterData character,
        EmotionType   emotion,
        bool          isLeft)
    {
        RawText       = rawText;
        CharacterName = characterName;
        Character     = character;
        Emotion       = emotion;
        IsLeft        = isLeft;
    }
}
```

### 3.2 `SeatId` [revised]

A type-safe identifier for the two dialogue seats. Replaces the magic strings `"right"`
and `"left"` that were previously scattered across `HandleSwitchSeat`, Yarn command
parsing, and `ResolveLine`. All three sites now share one type.

```csharp
public enum SeatId
{
    Right,  // the observer's seat — always rendered on the right
    Left    // the other party's seat — always rendered on the left
}
```

### 3.3 `DialogueContext` [revised]

Defines the two participants in a conversation — nothing more. The system does not know
or care whether a seat is occupied by a player, an NPC, a networked entity, or left empty.

```csharp
public class DialogueContext
{
    // ── The two seats ─────────────────────────────────────────────
    //
    // RIGHT — the observer's seat. Always rendered on the right of the
    // dialogue box on the screen that owns this context. In a networked
    // game each client independently sets RightSpeaker to whoever they
    // are controlling, so both clients see themselves on the right.
    //
    // LEFT  — the other party. Any character that is not the observer.
    //
    // Either seat may be null:
    //   Both set   → two-person conversation (player ↔ NPC, NPC ↔ NPC, etc.)
    //   Only Right → monologue / inner thoughts from the observer's character
    //   Only Left  → narration / cutscene line with no observer involvement
    //
    // Seats are internal-set: only DialogueContextFactory and
    // DialogueCommandRegistry.HandleSwitchSeat may mutate them.
    // External callers use DialogueContextFactory.Create() to construct
    // a context and AddAlias() to extend it.
    public CharacterData RightSpeaker { get; internal set; }
    public CharacterData LeftSpeaker  { get; internal set; }

    // ── Speaker alias map ─────────────────────────────────────────
    // Maps names used in .yarn files → a resolver that returns the
    // correct CharacterData at line-display time (evaluated lazily
    // so mid-conversation seat swaps are picked up automatically).
    //
    // Exposed as read-only to prevent callers from stomping the built-in
    // "Right" / "Left" aliases. Use AddAlias() to register characters.
    private readonly Dictionary<string, ISpeakerResolver> _speakerAliases = new();
    public IReadOnlyDictionary<string, ISpeakerResolver> SpeakerAliases => _speakerAliases;

    /// <summary>
    /// Register or overwrite an alias for use in .yarn files.
    /// The built-in "Right" and "Left" aliases are pre-populated by
    /// DialogueContextFactory and can be overridden here if needed.
    /// </summary>
    public void AddAlias(string name, ISpeakerResolver resolver)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Alias name must not be empty.", nameof(name));
        _speakerAliases[name] = resolver ?? throw new ArgumentNullException(nameof(resolver));
    }

    // ── Options ───────────────────────────────────────────────────

    // When true, a Dialogue_ChoicePending event is raised and the
    // service waits for an external ConfirmChoice(index) call before
    // proceeding. Useful for networked or split-screen play where
    // both participants should see the selection before it commits.
    public bool RequireChoiceConfirmation { get; set; }
}
```

> **Why ScriptableObjects?**
> `CharacterData` already exists in `Definitions/Character/CharacterData.cs` with name,
> portraits, and emotion entries. No new data model needed.
>
> **Why was `StartNode` removed?**
> The previous design had a `StartNode` property on `DialogueContext` and a `nodeName`
> parameter on `StartAsync` with no defined precedence between them. Since `StartAsync`
> is the entry point and `nodeName` is always required there, `StartNode` was redundant
> dead API. The node to play is always passed explicitly to `StartAsync`.

### 3.4 `ISpeakerResolver` — Dynamic Character Resolution

The resolver is called **at the moment a line is displayed**, so a seat swap mid-conversation
is picked up automatically without touching the `.yarn` file.

```csharp
/// <summary>
/// Resolves which CharacterData to display for a speaker alias
/// at the time a line is presented. Evaluated lazily per-line.
/// </summary>
public interface ISpeakerResolver
{
    CharacterData Resolve(DialogueContext context);
}

// ── Built-in implementations ───────────────────────────────────────────

/// Always returns the CharacterData in the Right seat (the observer).
public class RightSeatResolver : ISpeakerResolver
{
    public CharacterData Resolve(DialogueContext context)
        => context.RightSpeaker;
}

/// Always returns the CharacterData in the Left seat (the other party).
public class LeftSeatResolver : ISpeakerResolver
{
    public CharacterData Resolve(DialogueContext context)
        => context.LeftSpeaker;
}

/// A pinned, fixed character — the same CharacterData every time.
/// Use for NPCs or story characters whose identity never changes.
public class FixedSpeakerResolver : ISpeakerResolver
{
    private readonly CharacterData _data;
    public FixedSpeakerResolver(CharacterData data) => _data = data;
    public CharacterData Resolve(DialogueContext context) => _data;
}

/// Escape hatch — wire any custom logic with a lambda.
/// Example: narrator whose portrait changes based on game state.
public class DelegateSpeakerResolver : ISpeakerResolver
{
    private readonly Func<DialogueContext, CharacterData> _resolve;
    public DelegateSpeakerResolver(Func<DialogueContext, CharacterData> resolve)
        => _resolve = resolve;

    public CharacterData Resolve(DialogueContext context) => _resolve(context);
}
```

### 3.5 `DialogueChoiceOption` [revised]

```csharp
public readonly struct DialogueChoiceOption
{
    public readonly int    Index;
    public readonly string Text;
    public readonly bool   IsAvailable;

    // Explicit constructor is required — all fields are readonly.
    public DialogueChoiceOption(int index, string text, bool isAvailable)
    {
        Index       = index;
        Text        = text;
        IsAvailable = isAvailable;
    }
}
```

-----

## 4. The Presenter Pattern — `IDialoguePresenter` [revised]

All visual concerns live here. The service knows nothing about UIToolkit or prefabs.

`SkipTypewriter()` has been removed from this interface. It was an implementation detail
that assumed every presenter uses a typewriter animation — `SpeechBubblePresenter` may
use a fade or pop-in instead and has no typewriter to skip. Rather than naming a specific
animation technique on a general interface, `ShowLine` now returns an
`IDialogueLinePlayback` that separates the two phases the service actually cares about:
"can skip now" and "line is done". The service's `Advance()` drives those phases without
knowing what animation the presenter is running.

```csharp
/// <summary>
/// Returned immediately by ShowLine. The service awaits each phase
/// independently; the presenter drives them internally.
/// </summary>
public interface IDialogueLinePlayback
{
    // Completes when the animation is skippable (e.g. typewriter mid-reveal).
    UniTask SkippableAsync { get; }

    // Completes when the line is fully presented and Advance() should move on.
    UniTask CompletedAsync { get; }

    // Fast-forward the animation to its end state.
    void Skip();
}

public interface IDialoguePresenter
{
    // Called once before any lines are shown.
    UniTask OpenAsync(CancellationToken ct = default);

    // Begin presenting a line. Returns immediately with a playback handle;
    // the service awaits CompletedAsync or calls Skip() via the handle.
    IDialogueLinePlayback ShowLine(DialogueLine line, CancellationToken ct = default);

    // Show choices and return the selected index.
    UniTask<int> ShowChoicesAsync(
        IReadOnlyList<DialogueChoiceOption> options,
        CancellationToken ct = default);

    // Dismiss the view.
    UniTask CloseAsync(CancellationToken ct = default);
}
```

### 4.1 `DialogueBoxPresenter` [revised]

Drives the **existing** UIToolkit overlay (`DialogueBoxOverlay.asset`) and the CSS already
written in `Core.uss`.

Key responsibilities:

- Open/close the `OverlayDefinition` via `INavigationService`
- Update the active speaker's portrait, name label, and body text via `VisualElement` query
- Delegate text reveal to `ITypewriterEffect` via an `Action<string>` callback
- Show/hide the choices row (`.dialogue-choices`) and wire up buttons

```csharp
public class DialogueBoxPresenter : IDialoguePresenter
{
    private readonly INavigationService _nav;
    private readonly OverlayDefinition  _overlayDef;   // DialogueBoxOverlay SO
    private readonly ITypewriterEffect  _typewriter;

    // Resolved from the OverlayCanvas on OpenAsync.
    // Two portrait roots and two name labels — one per seat.
    // ShowLine activates the label matching line.IsLeft and hides the other,
    // so the name always appears on the correct side without swapping one element.
    private VisualElement _portraitLeft, _portraitRight;
    private Label         _nameLeft, _nameRight;   // dialogue-name--left / dialogue-name--right
    private Label         _textLabel;
    private VisualElement _choicesRow;

    // ...
}
```

### 4.2 `SpeechBubblePresenter` (future)

Implements the same `IDialoguePresenter` but instantiates a world-space bubble prefab above
a character's `Transform`. No other service code changes. Because `IDialoguePresenter`
contains no UIToolkit-specific methods, `SpeechBubblePresenter` implements
`IDialogueLinePlayback` using a fade or pop-in animation without touching the interface.

-----

## 5. `IDialogueService` Interface [revised]

The interface has been split into two focused contracts.

`IDialogueService` is what game code, triggers, and Timeline consume at runtime.
`IDialogueServiceConfig` is what the installer uses once at startup. Gameplay code has
no reason to know about presenter swapping or command registration, so those concerns
live on a separate interface that is not registered in the `ServiceLocator`.

`IDialogueService` now extends `IDisposable` so the `ServiceLocator` can properly tear
down the service at scene unload without requiring a cast.

```csharp
/// <summary>
/// Runtime dialogue API consumed by game code, triggers, and Timeline.
/// </summary>
public interface IDialogueService : IInitializableService, IDisposable
{
    // ── Lifecycle ──────────────────────────────────────────────────
    /// <summary>
    /// Start a dialogue node. If a dialogue is already running it is stopped
    /// cleanly — CloseAsync is awaited and OnDialogueEnded fires — before the
    /// new conversation begins.
    ///
    /// Throws InvalidOperationException if nodeName does not exist in the YarnProject.
    /// Throws OperationCanceledException if ct is cancelled; CloseAsync and
    /// OnDialogueEnded are still guaranteed to run on cancellation.
    /// </summary>
    UniTask StartAsync(string nodeName, DialogueContext context,
                       CancellationToken ct = default);

    void Stop();
    bool IsRunning { get; }

    // ── Advance / Input ────────────────────────────────────────────
    /// <summary>
    /// If a line animation is in progress, skips to end-of-line.
    /// If the line is fully presented, continues to the next line.
    /// No-op if IsRunning is false.
    /// </summary>
    void Advance();

    // ── Choice confirmation ────────────────────────────────────────
    /// <summary>
    /// Confirms a pending choice when RequireChoiceConfirmation is true.
    /// Raises Dialogue_ChoiceConfirmed and unblocks the runner.
    /// No-op if no choice is currently pending.
    /// </summary>
    void ConfirmChoice(int index);
}

/// <summary>
/// Setup interface consumed only by the installer.
/// DialogueService implements both IDialogueService and IDialogueServiceConfig,
/// but only IDialogueService is registered in the ServiceLocator.
/// </summary>
public interface IDialogueServiceConfig
{
    /// <summary>
    /// Swap the active presenter. Safe to call between conversations.
    /// If called mid-conversation, the new presenter takes effect on the next line.
    /// </summary>
    void SetPresenter(IDialoguePresenter presenter);

    /// <summary>
    /// Register a custom Yarn command handler.
    /// </summary>
    void RegisterCommand(string commandName, Func<string[], UniTask> handler);
}
```

-----

## 6. `DialogueService` — Implementation Sketch [revised]

`DialogueService` is now responsible only for async lifecycle management. Line resolution
and command registration have been extracted into `ILineResolver` and
`IDialogueCommandRegistry` respectively. Event firing is delegated to `IDialogueEventBus`.
All four are constructor-injected, so the service is fully valid at construction time —
no post-construction field assignment.

```csharp
public class DialogueService : IDialogueService, IDialogueServiceConfig, IDisposable
{
    // ── Dependencies (constructor-injected) ────────────────────────
    private readonly DialogueRunner           _runner;
    private readonly ILoggerService           _logger;
    private readonly ILineResolver            _lineResolver;
    private readonly IDialogueCommandRegistry _commandRegistry;
    private readonly IDialogueEventBus        _eventBus;

    // ── State ──────────────────────────────────────────────────────
    private IDialoguePresenter      _presenter;
    private DialogueContext         _context;
    private CancellationTokenSource _cts;
    private IDialogueLinePlayback   _currentPlayback;

    // ── IInitializableService ──────────────────────────────────────
    public int InitializationPriority => 50;

    public async UniTask InitializeAsync(IProgress<float> progress = null)
    {
        _runner.onNodeStart.AddListener(OnNodeStart);
        _runner.onNodeComplete.AddListener(OnNodeComplete);
        _commandRegistry.RegisterAll(_runner);
        await UniTask.CompletedTask;
    }

    // ── Public API ────────────────────────────────────────────────
    public async UniTask StartAsync(string nodeName, DialogueContext context,
                                    CancellationToken ct = default)
    {
        // Validate node before touching any state or opening the presenter
        if (!_runner.NodeExists(nodeName))
            throw new InvalidOperationException(
                $"DialogueService: Yarn node '{nodeName}' not found in the YarnProject.");

        // Clean up any in-flight conversation first
        if (IsRunning)
            await StopAndCloseAsync();

        _context = context;
        // Dispose previous CTS before creating a new one
        _cts?.Dispose();
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        try
        {
            await _presenter.OpenAsync(_cts.Token);
            _runner.StartDialogue(nodeName);
            _eventBus.RaiseStarted(nodeName);
        }
        catch (OperationCanceledException)
        {
            // Guarantee cleanup on cancellation: close presenter and fire ended event
            await _presenter.CloseAsync(CancellationToken.None);
            _eventBus.RaiseEnded(nodeName);
            throw;
        }
    }

    public void Stop() => _cts?.Cancel();

    public void Advance()
    {
        if (!IsRunning) return;

        if (_currentPlayback != null &&
            !_currentPlayback.CompletedAsync.Status.IsCompleted())
        {
            _currentPlayback.Skip();
        }
        else
        {
            _runner.Continue();
            _eventBus.RaiseAdvanced();
        }
    }

    public void ConfirmChoice(int index)
    {
        if (!IsRunning) return;
        _runner.SetSelectedOption(index);
        _eventBus.RaiseChoiceConfirmed(index);
    }

    // ── IDisposable ────────────────────────────────────────────────
    public void Dispose()
    {
        _cts?.Dispose();
        _cts = null;
        _runner.onNodeStart.RemoveListener(OnNodeStart);
        _runner.onNodeComplete.RemoveListener(OnNodeComplete);
    }

    // ── Internal helpers ──────────────────────────────────────────
    private async UniTask StopAndCloseAsync()
    {
        _cts?.Cancel();
        await _presenter.CloseAsync(CancellationToken.None);
        _cts?.Dispose();
        _cts = null;
    }

    // ── YarnSpinner View Interface ────────────────────────────────
    // Implement DialogueViewBase (YarnSpinner's recommended extension point).
    //
    // RunLine:
    //   line    = _lineResolver.Resolve(yarnLine, _context)
    //   _currentPlayback = _presenter.ShowLine(line, _cts.Token)
    //   await _currentPlayback.CompletedAsync
    //   requestNextLine callback
    //
    // RunOptions:
    //   options = map YarnSpinner OptionSet → List<DialogueChoiceOption>
    //   index   = await _presenter.ShowChoicesAsync(options, _cts.Token)
    //   _eventBus.RaiseChoiceMade(index)
    //   if RequireChoiceConfirmation → _eventBus.RaiseChoicePending(index) → wait for ConfirmChoice
    //   else → runner.SetSelectedOption(index)
    //
    // DialogueComplete:
    //   await _presenter.CloseAsync(CancellationToken.None)
    //   _eventBus.RaiseEnded(currentNodeName)
}
```

### 6.1 `ILineResolver` and `LineResolver` [revised]

Line resolution has been extracted from `DialogueService` into its own collaborator.
`DialogueService` owns the async lifecycle; `LineResolver` owns the mapping from a raw
`LocalizedLine` to a typed `DialogueLine`. They have independent reasons to change and
are now independent units.

```csharp
public interface ILineResolver
{
    DialogueLine Resolve(LocalizedLine yarnLine, DialogueContext context);
}

public class LineResolver : ILineResolver
{
    public DialogueLine Resolve(LocalizedLine yarnLine, DialogueContext context)
    {
        var name      = yarnLine.CharacterName ?? "";
        CharacterData charData = null;

        // Walk the alias map. Every speaker name in the .yarn file should have
        // an entry. charData stays null for anonymous narration lines — the
        // presenter hides portrait and name labels in that case.
        if (context.SpeakerAliases.TryGetValue(name, out var resolver))
            charData = resolver.Resolve(context);

        // Right seat = the observer. Determined by reference equality only.
        // Entity type is irrelevant. Seat swaps take effect on the next line.
        bool isLeft = !ReferenceEquals(charData, context.RightSpeaker);

        var emotion = ParseEmotionTag(yarnLine.Text);

        return new DialogueLine(
            rawText:       Yarn.Markup.LineParser.StripMarkup(yarnLine.Text.Text),
            characterName: charData?.CharacterName ?? name,
            character:     charData,
            emotion:       emotion,
            isLeft:        isLeft
        );
    }

    private static EmotionType ParseEmotionTag(Yarn.Markup.MarkupParseResult text)
    {
        if (text.TryGetAttributeWithName("emotion", out var attr) &&
            attr.Properties.TryGetValue("emotion", out var val) &&
            System.Enum.TryParse<EmotionType>(val.StringValue, ignoreCase: true, out var emotion))
        {
            return emotion;
        }
        return EmotionType.Neutral;
    }
}
```

### 6.2 Writing Dialogue in Yarn — Any Participant Combo

The `.yarn` file uses the alias names registered in `SpeakerAliases`. The canonical
seat aliases are `"Right"` and `"Left"` — they resolve to whoever is in those seats at
runtime, regardless of entity type. Named characters can be added alongside them.

```yarn
title: QuestAccept
---
// "Left" and "Right" resolve to whatever CharacterData is in those seats.
// This script works identically whether the seats hold players, NPCs,
// networked entities, or any combination.
Left: I need your help with something.
Right: What do you need?
Left: There's trouble at the old mill. Will you go?
-> Yes, I'll go.
    Right: On my way.
-> Not right now.
    Right: Maybe later.
===
```

Named characters sit alongside seat aliases without conflict:

```yarn
title: TavernScene
---
Innkeeper: Welcome, traveller.
Right: Good evening. Any rooms available?
===
```

Monologue — only one seat set in context, the other is null:

```yarn
title: InnerThoughts
---
Right: This place gives me a bad feeling...
Right: Better stay alert.
===
```

### 6.3 `DialogueContextFactory` [revised]

Context construction has been extracted from `DialogueInstaller` into a dedicated factory.
The installer was the wrong home for a factory method — its only job is wiring the DI
graph. Both `DialogueTrigger.BuildContext()` and `DialogueBehaviour.BuildContext()` now
call this factory, eliminating the previously documented duplication between those paths.

```csharp
/// <summary>
/// Creates fully initialized DialogueContext instances with the standard
/// "Right" and "Left" seat aliases pre-populated. This is the single
/// authoritative place for context construction.
/// </summary>
public static class DialogueContextFactory
{
    public static DialogueContext Create(
        CharacterData rightSpeaker,
        CharacterData leftSpeaker = null)
    {
        var ctx = new DialogueContext
        {
            RightSpeaker = rightSpeaker,
            LeftSpeaker  = leftSpeaker
        };

        ctx.AddAlias("Right", new RightSeatResolver());
        ctx.AddAlias("Left",  new LeftSeatResolver());

        return ctx;
    }
}
```

Callers add named characters and extend as needed per conversation:

```csharp
// Player talks to a shop NPC
var ctx = DialogueContextFactory.Create(heroData, innkeeperData);
ctx.AddAlias("Innkeeper", new FixedSpeakerResolver(innkeeperData));
await _dialogueService.StartAsync("TavernScene", ctx);

// Two players in co-op — each client independently sets its own right seat.
var ctx = DialogueContextFactory.Create(myCharacterData, otherPlayerData);
await _dialogueService.StartAsync("CoopBanter", ctx);

// Pure NPC cutscene
var ctx = DialogueContextFactory.Create(captainData, messengerData);
ctx.AddAlias("Captain",   new FixedSpeakerResolver(captainData));
ctx.AddAlias("Messenger", new FixedSpeakerResolver(messengerData));
await _dialogueService.StartAsync("CaptainBriefing", ctx);

// Dynamic narrator whose portrait changes based on game state
ctx.AddAlias("Narrator", new DelegateSpeakerResolver(
    c => gameState.IsEvil ? darkNarratorData : lightNarratorData));
```

-----

## 7. Typewriter Effect [revised]

Completely standalone — no knowledge of YarnSpinner or presenters.

Previously `PlayAsync` accepted a UIToolkit `Label` directly, which prevented reuse in
`SpeechBubblePresenter` (which may use TextMeshPro or a custom world-space renderer).
The signature now accepts an `Action<string>` callback, making the effect fully
renderer-agnostic. An `ITypewriterEffect` interface has been added so presenters and
tests can substitute the implementation without depending on the concrete class.

The previous version also orphaned its internal `CancellationTokenSource` on each call.
The new version disposes the previous token before allocating a new one.

```csharp
public interface ITypewriterEffect
{
    float CharactersPerSecond { get; set; }

    /// <summary>
    /// Reveal fullText character by character, invoking onTextUpdate each step.
    /// Returns when all text is shown or Skip() has been called.
    /// </summary>
    UniTask PlayAsync(string fullText, Action<string> onTextUpdate,
                      CancellationToken ct = default);

    void Skip();
}

public class TypewriterEffect : ITypewriterEffect
{
    public float CharactersPerSecond { get; set; } = 30f;

    private CancellationTokenSource _skipCts;

    public async UniTask PlayAsync(string fullText, Action<string> onTextUpdate,
                                   CancellationToken ct = default)
    {
        // Dispose previous skip token before creating a new one
        _skipCts?.Dispose();
        _skipCts = new CancellationTokenSource();

        var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _skipCts.Token);

        onTextUpdate?.Invoke("");
        float delay = 1f / CharactersPerSecond;

        for (int i = 0; i < fullText.Length; i++)
        {
            if (linked.Token.IsCancellationRequested) break;
            onTextUpdate?.Invoke(fullText[..(i + 1)]);
            await UniTask.WaitForSeconds(delay, cancellationToken: linked.Token)
                         .SuppressCancellationThrow();
        }

        onTextUpdate?.Invoke(fullText);   // ensure complete on skip
        linked.Dispose();
    }

    public void Skip() => _skipCts?.Cancel();
}
```

**Usage inside `DialogueBoxPresenter`** — renderer-agnostic, works identically in any
presenter that has a string-settable text element:

```csharp
_typewriter.PlayAsync(line.RawText, text => _textLabel.text = text, ct)
           .ContinueWith(() => _playback.MarkCompleted())
           .Forget();
```

**Pause tags** (`[pause=0.3]`) can be parsed from Yarn markup metadata and injected as
extra `WaitForSeconds` calls at matching character indices.

-----

## 8. `IDialogueEventBus` [revised]

Previously `DialogueService` held public mutable fields for each SOAP event asset,
leaving the object partially initialized between construction and installer assignment
and coupling it to the concrete SOAP types. These have been replaced with an
`IDialogueEventBus` interface injected at construction time.

```csharp
/// <summary>
/// Abstracts all event-firing from DialogueService. The service is fully
/// initialized at construction and has no knowledge of SOAP types.
/// </summary>
public interface IDialogueEventBus
{
    void RaiseStarted(string nodeName);
    void RaiseEnded(string nodeName);
    void RaiseLine(DialogueLine line);
    void RaiseAdvanced();
    void RaiseChoiceMade(int index);
    void RaiseChoicePending(int index);
    void RaiseChoiceConfirmed(int index);
}

/// <summary>
/// SOAP-backed implementation. Constructed and wired in the installer.
/// </summary>
public class SoapDialogueEventBus : IDialogueEventBus
{
    private readonly ScriptableEventString         _onStarted;
    private readonly ScriptableEventString         _onEnded;
    private readonly ScriptableEvent<DialogueLine> _onLinePresented;
    private readonly ScriptableEventNoParam        _onAdvanced;
    private readonly ScriptableEventInt            _onChoiceMade;
    private readonly ScriptableEventInt            _onChoicePending;
    private readonly ScriptableEventInt            _onChoiceConfirmed;

    public SoapDialogueEventBus(
        ScriptableEventString         onStarted,
        ScriptableEventString         onEnded,
        ScriptableEvent<DialogueLine> onLinePresented,
        ScriptableEventNoParam        onAdvanced,
        ScriptableEventInt            onChoiceMade,
        ScriptableEventInt            onChoicePending,
        ScriptableEventInt            onChoiceConfirmed)
    {
        _onStarted         = onStarted;
        _onEnded           = onEnded;
        _onLinePresented   = onLinePresented;
        _onAdvanced        = onAdvanced;
        _onChoiceMade      = onChoiceMade;
        _onChoicePending   = onChoicePending;
        _onChoiceConfirmed = onChoiceConfirmed;
    }

    public void RaiseStarted(string nodeName)   => _onStarted?.Raise(nodeName);
    public void RaiseEnded(string nodeName)     => _onEnded?.Raise(nodeName);
    public void RaiseLine(DialogueLine line)    => _onLinePresented?.Raise(line);
    public void RaiseAdvanced()                 => _onAdvanced?.Raise();
    public void RaiseChoiceMade(int index)      => _onChoiceMade?.Raise(index);
    public void RaiseChoicePending(int index)   => _onChoicePending?.Raise(index);
    public void RaiseChoiceConfirmed(int index) => _onChoiceConfirmed?.Raise(index);
}
```

-----

## 9. SOAP Event Assets [revised]

Create these `ScriptableObject` assets in `Assets/Data/Events/Dialogue/`:

|Asset name                 |Type                           |Raised when                             |
|---------------------------|-------------------------------|----------------------------------------|
|`Dialogue_Started`         |`ScriptableEventString`        |dialogue node begins                    |
|`Dialogue_Ended`           |`ScriptableEventString`        |dialogue node completes or is stopped   |
|`Dialogue_LinePresented`   |`ScriptableEvent<DialogueLine>`|line shown to player                    |
|`Dialogue_Advanced`        |`ScriptableEventNoParam`       |player presses advance                  |
|`Dialogue_ChoiceMade`      |`ScriptableEventInt`           |local player selects a choice index     |
|`Dialogue_ChoicePending`   |`ScriptableEventInt`           |choice made, awaiting external confirm  |
|`Dialogue_ChoiceConfirmed` |`ScriptableEventInt`           |choice confirmed, runner unblocked      |

> These wire into any system via `EventListenerGeneric<T>` components — no DialogueService
> dependency required downstream.

-----

## 10. Built-in Yarn Commands [revised]

Register these inside `IDialogueCommandRegistry.RegisterAll()`:

```yarn
# Swap which CharacterData occupies a seat mid-conversation.
# Seat is "Right" or "Left" (matches SeatId enum name, case-insensitive).
# The next line after this command will use the updated seat data.
<<switchSeat Right "Warrior">>    // right seat now shows Warrior's CharacterData
<<switchSeat Left "Elder">>       // left seat now shows Elder's CharacterData

# Trigger a SOAP ScriptableEvent by name.
# Resolved via a pre-registered Dictionary<string, ScriptableEventNoParam>
# populated in the installer — no runtime Resources.Load.
<<event "BossAppeared">>

# Set character emotion on the portrait.
<<emotion "Alice" "sad">>

# Control typewriter speed mid-line.
<<speed 15>>
<<speed default>>

# Pause narrative without closing box.
<<waitDialogue 2.0>>
```

### `IDialogueCommandRegistry` [revised]

Command registration has been extracted from `DialogueService` into its own collaborator.
The service's `InitializeAsync` calls `RegisterAll` once; thereafter it has no knowledge
of which commands exist.

```csharp
public interface IDialogueCommandRegistry
{
    /// <summary>
    /// Register all built-in Yarn commands on the provided runner.
    /// Called once during DialogueService.InitializeAsync.
    /// </summary>
    void RegisterAll(DialogueRunner runner);
}

public class DialogueCommandRegistry : IDialogueCommandRegistry
{
    // contextRef is the live context updated each StartAsync — shared reference.
    private DialogueContext _contextRef;
    private readonly ILoggerService _logger;

    // Named event map pre-populated by the installer from inspector SO references.
    // Avoids any Resources.Load call on the hot dialogue command path.
    private readonly IReadOnlyDictionary<string, ScriptableEventNoParam> _namedEvents;

    public DialogueCommandRegistry(
        ILoggerService logger,
        IReadOnlyDictionary<string, ScriptableEventNoParam> namedEvents)
    {
        _logger      = logger;
        _namedEvents = namedEvents;
    }

    // Called by DialogueService each StartAsync to keep the context reference current.
    public void SetContext(DialogueContext context) => _contextRef = context;

    public void RegisterAll(DialogueRunner runner)
    {
        runner.AddCommandHandler("switchSeat",   HandleSwitchSeat);
        runner.AddCommandHandler("event",        HandleEvent);
        runner.AddCommandHandler("emotion",      HandleEmotion);
        runner.AddCommandHandler("speed",        HandleSpeed);
        runner.AddCommandHandler("waitDialogue", HandleWaitDialogue);
    }

    private UniTask HandleSwitchSeat(string[] args)
    {
        // SeatId enum parse — no magic strings
        if (!System.Enum.TryParse<SeatId>(args[0], ignoreCase: true, out var seat))
        {
            _logger?.LogWarning(this,
                $"switchSeat: unknown seat '{args[0]}'. Expected 'Right' or 'Left'.");
            return UniTask.CompletedTask;
        }

        var aliasName = args[1];
        if (!_contextRef.SpeakerAliases.TryGetValue(aliasName, out var resolver))
        {
            _logger?.LogWarning(this, $"switchSeat: alias '{aliasName}' not registered.");
            return UniTask.CompletedTask;
        }

        var charData = resolver.Resolve(_contextRef);
        if (seat == SeatId.Right) _contextRef.RightSpeaker = charData;
        else                      _contextRef.LeftSpeaker  = charData;

        return UniTask.CompletedTask;
    }

    private UniTask HandleEvent(string[] args)
    {
        var eventName = args[0];
        if (_namedEvents.TryGetValue(eventName, out var evt))
            evt.Raise();
        else
            _logger?.LogWarning(this,
                $"<<event>>: no event registered with name '{eventName}'.");
        return UniTask.CompletedTask;
    }

    // HandleEmotion, HandleSpeed, HandleWaitDialogue implementations omitted for brevity.
}
```

-----

## 11. Conversation Setup — Example Combinations

The service treats all of these identically. The only difference is which `CharacterData`
goes in which seat and which aliases are registered.

### Player ↔ NPC

```csharp
var ctx = DialogueContextFactory.Create(heroData, innkeeperData);
ctx.AddAlias("Innkeeper", new FixedSpeakerResolver(innkeeperData));
await _dialogueService.StartAsync("TavernScene", ctx);
```

### Co-op: Player ↔ Player (networked or local split-screen)

Each client calls `DialogueContextFactory.Create` independently with its own character
as `rightSpeaker`. No special networking path in the dialogue system — the seats look
the same from the service's perspective on every client.

```csharp
// Client A (playing as Knight)
var ctx = DialogueContextFactory.Create(knightData, mageData);
await _dialogueService.StartAsync("CoopBanter", ctx);

// Client B (playing as Mage) — identical call, seats are just reversed
var ctx = DialogueContextFactory.Create(mageData, knightData);
await _dialogueService.StartAsync("CoopBanter", ctx);
```

Both clients see themselves on the right.

### NPC ↔ NPC Cutscene

```csharp
var ctx = DialogueContextFactory.Create(captainData, messengerData);
ctx.AddAlias("Captain",   new FixedSpeakerResolver(captainData));
ctx.AddAlias("Messenger", new FixedSpeakerResolver(messengerData));
await _dialogueService.StartAsync("CaptainBriefing", ctx);
```

### Monologue / Inner Thoughts

```csharp
// LeftSpeaker defaults to null — presenter hides the left portrait and name label.
var ctx = DialogueContextFactory.Create(heroData);
await _dialogueService.StartAsync("InnerThoughts", ctx);
```

### Seat Swap at Runtime (via Yarn command)

`<<switchSeat Right "Warrior">>` is handled by `DialogueCommandRegistry.HandleSwitchSeat`.
`_contextRef.RightSpeaker` is updated; the next `LineResolver.Resolve()` call picks it up.

### Choice Confirmation (for any two-party flow)

When `RequireChoiceConfirmation` is true, after the local player selects an option the
service raises `Dialogue_ChoicePending`. Whatever system owns confirmation (network,
AI, another player's input) listens and calls `dialogueService.ConfirmChoice(index)`.
The dialogue service does not know or care who or what provides that confirmation.

-----

## 11.5 Dynamic Context Building — `DialogueTrigger` & `IDialogueParticipant` [revised]

Rather than manually constructing a `DialogueContext` at every call site, two lightweight
pieces let the scene wire itself up and build the context at the moment dialogue fires.

### `IDialogueParticipant`

Any scene object that can sit in a dialogue seat implements this interface.

```csharp
/// <summary>
/// Implemented by any scene object that can occupy a dialogue seat.
/// Examples: PlayerController, NPC, NetworkedCharacter.
/// The trigger queries this at fire-time, not at setup-time.
/// </summary>
public interface IDialogueParticipant
{
    CharacterData GetCharacterData();
}
```

A player controller adds three lines:

```csharp
public class PlayerController : MonoBehaviour, IDialogueParticipant
{
    [SerializeField] private CharacterData _characterData;
    public CharacterData GetCharacterData() => _characterData;
}
```

An NPC is identical. A networked character resolves from the network state at call time.
The trigger never knows which kind it is talking to.

### `ILocalPlayerProvider` [revised]

`DialogueTrigger` previously called `FindFirstObjectByType<PlayerController>()` directly,
hard-coupling it to a concrete type. Any game object that implements `IDialogueParticipant`
but is not a `PlayerController` would be invisible to auto-find — a DIP and OCP violation.

```csharp
/// <summary>
/// Abstracts local player lookup. Inject a different implementation for
/// split-screen, network play, or testing without modifying DialogueTrigger.
/// </summary>
public interface ILocalPlayerProvider
{
    IDialogueParticipant GetLocalPlayer();
}

/// Default implementation — finds the first GameObject tagged "Player"
/// that implements IDialogueParticipant. Works with any concrete type.
public class TaggedLocalPlayerProvider : ILocalPlayerProvider
{
    public IDialogueParticipant GetLocalPlayer()
    {
        var go = GameObject.FindGameObjectWithTag("Player");
        return go != null ? go.GetComponent<IDialogueParticipant>() : null;
    }
}
```

### `DialogueTrigger` [revised]

```csharp
public class DialogueTrigger : MonoBehaviour
{
    [Header("Dialogue")]
    [DialogueSequenceId]
    [SerializeField] private string _nodeName;

    [Header("Participants")]
    [Tooltip("The observer's seat — rendered on the right. " +
             "Leave null to auto-find via ILocalPlayerProvider.")]
    [SerializeField] private GameObject _rightParticipant;

    [Tooltip("The other party's seat — rendered on the left. " +
             "Leave null for a monologue.")]
    [SerializeField] private GameObject _leftParticipant;

    [Header("Options")]
    [SerializeField] private bool _requireChoiceConfirmation;

    // ── Called by physics trigger, interaction system, Timeline, etc. ──
    public void Trigger()
    {
        var service = ServiceLocator.Global.Get<IDialogueService>();
        var ctx     = BuildContext();
        service.StartAsync(_nodeName, ctx).Forget();
    }

    public DialogueContext BuildContext()
    {
        var playerProvider = ServiceLocator.Global.Get<ILocalPlayerProvider>();

        var right = ResolveParticipant(_rightParticipant, playerProvider, autoFind: true);
        var left  = ResolveParticipant(_leftParticipant,  playerProvider, autoFind: false);

        var ctx = DialogueContextFactory.Create(right, left);
        ctx.RequireChoiceConfirmation = _requireChoiceConfirmation;

        // Add named alias for the left seat so Yarn authors can use either
        // "Left" (seat alias) or the character's own name interchangeably.
        if (left != null)
            ctx.AddAlias(left.CharacterName, new FixedSpeakerResolver(left));

        return ctx;
    }

    private static CharacterData ResolveParticipant(
        GameObject go, ILocalPlayerProvider playerProvider, bool autoFind)
    {
        if (go != null)
            return go.GetComponent<IDialogueParticipant>()?.GetCharacterData();

        if (autoFind)
            return playerProvider?.GetLocalPlayer()?.GetCharacterData();

        return null;
    }
}
```

### How it composes in practice

**NPC with a trigger collider** — drag the NPC into `_leftParticipant` and leave
`_rightParticipant` null. Auto-find via `ILocalPlayerProvider` picks up the local player
at fire-time regardless of their concrete type.

```
[NPC GameObject]
  ├── NpcController         (implements IDialogueParticipant)
  ├── DialogueTrigger
  │     _nodeName           = "VillagerGreeting"
  │     _rightParticipant   = (null → ILocalPlayerProvider)
  │     _leftParticipant    = this NPC's GameObject
  └── Collider (OnTriggerEnter → dialogueTrigger.Trigger())
```

**Co-op** — each client's `ILocalPlayerProvider` returns that client's own player.
Same trigger, same Yarn node, correct seats on every screen.

**Scripted cutscene without a player** — assign both seats explicitly to NPC
GameObjects; auto-find is skipped since `_rightParticipant` is not null.

**Timeline clip** — `DialogueBehaviour.BuildContext()` calls `DialogueContextFactory.Create`
directly — the same factory `DialogueTrigger` uses — with no duplicated resolution logic.

### When you need more control

```csharp
var ctx = DialogueContextFactory.Create(
    rightSpeaker: interactingPlayer.GetCharacterData(),
    leftSpeaker:  targetNpc.GetCharacterData()
);
await _dialogueService.StartAsync("QuestOffer", ctx);
```

`DialogueTrigger` is a convenience, not a requirement. The `DialogueContext` + `StartAsync`
path always remains available for cases that need full control.

### `DialogueTrack` [revised]

```
Timeline Track: DialogueTrack (inherits from TrackAsset)
  └── DialogueClip (inherits from PlayableAsset)
        ├── nodeName         : string      [DialogueSequenceId] attribute → dropdown
        ├── rightParticipant : GameObject  (optional; null = ILocalPlayerProvider)
        ├── leftParticipant  : GameObject  (optional; null = monologue)
        └── waitForCompletion: bool
```

```csharp
[TrackClipType(typeof(DialogueClip))]
[TrackBindingType(typeof(DialogueServiceBridge))]
public class DialogueTrack : TrackAsset { }

public class DialogueBehaviour : PlayableBehaviour
{
    // Properties, not public fields
    public string     NodeName           { get; set; }
    public bool       WaitForCompletion  { get; set; }
    public GameObject RightParticipant   { get; set; }
    public GameObject LeftParticipant    { get; set; }

    private IDialogueService _service;
    private bool             _started;

    public override void OnBehaviourPlay(Playable playable, FrameData info)
    {
        _service = ServiceLocator.Global.Get<IDialogueService>();
        if (!_started)
        {
            _started = true;
            _service.StartAsync(NodeName, BuildContext()).Forget();
        }
    }

    public override void OnBehaviourPause(Playable playable, FrameData info)
    {
        // Reset _started so scrubbing backward past this clip fires it again
        _started = false;

        if (WaitForCompletion) return;
        _service?.Stop();
    }

    public override void OnPlayableDestroy(Playable playable)
    {
        _started = false;
    }

    private DialogueContext BuildContext()
    {
        var playerProvider = ServiceLocator.Global.Get<ILocalPlayerProvider>();

        var right = RightParticipant != null
            ? RightParticipant.GetComponent<IDialogueParticipant>()?.GetCharacterData()
            : playerProvider?.GetLocalPlayer()?.GetCharacterData();

        var left = LeftParticipant != null
            ? LeftParticipant.GetComponent<IDialogueParticipant>()?.GetCharacterData()
            : null;

        return DialogueContextFactory.Create(right, left);
    }
}
```

`DialogueServiceBridge` is a thin `MonoBehaviour` on the Timeline object that exposes the
`IDialogueService` reference to the track. The `[DialogueSequenceId]` attribute (already
written in `Editor/Story/DialogueSequenceIdDrawer.cs`) gives the dropdown for `nodeName`.

-----

## 12. Service Registration [revised]

```csharp
public class DialogueInstaller : IInstaller
{
    [Header("Yarn")]
    public DialogueRunner DialogueRunner;

    [Header("Overlay")]
    public OverlayDefinition DialogueBoxOverlay;

    [Header("SOAP Events")]
    public ScriptableEventString         OnStarted;
    public ScriptableEventString         OnEnded;
    public ScriptableEvent<DialogueLine> OnLinePresented;
    public ScriptableEventNoParam        OnAdvanced;
    public ScriptableEventInt            OnChoiceMade;
    public ScriptableEventInt            OnChoicePending;
    public ScriptableEventInt            OnChoiceConfirmed;

    [Header("Named Events (for <<event>> command)")]
    [Tooltip("Pre-register any ScriptableEventNoParam assets that Yarn scripts" +
             " may trigger via <<event \"Name\">>. Avoids runtime asset lookups.")]
    public NamedEventEntry[] NamedEvents;

    public void Install(IServiceLocator locator)
    {
        var navService = locator.Get<INavigationService>();
        var logger     = locator.Get<ILoggerService>();

        var typewriter = new TypewriterEffect();
        var presenter  = new DialogueBoxPresenter(navService, DialogueBoxOverlay, typewriter);

        var eventBus = new SoapDialogueEventBus(
            OnStarted, OnEnded, OnLinePresented,
            OnAdvanced, OnChoiceMade, OnChoicePending, OnChoiceConfirmed);

        var namedEventMap = NamedEvents.ToDictionary(e => e.Name, e => e.Event);

        var lineResolver    = new LineResolver();
        var commandRegistry = new DialogueCommandRegistry(logger, namedEventMap);

        var service = new DialogueService(
            DialogueRunner, logger, lineResolver, commandRegistry, eventBus);

        // IDialogueServiceConfig is not registered — only the installer holds it
        ((IDialogueServiceConfig)service).SetPresenter(presenter);

        locator.RegisterInstance<IDialogueService>(service);
        locator.RegisterInstance<ILocalPlayerProvider>(new TaggedLocalPlayerProvider());
    }
}

[System.Serializable]
public struct NamedEventEntry
{
    public string                 Name;
    public ScriptableEventNoParam Event;
}
```

-----

## 13. Input Wiring

Hook the "Advance" action (already in `InputReader`) directly to `IDialogueService.Advance()`:

```csharp
// In a thin MonoBehaviour on the scene bootstrapper:
[InputAction("Interact", InputActionPhase.Started)]
private void OnInteract(InputAction.CallbackContext _)
{
    if (_dialogueService.IsRunning)
        _dialogueService.Advance();
}
```

-----

## 14. File / Folder Structure [revised]

```
Assets/
└── Core/
    └── Systems/
        └── Dialogue/
            ├── IDialogueService.cs         // runtime contract; extends IDisposable
            ├── IDialogueServiceConfig.cs   // setup contract (SetPresenter, RegisterCommand)
            ├── DialogueService.cs
            ├── DialogueContext.cs
            ├── DialogueLine.cs
            ├── DialogueChoiceOption.cs
            ├── SeatId.cs                   // enum: Right, Left
            │
            ├── Resolver/
            │   ├── ILineResolver.cs
            │   ├── LineResolver.cs
            │   ├── ISpeakerResolver.cs
            │   ├── RightSeatResolver.cs
            │   ├── LeftSeatResolver.cs
            │   ├── FixedSpeakerResolver.cs
            │   └── DelegateSpeakerResolver.cs
            │
            ├── Presenter/
            │   ├── IDialoguePresenter.cs
            │   ├── IDialogueLinePlayback.cs
            │   ├── DialogueBoxPresenter.cs      ← wraps existing overlay + CSS
            │   └── SpeechBubblePresenter.cs     ← (stub / future)
            │
            ├── Events/
            │   ├── IDialogueEventBus.cs
            │   └── SoapDialogueEventBus.cs
            │
            ├── Commands/
            │   ├── IDialogueCommandRegistry.cs
            │   └── DialogueCommandRegistry.cs
            │
            ├── Effects/
            │   ├── ITypewriterEffect.cs
            │   └── TypewriterEffect.cs
            │
            ├── Factory/
            │   └── DialogueContextFactory.cs
            │
            ├── Trigger/
            │   ├── IDialogueParticipant.cs
            │   ├── ILocalPlayerProvider.cs
            │   ├── TaggedLocalPlayerProvider.cs
            │   └── DialogueTrigger.cs
            │
            ├── Timeline/
            │   ├── DialogueTrack.cs
            │   ├── DialogueClip.cs
            │   ├── DialogueBehaviour.cs
            │   └── DialogueServiceBridge.cs
            │
            └── Installer/
                └── DialogueInstaller.cs
```

-----

## 15. Implementation Order (Recommended Phases)

### Phase 1 — Foundation (no UI yet)

1. `SeatId`, `DialogueContext`, `DialogueLine`, `DialogueChoiceOption`
2. `ISpeakerResolver` + four built-in implementations
3. `ILineResolver` + `LineResolver`
4. `DialogueContextFactory`
5. `IDialogueService` + `IDialogueServiceConfig` + `IDialoguePresenter` + `IDialogueLinePlayback`
6. `ITypewriterEffect` + `TypewriterEffect`
7. `IDialogueEventBus` + `SoapDialogueEventBus`
8. `IDialogueCommandRegistry` + `DialogueCommandRegistry`
9. `DialogueService` wired to `DialogueRunner` callbacks (log only, no presenter)

### Phase 2 — DialogueBox Presenter

1. `DialogueBoxPresenter` (uses existing overlay & CSS, two name labels per seat)
2. `DialogueInstaller` + register in scene bootstrapper
3. Wire `InputReader` Advance action
4. `ILocalPlayerProvider` + `TaggedLocalPlayerProvider`
5. `IDialogueParticipant` + `DialogueTrigger`

### Phase 3 — Events & Commands

1. SOAP event SO assets (all seven from Section 9)
2. Built-in Yarn commands (`<<event>>`, `<<emotion>>`, `<<speed>>`, `<<waitDialogue>>`)
3. `<<switchSeat>>` + choice confirmation flow (`RequireChoiceConfirmation`, `ConfirmChoice`)
4. Populate `NamedEvents` inspector list for `<<event>>` resolution

### Phase 4 — Timeline

1. `DialogueTrack` / `DialogueClip` / `DialogueBehaviour`
2. `DialogueServiceBridge`

### Phase 5 — Speech Bubble (future)

1. `SpeechBubblePresenter` (world-space prefab, same `IDialoguePresenter` contract)
2. `((IDialogueServiceConfig)service).SetPresenter(new SpeechBubblePresenter(...))` — zero service changes
