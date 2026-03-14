**Dialogue System - Code Review & Design Evaluation**

Mystery of the Scarab · Core Repository | JuveNILE Games

March 2026

# **Executive Summary**

The dialogue system is functional but architecturally immature. It will become a significant maintenance liability as the game's narrative scope grows. The core data model uses a flat list with raw integer jump indices for branching, there is no interface abstraction over the manager, the view is hard-coupled to Easy Cutscene internals, and the DialogueBox contains substantial code duplication alongside dead/commented-out code from incomplete refactoring. These are not minor polish issues - they affect how scalable, testable, and extendable the system is in practice.

The good news: the IDialogueView abstraction is a solid foundation, the async/UniTask pipeline is appropriate, and the injection pattern is consistent with the rest of Core. The system is worth refactoring rather than replacing.

# **1\. Architectural Issues**

## **1.1 No IDialogueService Interface**

Every other significant system in Core exposes an interface (INavigationService, ISaveService, IAssetManagementService, etc.). DialogueManager breaks this pattern by being a concrete MonoBehaviour with no interface. This has two practical consequences:

- It cannot be mocked or substituted in tests.
- DialogueTrigger and any game code that calls StartDialogue() must take a hard dependency on the concrete class, not an abstraction.

The fix is straightforward - extract IDialogueService with the public surface (StartDialogue, SkipDialogue, GetCurrentLine, events) and register it through ServiceLocator like every other service.

## **1.2 DialogueSequence Is Not a ScriptableObject**

DialogueSequence is a plain \[System.Serializable\] class, meaning all dialogue data lives nested inside DialogueDatabase. This has several consequences:

- Sequences cannot be individually version-controlled, referenced by GUID, or loaded via Addressables.
- The database serializes as one monolithic .asset, causing merge conflicts on every dialogue edit.
- There is no path to streaming or incremental loading - the entire dialogue corpus is loaded with the database.

In a mystery/narrative game where dialogue is a core content type, sequences should be ScriptableObjects. Each sequence lives in its own .asset file, the database becomes a registry (list of references), and authoring can be distributed across the team without merge conflicts.

## **1.3 Branching Uses Raw Integer Indices**

This is the most fundamental structural problem. DialogueChoice.nextLineIndex is an integer offset into a flat List&lt;DialogueLine&gt;. The branching "graph" is actually:

i = nextIndex - 1; // -1 because loop will increment

This is the classic naive approach and it breaks badly in practice:

- Any insertion or reordering of lines invalidates all jump targets silently - no compiler error, no warning.
- Non-linear narrative (flashbacks, looping conversations, conditional revisits) cannot be expressed without increasingly fragile index arithmetic.
- There is no way to express "go to a different sequence" from a choice - branching is local only.

The industry-standard approach is a node graph where each line/node holds a reference to its successor(s) by stable ID, not positional index. Tools like Yarn Spinner and Ink use this model specifically because integer indices are unworkable at content scale.

## **1.4 DialogueBox Self-Registration Is Backwards**

In Start(), DialogueBox reaches into ServiceLocator.Global to find DialogueManager and calls RegisterView(this):

var dm = ServiceLocator.Global?.Get&lt;DialogueManager&gt;();

if (dm != null) dm.RegisterView(this);

This inverts the dependency relationship. The view should not know about the manager - the manager (or a higher-level coordinator) should know about the view. In the current design, if DialogueBox is not yet initialized when a dialogue starts, the view is null and the sequence fails silently. The correct model is for DialogueManager to receive its view via injection or explicit configuration, not via discovery from the view side.

# **2\. Data Model Issues**

## **2.1 Migration Debt Baked Into DialogueLine**

DialogueLine carries both old and new fields simultaneously with no enforcement:

- voiceClip (AudioClip) alongside voiceData (SoundData), with a tooltip saying 'Legacy: Use voiceData instead'.
- speakerName (string) alongside character (CharacterData) and emotion (EmotionType), with a comment 'Manual Override (Legacy)'.

Both of these dual-field patterns make every consumer of DialogueLine branch on which version of the data is populated. The ProcessDialogueLine method already does this. The correct approach is to complete the migration: strip the legacy fields, write a one-time migration utility, and have a clean model going forward.

## **2.2 DialogueCutsceneData Is a Struct With Reference Type Fields**

DialogueCutsceneData is a struct containing List&lt;CharacterUpdate&gt; and List&lt;PropUpdate&gt;. This is a Unity anti-pattern. A struct with reference-type fields behaves unexpectedly:

- Default value is not null - it is an empty struct with null lists. The null-check in ProcessDialogueLine is incorrect:

if (useCutsceneVisuals && !line.cutsceneData.Equals(null))

This condition is always true for a struct. The actual guard should check the lists themselves, or the type should be a class.

- Copying a DialogueLine copies the struct but not its lists - the two copies share the same List instances.

## **2.3 UnityEvents on Data Objects**

DialogueSequence has onSequenceStart and onSequenceEnd as UnityEvents, and DialogueLine has onLineStart and onLineEnd. UnityEvents serialized on data objects create persistent editor references from data assets to scene objects, which causes dependency leaks and makes it impossible to load a sequence without also loading whatever objects its events reference.

DialogueManager already fires C# events (OnDialogueStarted, OnDialogueCompleted, OnLineStarted, OnLineCompleted) for the same purposes. The UnityEvents on the data objects are redundant and should be removed in favour of the C# event pattern.

# **3\. Implementation Issues**

## **3.1 Code Duplication in DialogueBox**

The visual setup logic for speaker name and portrait is implemented twice, identically, in:

- SetupStaticVisuals(DialogueLine line) - called from ApplyThemeAndState
- ShowLine(DialogueLine line) - called per line during dialogue

About 50 lines of code are copy-pasted verbatim, including the flip-logic comment. This means any change to how portraits or names are displayed must be made in two places. Both methods should delegate to a single private UpdateSpeakerVisuals(DialogueLine line) method.

## **3.2 Commented-Out and Confused Code in DialogueManager**

ProcessDialogueLine contains comments that indicate an incomplete refactoring:

// UpdateDialogueUI(line); // Removed in previous edits, View handles this now?

// WAIT: I removed UpdateDialogueUI method but in previous step I just replaced ProcessDialogueLine.

// But wait, the previous code snippet \*removed\* UpdateDialogueUI call because...

This is debugging commentary that was left in production code. It signals the method was edited incrementally without a final cleanup pass. The method should be tidied and the reasoning made clear in a brief comment if needed.

## **3.3 O(n) Database Lookup on Every StartDialogue Call**

DialogueDatabase.GetSequence uses LINQ FirstOrDefault over a list:

return sequences.FirstOrDefault(s => s.id == id);

For a small database this is negligible, but it is trivially fixed. The database should build a Dictionary&lt;string, DialogueSequence&gt; on first access (or in OnEnable) and use that for O(1) lookups. This also makes duplicate-ID bugs obvious at authoring time rather than runtime.

## **3.4 Typing Uses O(n²) Substring**

The TypeText loop builds text incrementally using Substring(0, i) on each frame:

dialogueLabel.text = text.Substring(0, i);

Substring allocates a new string on every character, so a 200-character line allocates roughly 200 strings. For a UI Toolkit label, the correct approach is to either use a StringBuilder and assign .ToString() each iteration, or - better for UI Toolkit - use visible character count via rich text tags or a custom shader approach to reveal text without per-frame allocation.

## **3.5 WaitForAdvance Discards Auto-Advance Winner**

In auto-advance mode, the code awaits WhenAny but never uses the result to determine whether input or the timer won:

var winner = await UniTask.WhenAny(inputTask, delayTask);

winner is assigned but never read. This means if the player presses advance early, the code correctly advances, but there is no mechanism to distinguish 'player skipped' from 'auto-advanced' for analytics, UI feedback, or logic that might differ between the two cases. The variable should be used or the comment should explain why it is intentionally discarded.

## **3.6 Hard Coupling to Easy Cutscene Plugin**

DialogueManager directly references EcCutsceneManager, EcCharacter, EcProps, EcTransformSetting, and EcCutscene from the Easy Cutscene plugin. This means:

- The dialogue system cannot be used without Easy Cutscene present.
- Any migration away from Easy Cutscene requires changes to DialogueManager's core logic.

Cutscene integration should be handled through an ICutsceneDirector interface that DialogueManager holds a reference to. Easy Cutscene becomes one implementation of that interface. The dialogue system doesn't need to know what drives the visuals.

## **3.7 DialogueBox BuildUI Is Fully Code-Driven**

The project uses UI Toolkit (UIDocument, VisualElement) extensively, and the rest of the navigation/overlay system uses UXML templates. DialogueBox instead builds its entire element tree in code (BuildUIIfNeeded), creating named elements procedurally and applying styles via class names. This is harder to iterate on visually and bypasses the UI Builder workflow. The UI should be expressed as a .uxml file, with DialogueBox's code limited to querying named elements and updating their content.

# **4\. Issue Summary**

| **Header** | Issue |
| --- | --- |
| **Critical** | Branching uses raw integer indices - breaks on any reorder |
| **Critical** | DialogueSequence is not a ScriptableObject - monolithic asset, no Addressables support |
| **High** | No IDialogueService interface - untestable, breaks Core DI pattern |
| **High** | DialogueBox self-registers - inverted dependency, fragile init ordering |
| **High** | Hard coupling to Easy Cutscene - ICutsceneDirector abstraction needed |
| **Medium** | Migration debt in DialogueLine - dual voiceClip/voiceData, speakerName/character fields |
| **Medium** | DialogueCutsceneData is a struct with List fields - incorrect null guard |
| **Medium** | UnityEvents on data objects - causes serialized editor dependencies |
| **Medium** | 50-line code duplication in DialogueBox portrait/name setup |
| **Low** | O(n) database lookup - should use Dictionary |
| **Low** | O(n²) Substring allocation in TypeText |
| **Low** | WaitForAdvance discards auto-advance winner |
| **Low** | Commented-out refactoring notes left in ProcessDialogueLine |
| **Low** | DialogueBox UI built in code - should use UXML template |

# **5\. Recommended Approach**

The following order is suggested based on impact and dependency:

### **Step 1 - Fix the Data Model (unblocks everything else)**

- Convert DialogueSequence to a ScriptableObject.
- Change DialogueChoice.nextLineIndex to reference a DialogueSequence ID (string), not an integer. This makes branching cross-sequence and refactor-safe.
- Remove legacy fields (voiceClip, speakerName) after writing a migration utility.
- Change DialogueCutsceneData from struct to class, or remove the UnityEvent from it.

### **Step 2 - Add IDialogueService**

- Extract the public API into an interface.
- Register DialogueManager as IDialogueService in CoreServicesInstaller.
- Update all consumers (DialogueTrigger, DialogueBox, game code) to reference the interface.

### **Step 3 - Fix DialogueBox**

- Extract UpdateSpeakerVisuals to eliminate the duplication.
- Move to a UXML-based layout.
- Remove the self-registration from Start() - accept view injection from outside.

### **Step 4 - Add ICutsceneDirector**

- Define interface with methods like SetupForSequence, UpdateCharacter, UpdateProp.
- Wrap EcCutsceneManager calls in an EasyCutsceneDirector implementation.
- Inject ICutsceneDirector into DialogueManager, remove direct plugin references.

### **Step 5 - Evaluate Yarn Spinner or Ink**

If the narrative scope of Mystery of the Scarab is significant (multiple characters, conditional story flags, branching paths), the hand-rolled system will struggle to keep up. Yarn Spinner integrates cleanly with Unity, outputs to ScriptableObject-compatible data, and has first-class tooling for writers. Ink has a deeper branching model and strong tooling. Either would replace the flat-list+integer-index approach with a proper authoring workflow. This is worth evaluating before the dialogue content volume grows too large to migrate comfortably.

_End of review. All findings are based on static analysis of the Core repository, branch: mysery_of_the_scarab_core._