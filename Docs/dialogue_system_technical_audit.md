**Dialogue System - Deep Technical Audit**

Mystery of the Scarab · Core Repository | JuveNILE Games | March 2026

# **Scope**

This audit covers all files that participate in the dialogue system's execution path, not just the Story/ folder. Files reviewed in full:

- Story/: DialogueManager.cs, DialogueLine.cs, DialogueSequence.cs, DialogueChoice.cs, DialogueDatabase.cs, DialogueTrigger.cs, DialogueBox.cs, IDialogueView.cs, all Structs
- Systems/Runner/: TaskRunner.cs, RunningTask.cs, IRunningTask.cs, ITaskResult.cs
- Systems/InputManagement/: InputReader.cs, InputManager.cs
- Systems/AudioSystem/: AudioService.cs, SoundBuilder.cs, SoundData.cs, SoundEmitter.cs
- Systems/Localization/: LocalizationService.cs, LocalizedString.cs (LocalizedStringReference)
- Systems/Services/: ServiceLocator.cs
- Utility/: ServiceInjector.cs
- Definitions/Character/: CharacterData.cs
- Editor/Story/: DialogueSequenceIdDrawer.cs

Findings are grouped by technical category. Each entry includes the exact file and line context, the root cause, the runtime consequence, and a concrete fix.

# **1\. Memory Allocation & GC Pressure**

## **1.1 O(n²) String Allocations During Typewriter Effect**

| **File** | DialogueBox.cs - TypeText() |
| --- | --- |
| **Root cause** | text.Substring(0, i) is called on every iteration of the character loop |
| **Consequence** | A 200-char line allocates ~200 strings. Typing speed of 30ms/char = ~6,000ms of allocations. Each allocation can trigger incremental GC, causing micro-stutters visible as frame spikes mid-dialogue |
| **Compounding factor** | After each Substring, UI Toolkit performs a full layout pass on the Label, so GC pressure and layout work scale together as O(n²) per line |

Current code:

dialogueLabel.text = text.Substring(0, i);

Fix - use a single pre-allocated char buffer and a ReadOnlySpan to write substrings without allocating, then assign once per frame rather than per character:

private char\[\] \_typeBuffer;

// In TypeText, before loop:

if (\_typeBuffer == null || \_typeBuffer.Length < text.Length)

\_typeBuffer = new char\[text.Length\];

text.CopyTo(0, \_typeBuffer, 0, i);

dialogueLabel.text = new string(\_typeBuffer, 0, i); // still one alloc, but avoids Substring

Better still, use a StringBuilder and assign .ToString() only when the visible length changes. For UI Toolkit specifically the correct approach is to use a VisualElement with a custom IMGUIContainer or set the text once at full length and use a USS -unity-text-overflow-position / clip approach, revealing characters via a custom shader or mesh, avoiding per-character text assignment entirely.

## **1.2 Per-Line Lambda Closure Allocations in ProcessDialogueLine**

| **File** | DialogueManager.cs - ProcessDialogueLine() |
| --- | --- |
| **Root cause** | Two InputReader.InputEvent delegates are constructed as lambdas inside the method body on every call |
| **Consequence** | Every dialogue line allocates two delegate objects + their closure objects on the heap. A sequence of 30 lines = 60 heap-allocated objects that cannot be pooled or reused |

Current code:

InputReader.InputEvent onSkipTyping = ctx => {

if (ctx.phase == InputActionPhase.Performed) \_activeView.SkipTyping();

};

// ...and again for onAdvance in WaitForAdvance

These lambdas are used only to subscribe/unsubscribe within the same call scope, so there is no reason to allocate them freshly each time. The fix is to promote them to cached instance fields or use a method group reference:

// Cached at field level:

private InputReader.InputEvent \_onSkipTypingHandler;

private InputReader.InputEvent \_onAdvanceHandler;

// In constructor or Awake:

\_onSkipTypingHandler = ctx => { if (ctx.phase == InputActionPhase.Performed) \_activeView?.SkipTyping(); };

\_onAdvanceHandler = ctx => { if (ctx.phase == InputActionPhase.Performed) \_advanceSignal?.TrySetResult(true); };

Because InputReader stores subscriptions as multicast delegates keyed by string, the subscribe/unsubscribe pattern still works correctly with cached delegates.

## **1.3 SoundData Heap Allocation for Every Legacy Voice Line**

| **File** | DialogueManager.cs - ProcessDialogueLine() |
| --- | --- |
| **Root cause** | The voiceClip fallback branch constructs a new SoundData on every line that uses the legacy field |
| **Consequence** | One heap allocation per voiced line during the hot path of dialogue playback |

var temp = new SoundData { clip = line.voiceClip, Volume = 1f };

\_audioService.CreateSound().WithSoundData(temp).Play();

Since voiceClip is already marked as legacy and migration to voiceData is planned, the correct fix is to complete the migration. If the legacy field must remain temporarily, cache the wrapper SoundData per-line at database load time rather than allocating it during playback.

## **1.4 O(n) LINQ Scan on CharacterData.GetPortrait() Per Displayed Line**

| **File** | CharacterData.cs - GetPortrait(EmotionType) |
| --- | --- |
| **Root cause** | emotions.FirstOrDefault(e => e.Type == emotionType) allocates a LINQ enumerator and scans the list linearly |
| **Consequence** | Called once per ShowLine call and once per SetupStaticVisuals call (i.e., twice per displayed line due to the duplication bug). For a character with 20 emotion entries, this is 40 comparisons per line |

public Sprite GetPortrait(EmotionType emotionType)

{

var emotion = emotions.FirstOrDefault(e => e.Type == emotionType);

return emotion.Portrait != null ? emotion.Portrait : defaultPortrait;

}

Fix - build a Dictionary&lt;EmotionType, Sprite&gt; cache in OnEnable or OnValidate. Since CharacterData is a ScriptableObject, the cache is built once and reused:

private Dictionary&lt;EmotionType, Sprite&gt; \_portraitCache;

private void OnEnable() => RebuildCache();

private void RebuildCache() {

\_portraitCache = emotions.ToDictionary(e => e.Type, e => e.Portrait);

}

public Sprite GetPortrait(EmotionType t) =>

\_portraitCache.TryGetValue(t, out var s) && s ? s : defaultPortrait;

## **1.5 DialogueSequenceIdDrawer Calls AssetDatabase.FindAssets Every OnGUI Frame**

| **File** | DialogueSequenceIdDrawer.cs - OnGUI() |
| --- | --- |
| **Root cause** | FindAssetsByType&lt;DialogueDatabase&gt;() runs AssetDatabase.FindAssets() and LoadAssetAtPath() on every single call to OnGUI. Unity calls OnGUI 2-3 times per repaint frame for layout and repaint passes |
| **Consequence** | Each inspector repaint triggers a full asset database scan. With multiple DialogueTrigger components selected, or during rapid editing, this will cause noticeable editor lag |

// Called every frame:

var databases = FindAssetsByType&lt;DialogueDatabase&gt;();

Fix - cache the result using a static field with a dirty flag, or use AssetPostprocessor to invalidate the cache when the database changes:

private static DialogueDatabase \_cachedDatabase;

private static List&lt;string&gt; \_cachedIds;

// Invalidate in OnEnable or via AssetPostprocessor.OnPostprocessAllAssets

Unity's PropertyDrawer instances are persistent per type, so a static cache is safe here. The database and IDs should be rebuilt only when the asset changes, not per frame.

# **2\. Async & Threading Issues**

## **2.1 Dialogue Sequence Is Wrapped in TaskRunner Unnecessarily**

| **File** | DialogueManager.cs - StartDialogue() |
| --- | --- |
| **Root cause** | TaskRunner.AddTask&lt;bool, DialogueManager&gt;() is called to run the dialogue sequence, creating a ServiceTaskContext that increments the ServiceLocator's runningTaskCounts for DialogueManager |
| **Consequence 1** | RunningTask unconditionally starts with await UniTask.Yield(), inserting a one-frame delay between StartDialogue() being called and the first line appearing on screen |
| **Consequence 2** | TaskRunner.PauseAll() and CancelAll() will affect the dialogue task. A global pause (e.g. opening a settings overlay) may incorrectly pause mid-dialogue |
| **Consequence 3** | The ServiceLocator's shutdown logic will wait up to ShutdownWaitForTasksTimeout (10 seconds) for the dialogue to complete when shutting down the scene |

TaskRunner is designed for background work (asset loading, save operations) where progress tracking and service lifecycle management matter. A foreground interactive dialogue sequence does not benefit from this and inherits all its costs.

The fix is to run the dialogue directly as a managed UniTask with its own CancellationTokenSource, and expose the awaitable handle as a custom class rather than RunningTask&lt;T&gt;:

public UniTask&lt;bool&gt; StartDialogue(DialogueSequence sequence)

{

\_dialogueCts = new CancellationTokenSource();

\_currentDialogueTask = RunDialogueSequence(sequence, \_dialogueCts.Token)

.AttachExternalCancellation(\_dialogueCts.Token);

return \_currentDialogueTask;

}

## **2.2 Unconditional Frame Skip at Start of Every Dialogue Task**

| **File** | RunningTask.cs - RunWrappedTask() |
| --- | --- |
| **Root cause** | await UniTask.Yield() is the first instruction in RunWrappedTask, unconditionally delaying start by one frame for all tasks |
| **Consequence** | When StartDialogue() is called on frame N, the dialogue box Show() call doesn't execute until frame N+1. Depending on animation timing, this creates a one-frame flicker or blank frame before the UI appears |

private async UniTask&lt;TaskResult<T&gt;> RunWrappedTask(...)

{

await UniTask.Yield(); // <-- Unconditional 1-frame delay

This was likely added to ensure async context is properly initialized. However, it applies to all tasks including dialogue, causing the delay. If removed from RunningTask globally, background tasks should use UniTask.SwitchToThreadPool() explicitly. For the dialogue specifically, this is another reason to not use TaskRunner at all (see 2.1).

## **2.3 WaitForAdvance Does Not Propagate CancellationToken to Auto-Advance Delay**

| **File** | DialogueManager.cs - WaitForAdvance() |
| --- | --- |
| **Root cause** | In auto-advance mode, WhenAny is awaited but the result is discarded; and in both modes, if the CancellationToken fires while WhenAny is in-flight, the auto-advance delay task continues running orphaned |
| **Consequence** | On scene transition or SkipDialogue(), the dialogue task's CancellationToken fires. The advance signal TCS has token.Register to cancel it, but the delay task from UniTask.Delay doesn't get cancelled because the WhenAny winner was discarded |

var winner = await UniTask.WhenAny(inputTask, delayTask); // winner discarded

The delay task should use the outer CancellationToken so it cancels cleanly:

delayTask = UniTask.Delay((int)(defaultAutoAdvanceDelay \* 1000), cancellationToken: token);

The token is already being passed in to WaitForAdvance - it just needs to be forwarded to the delay.

## **2.4 SoundEmitter Uses Polling Coroutine with 100ms Interval**

| **File** | SoundEmitter.cs - WaitForSoundToFinish() |
| --- | --- |
| **Root cause** | Every active SoundEmitter runs a coroutine that yields WaitForSecondsRealtime(0.1f) in a while loop, polling audioSource.isPlaying 10 times per second |
| **Consequence** | With 10 simultaneous voice/sfx emitters active, that's 100 coroutine wake-ups per second purely for completion detection. The pool can hold up to 100 emitters (MaxPoolSize) |

while (\_audioSource != null && (\_audioSource.isPlaying || \_isPaused))

{

yield return new WaitForSecondsRealtime(0.1f);

}

The correct approach is to yield until the clip duration has elapsed, using the clip length directly:

float duration = \_audioSource.clip.length / Mathf.Abs(\_audioSource.pitch);

yield return new WaitForSecondsRealtime(duration);

// Then check isPlaying as a single guard, not in a loop

For looping sounds, the current polling approach must remain, but it should only be used when soundData.loop is true. Non-looping sounds (the vast majority) can use the duration-based yield.

## **2.5 DialogueExample Uses System.Threading.Tasks.Task.Run to Monitor a UniTask**

| **File** | DialogueExample.cs - Start() |
| --- | --- |
| **Root cause** | Task.Run() launches a .NET thread pool thread that polls dialogueTask.Progress and dialogueTask.IsCompleted, both of which are non-thread-safe properties on RunningTask&lt;T&gt; |
| **Consequence** | Race condition on IsCompleted and Progress (no volatile/Interlocked on either property read from a background thread). This is a data race that can cause torn reads or infinite loops |

_ = Task.Run(async () => {

while (!dialogueTask.IsCompleted) {

Debug.Log(\$"Dialogue Progress: {dialogueTask.Progress:P0}");

await Task.Delay(100);

}

});

This is example/demo code, but it demonstrates incorrect usage of the system. RunningTask.IsCompleted is a plain bool (not volatile), and Progress is a plain float - neither is safe to read from a thread pool thread while the main thread writes them. The example should use OnCompleted callback or await the CurrentTask directly on the main thread.

# **3\. Input Subscription Correctness Issues**

## **3.1 Skip-Typing and Advance-Line Subscribe to the Same Actions Simultaneously**

| **File** | DialogueManager.cs - ProcessDialogueLine() |
| --- | --- |
| **Root cause** | onSkipTyping and onAdvance are both subscribed to 'Interact' and 'Skip' at the same time during ShowLine and WaitForAdvance respectively - with no ordering guarantee |
| **Consequence** | If ShowLine completes instantly (e.g. very short text or instant speed), the player's press to skip typing also immediately fires the advance signal. The intended UX of 'first press skips, second press advances' requires two distinct phases with separate subscription windows |

The two subscriptions are sequentially scoped (ProcessDialogueLine subscribes onSkipTyping around ShowLine, then WaitForAdvance subscribes onAdvance), but because ShowLine's TypeText can complete near-instantaneously and the advance signal TCS is created before the input subscription, a fast press can race between the two windows.

The fix is to make the state machine explicit: track whether typing is currently in progress as a shared flag, and have a single input handler that dispatches to either SkipTyping or Advance based on the flag. This is a standard double-tap-to-advance pattern.

## **3.2 DialogueTrigger Subscribes to 'Interact' Globally Without Range Check During Dialogue**

| **File** | DialogueTrigger.cs - OnEnable() / OnInteract() |
| --- | --- |
| **Root cause** | Every enabled DialogueTrigger subscribes to 'Interact' via InputReader. If the player is in dialogue and walks near a second trigger, pressing Interact fires both the dialogue advance and the second trigger's OnInteract |
| **Consequence** | Triggers can start a second dialogue while the first is still running. The guard in StartDialogue() prevents the second from running, but it logs a warning and returns null, which is silent from the player's perspective |

The DialogueTrigger should check whether a dialogue is currently active before acting on Interact. The cleanest solution is for DialogueTrigger to check IDialogueService.IsActive (once the interface exists), or to have DialogueManager disable all Interact subscriptions other than its own while a sequence is running.

## **3.3 InputReader.SubscribePerformed Does Not Guard Against Duplicate Subscriptions**

| **File** | InputReader.cs - SubscribePerformed() |
| --- | --- |
| **Root cause** | The method uses += on the delegate without checking for duplicate subscribers. C# multicast delegates will call the same callback multiple times if it is registered twice |
| **Consequence** | If ProcessDialogueLine is re-entered or Subscribe is called twice for any reason, the callback fires twice per input event. This could double-advance dialogue or double-skip typing |

if (\_performedSubscriptions.TryGetValue(actionName, out var existing))

\_performedSubscriptions\[actionName\] = existing + callback; // no duplicate check

A defensive guard: before adding, subtract the callback first to ensure it's not already present, then add it:

existing = existing - callback + callback; // idempotent subscribe

# **4\. Correctness Bugs**

## **4.1 Struct Null-Check Is Always True (NullReferenceException Risk)**

| **File** | DialogueManager.cs - ProcessDialogueLine() |
| --- | --- |
| **Root cause** | DialogueCutsceneData is a value type (struct). Calling .Equals(null) on a struct always returns false, so !line.cutsceneData.Equals(null) is always true |
| **Consequence** | TriggerCutsceneEvents is called for every line when useCutsceneVisuals is true, even lines with no cutscene data. Inside TriggerCutsceneEvents, the code iterates data.characterUpdates and data.propUpdates - both are List&lt;T&gt; fields on a struct, so their default value is null. This will throw NullReferenceException on any line that does not explicitly initialize those lists |

// BUG: this is always true for a struct

if (useCutsceneVisuals && !line.cutsceneData.Equals(null))

{

TriggerCutsceneEvents(line.cutsceneData);

}

// Inside TriggerCutsceneEvents:

foreach (CharacterUpdate update in data.characterUpdates) // NPE if list is null

Two fixes needed: (a) change DialogueCutsceneData from struct to class so it can be null, and (b) add null-guards on the list fields in TriggerCutsceneEvents. Or add a HasData property to the struct that checks the lists:

public bool HasData => (characterUpdates != null && characterUpdates.Count > 0)

|| (propUpdates != null && propUpdates.Count > 0);

## **4.2 AudioSource.Stop() Called Twice in OnReturnToPool**

| **File** | SoundEmitter.cs - OnReturnToPool() |
| --- | --- |
| **Root cause** | Copy-paste error: \_audioSource.Stop() appears on two consecutive lines |
| **Consequence** | Harmless but indicates the method was not reviewed after editing |

\_audioSource.Stop();

\_audioSource.Stop(); // duplicate

## **4.3 LocalizationService.Instance Is Set in Constructor (Singleton Anti-Pattern)**

| **File** | LocalizationService.cs - constructor |
| --- | --- |
| **Root cause** | The constructor assigns \_instance = this, making this service a dual-access object: ServiceLocator-registered AND globally accessible via the static Instance property |
| **Consequence 1** | LocalizedStringReference.GetLocalizedValue() bypasses ServiceLocator entirely by calling LocalizationService.Instance directly. This means it cannot be mocked or substituted in tests, and it will silently return fallback text in any scene that hasn't fully initialized |
| **Consequence 2** | If LocalizationService is instantiated twice (e.g. in tests or from multiple scenes), the second instance silently replaces the first in \_instance. Any outstanding LocalizedStringReference objects then resolve against the new instance |

The static Instance property is already marked \[Obsolete\], but LocalizedStringReference still uses it. The fix is to inject the localization service properly into wherever string resolution happens. For inline structs like LocalizedStringReference that cannot hold injected references, the standard pattern is a static service accessor that uses ServiceLocator.Global rather than a static field set by the constructor:

public string GetLocalizedValue(params object\[\] args)

{

if (!ServiceLocator.Global.TryGet&lt;LocalizationService&gt;(out var svc))

return FallbackText;

return svc.GetString(Key, FallbackText);

}

## **4.4 DialogueBox Registers With DialogueManager in Start() With No Cleanup in OnDestroy**

| **File** | DialogueBox.cs - Start() |
| --- | --- |
| **Root cause** | DialogueBox calls dm.RegisterView(this) but never calls dm.RegisterView(null) or equivalent in OnDestroy or OnDisable |
| **Consequence** | If the DialogueBox prefab is destroyed during a scene transition while a dialogue is running, DialogueManager holds a reference to a destroyed MonoBehaviour. The next call to \_activeView.Show(), ShowLine(), or Hide() will call methods on a null Unity Object, throwing MissingReferenceException |

// Missing:

private void OnDestroy()

{

var dm = ServiceLocator.Global?.Get&lt;DialogueManager&gt;();

if (dm != null && dm.ActiveView == (IDialogueView)this)

dm.SetView(null);

}

## **4.5 CharacterData Mixes Gameplay and Dialogue Data in One ScriptableObject**

| **File** | CharacterData.cs |
| --- | --- |
| **Root cause** | CharacterData contains walkSpeed, sprintSpeed, jumpForce, gravityMultiplier, airControl, maxFallSpeed alongside characterName, defaultPortrait, and emotions |
| **Consequence** | Every dialogue line that references a character loads all physics/movement data into memory even though it's irrelevant to the dialogue system. When loading from Addressables, the entire SO must be resident. This also means NPC characters (who may not be player-controlled) carry movement data fields that don't apply to them |

Separate into CharacterData (name, portrait, emotions) and CharacterMovementData (speeds, physics). DialogueLine references CharacterData only. Player components reference CharacterMovementData. The two can be linked by a shared CharacterDefinition if needed.

# **5\. UI Toolkit-Specific Issues**

## **5.1 Per-Character Text Assignment Triggers Full Label Layout on Every Frame**

| **File** | DialogueBox.cs - TypeText() |
| --- | --- |
| **Root cause** | Setting dialogueLabel.text on every iteration of the typing loop marks the Label element dirty, causing UI Toolkit to re-run its text layout pipeline (font metrics, line-wrapping, kerning) on every tick during typewriter effect |
| **Consequence** | UI Toolkit's text layout is not cheap. For a 200-character line at 30ms per character, the layout pipeline runs ~33 times per second for the duration of that line. This is the single largest rendering cost in the dialogue system |

The correct approach for UI Toolkit is to set the full text on the label once and use a visible character count mechanism. Two options:

- Use USS -unity-text-overflow-position with a clip mask to reveal text progressively by animating the width of a containing VisualElement - zero text layout updates after initial set.
- Use a RichText approach with an invisible color span (e.g. &lt;color=#00000000&gt;) for the unrevealed portion, swapping visible/invisible character regions - only one text.Length assignment at start, then only span start index changes.

Option 2 is available natively in UI Toolkit and requires only one additional tag character per frame rather than a substring allocation.

## **5.2 DialogueBox Rebuilds UI Tree Each Time BuildUIIfNeeded Is Called**

| **File** | DialogueBox.cs - BuildUIIfNeeded() |
| --- | --- |
| **Root cause** | BuildUIIfNeeded calls root.Clear() and reconstructs the full VisualElement hierarchy when mainContainer is null or detached. This can happen on each Show() if the overlay canvas rebuilds the root |
| **Consequence** | Creating 10+ VisualElements procedurally, assigning USS classes, and attaching them to the tree all triggers layout invalidation. If this occurs on the frame the dialogue box opens, it produces a layout stall before the first line appears |

The tree should be constructed in UXML, queried once in Awake/OnEnable, and then only data (text, sprites, visibility) should change at runtime. See Section 3.7 of the previous design review for rationale. For the immediate fix, ensure BuildUIIfNeeded is only called once per lifecycle, not per Show() call.

# **6\. Minor Issues & Code Hygiene**

## **6.1 Commented-Out Code and Unresolved Reasoning in Hot Path**

ProcessDialogueLine contains 5 lines of inline reasoning about a previous refactoring that removed UpdateDialogueUI. This is the hot path called on every line and should not contain self-dialogue about incomplete edits. The intent should be captured in a commit message or design doc, not in production code.

## **6.2 DialogueBox.ShowLine Duplicates Character Resolve Logic from SetupStaticVisuals**

Approximately 50 lines of code are identical between SetupStaticVisuals (called once during Show()) and ShowLine (called per line). Both resolve displayName and displaySprite from the same CharacterData/emotion path, and both set left/right portrait visibility with the same scale-flip logic. Any fix to portrait rendering must be applied in both places or it will regress. Extract to a private void UpdateSpeakerVisuals(DialogueLine line) method.

## **6.3 LocalizedStringReference Null-Check Uses Wrong Field**

In DialogueBox.ShowLine, text resolution guards against a null Key:

if (line.text.Key != null)

finalText = line.text.GetLocalizedValue();

LocalizedStringReference.Key is a string field. An empty string ("") passes this null check and is forwarded to LocalizationService.GetString("", ...), which triggers a LogWarning per line. The guard should be:

if (!string.IsNullOrEmpty(line.text.Key))

## **6.4 SetupStaticVisuals Portrait Scale-Flip Comments Are Inverted**

The code and its comments contradict each other:

// Left speaker: 'Assume sprite faces Right. Left speaker faces Right (scale 1).'

leftPortrait.style.scale = new Scale(new Vector3(-1, 1, 1)); // scale is -1, not 1

// Right speaker: 'Right speaker faces Left (scale -1).'

rightPortrait.style.scale = new Scale(new Vector3(1, 1, 1)); // scale is 1, not -1

The scales are swapped relative to the comments. One of them is wrong. This needs verification against the actual sprite orientation.

# **7\. Full Issue Index**

| **Severity** | **Location** | **Category** | **Issue** |
| --- | --- | --- | --- |
| **Critical** | DialogueManager.cs | Correctness | struct null-check always true → NPE in TriggerCutsceneEvents |
| **Critical** | DialogueBox.cs | Performance | O(n²) string allocs + O(n²) layout passes during typewriter effect |
| **Critical** | DialogueSequenceIdDrawer.cs | Editor Perf | AssetDatabase.FindAssets called every OnGUI frame |
| **Critical** | DialogueExample.cs | Threading | Task.Run polling non-thread-safe RunningTask properties |
| **High** | DialogueManager.cs | Architecture | Dialogue wrapped in TaskRunner: 1-frame delay, global pause risk, shutdown stall |
| **High** | DialogueBox.cs | UI Perf | Per-character text assignment triggers full UI Toolkit layout pass each tick |
| **High** | DialogueManager.cs | Memory | Lambda delegate allocations per dialogue line (2 closures × N lines) |
| **High** | LocalizationService.cs | Correctness | Static Instance anti-pattern: bypasses DI, unsafe if created twice |
| **High** | DialogueBox.cs | Correctness | No OnDestroy cleanup → MissingReferenceException if destroyed mid-dialogue |
| **High** | DialogueManager.cs | Input | Skip-typing and advance-line subscribe simultaneously: phase race condition |
| **Medium** | RunningTask.cs | Async | Unconditional UniTask.Yield() adds 1-frame delay to all tasks including dialogue |
| **Medium** | DialogueManager.cs | Async | Auto-advance delay task not passed CancellationToken - orphaned on cancel |
| **Medium** | SoundEmitter.cs | Performance | 100ms polling coroutine per active emitter instead of clip-duration yield |
| **Medium** | DialogueTrigger.cs | Input | Global Interact subscription: second trigger fires during active dialogue |
| **Medium** | InputReader.cs | Correctness | No duplicate subscription guard: same callback fires N times if added N times |
| **Medium** | CharacterData.cs | Memory/Design | LINQ FirstOrDefault scan per displayed line - should use cached Dictionary |
| **Medium** | DialogueManager.cs | Memory | Legacy voiceClip path allocates new SoundData per line in hot path |
| **Medium** | CharacterData.cs | Design | Movement/physics data co-located with dialogue portrait data |
| **Medium** | DialogueBox.cs | Design | UI tree built procedurally - should use UXML; rebuild on Show() causes layout stall |
| **Low** | SoundEmitter.cs | Hygiene | AudioSource.Stop() called twice in OnReturnToPool |
| **Low** | DialogueManager.cs | Hygiene | Commented-out refactoring reasoning left in ProcessDialogueLine hot path |
| **Low** | DialogueBox.cs | Hygiene | 50-line portrait setup code duplicated across SetupStaticVisuals and ShowLine |
| **Low** | DialogueBox.cs | Hygiene | Empty if-block in ApplyThemeAndState (if \_initialLine == null with no body) |
| **Low** | DialogueBox.cs | Correctness | Portrait scale-flip values contradict their inline comments |
| **Low** | DialogueBox.cs | Correctness | Localized key null-check should use IsNullOrEmpty to prevent empty-key warnings |

_End of audit. 25 issues identified across 9 files. All findings are based on static analysis of branch: mysery_of_the_scarab_core._