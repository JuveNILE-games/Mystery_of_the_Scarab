**Yarn Spinner Integration - Design & Migration Document**

Mystery of the Scarab · Core Repository | JuveNILE Games | March 2026

# **1\. Why Yarn Spinner - Problems It Directly Solves**

Each point below is a problem identified in the technical audit, paired with the specific Yarn Spinner mechanism that eliminates it rather than patches it.

## **1.1 Flat List + Integer Index Branching → Node Graph**

The current system encodes dialogue branching as DialogueChoice.nextLineIndex, a raw integer offset into a flat List&lt;DialogueLine&gt;. Any reorder of lines silently breaks all jump targets.

Yarn's script format is node-based. Every unit of dialogue is a named node - a self-contained block with a title header. Choices jump to nodes by name, not by position:

title: Sarcophagus_Examine

\---

Amira: This seal hasn't been broken in three thousand years.

\-> Ask about the hieroglyphs

&lt;<jump Sarcophagus_Hieroglyphs&gt;>

\-> Leave it alone

&lt;<jump MainHall&gt;>

\===

Node names are stable identifiers. Reordering, adding, or removing lines inside a node does not affect any jump that references it by name. Branching across sequences (from one conversation to another) is native: &lt;<jump AnotherNode&gt;> works across files in the same YarnProject.

## **1.2 Monolithic DialogueDatabase ScriptableObject → Distributed .yarn Files**

DialogueDatabase is a single ScriptableObject holding all sequences, causing merge conflicts on every dialogue edit. Yarn Spinner uses plain text .yarn files - one file per scene, character, chapter, or whatever granularity the team prefers. These are compiled by the Unity importer into a YarnProject asset that acts as an index. Each writer works in their own file, with zero merge conflict surface.

The YarnProject asset itself is small (it's a reference manifest). The actual content lives in the .yarn text files, which are committed like any other source file and diff cleanly.

## **1.3 No Narrative Variables → First-Class Variable System**

The current DialogueLine has no mechanism for conditional dialogue - a line either runs or it doesn't based on sequence order. There is no way to write 'show this line only if the player already found the key.'

Yarn supports typed variables (\$strings, \$numbers, \$booleans) and conditions natively:

&lt;<if \$foundTheKey&gt;>

Amira: The lock mechanism matches the key you found earlier.

&lt;<else&gt;>

Amira: There must be some kind of locking mechanism here.

&lt;<endif&gt;>

Variables are stored in a VariableStorageBehaviour, which Core can implement as a bridge to its own SaveService - so narrative state persists automatically across sessions without any additional plumbing.

## **1.4 O(n) String Scan on DialogueDatabase → YarnProject Compiled Lookup**

DialogueDatabase.GetSequence uses LINQ FirstOrDefault over a list, scanning linearly for a matching ID. The YarnProject is pre-compiled by the Unity editor importer into a program bytecode format. At runtime, node lookup is a dictionary access by name - O(1), and performed by Yarn Spinner internally, not in game code.

## **1.5 Commented-Out Refactoring Debt and Dead Migration Fields → Clean Data Model**

DialogueLine currently carries both voiceClip (legacy) and voiceData (current), and both speakerName (legacy) and character/emotion (current). Yarn Spinner has no opinion about how your game stores character data - it delivers a line with a character name and text, and your DialoguePresenter decides how to resolve portrait, audio, and emotion. The migration confusion goes away because Yarn doesn't hold those fields at all.

## **1.6 DialogueCutsceneData Struct Bug → Commands**

The NullReferenceException caused by the struct null-check (the most critical bug in the audit) exists because character and prop changes are embedded as data fields on DialogueLine. In Yarn Spinner, scene direction is expressed as commands - instructions to Unity that execute immediately when reached:

&lt;<expression amira worried&gt;>

Amira: I don't like this...

&lt;<move amira stage_left&gt;>

Commands are decoupled from line data entirely. They are not structs on lines - they are separate bytecode instructions the runner encounters in sequence. There is nothing to null-check.

## **1.7 LocalizationService Integration → Built-In Line Provider Bridge**

The current system has LocalizedStringReference calling LocalizationService.Instance (a static singleton anti-pattern) per line. Yarn Spinner's line resolution goes through a LineProvider - an abstract class specifically designed to be replaced. Core can implement a LineProvider that delegates to its own LocalizationService, keeping full control over how translated text is fetched and cached.

# **2\. Yarn Spinner 3 Architecture - Key Components**

Before mapping to the existing codebase, here is a concise reference of the Yarn Spinner 3 components that the integration will touch.

| **DialogueRunner (MonoBehaviour)** | The central hub. Owns the Yarn program, drives execution, dispatches lines/options/commands to Presenters, and exposes StartDialogue(nodeName), Stop(), and events (onDialogueStart, onDialogueComplete, onNodeStart, onNodeComplete). |
| --- | --- |

| **YarnProject (.asset)** | Compiled output of one or more .yarn files. Produced automatically by the Unity editor importer. Assigned to DialogueRunner in the Inspector. Multiple YarnProjects can exist for different subsystems (story dialogue vs. NPC barks vs. journal entries). |
| --- | --- |

| **DialoguePresenterBase** | Abstract base class (Yarn Spinner 3) for anything that displays dialogue to the player. Replaces the old callback-based DialogueViewBase from v2. Presenters receive lines via async RunLineAsync(LocalizedLine, LineCancellationToken) and options via RunOptionsAsync(DialogueOption\[\], CancellationToken). Cancellation tokens replace the old requestInterrupt / MarkLineComplete pattern. |
| --- | --- |

| **LineCancellationToken** | Passed to RunLineAsync alongside the line. Two states: IsNextLineRequested (player pressed advance) and IsNextContentRequested (content has been replaced/interrupted). Presenters poll or await these instead of registering input callbacks themselves. |
| --- | --- |

| **LineProvider / ILineProvider** | Abstract class responsible for resolving a line ID (a stable string like 'line:abc123') into a LocalizedLine containing the actual text, character name, and associated metadata. Custom implementations can bridge to any localization backend. |
| --- | --- |

| **VariableStorageBehaviour** | Abstract MonoBehaviour that Yarn Spinner calls to read/write \$variables. Implement TryGetValue&lt;T&gt;, SetValue (string/float/bool overloads), GetAllVariables, SetAllVariables, and Clear. The backing store can be anything - including Core's SaveService. |
| --- | --- |

| **\[YarnCommand\] / \[YarnFunction\]** | C# attributes (from Yarn.Unity.Attributes namespace in v3). Any MonoBehaviour method decorated with \[YarnCommand("name")\] becomes callable from Yarn scripts as &lt;<name args&gt;>. Static methods decorated with \[YarnFunction("name")\] become callable as inline expressions. Commands can be async (returning IEnumerator or UniTask). |
| --- | --- |

| **Markup Attributes** | Yarn supports inline markup in line text: \[wave\]hello\[/wave\]. The markup system parses these into MarkupAttribute structs attached to the LocalizedLine. Custom IAttributeMarkerProcessor implementations can process them before the line reaches the Presenter. Used for character names, animations, text effects. |
| --- | --- |

| **Line Tags / Metadata** | Hashtag annotations on individual lines (#emotion:worried, #speaker_side:right). Accessible via LocalizedLine.Metadata as a string array. Used for presenter-level hints that don't belong in the script logic itself - portrait side, audio cue, animation override. |
| --- | --- |

# **3\. Concept Mapping - Current System to Yarn Spinner 3**

| **Current (Core)** | **Yarn Spinner 3 equivalent** | **Action required** |
| --- | --- | --- |
| DialogueManager | DialogueRunner | Delete. Wire runner into ServiceLocator. |
| IDialogueView | DialoguePresenterBase | Rename/refactor. API changes significantly. |
| DialogueBox | Custom DialoguePresenter | Refactor to extend DialoguePresenterBase. |
| DialogueSequence | YarnProject + node | Delete. Content moves to .yarn files. |
| DialogueDatabase | YarnProject asset | Delete. YarnProject is the registry. |
| DialogueLine | LocalizedLine (Yarn runtime type) | Delete. Data model replaced entirely. |
| DialogueChoice | DialogueOption\[\] | Delete. Choices are first-class in Yarn. |
| DialogueTrigger | dialogueRunner.StartDialogue(node) | Simplify to one-line caller. |
| DialogueCutsceneData | \[YarnCommand\] methods | Delete struct. Use command attributes. |
| EcCutsceneManager calls | \[YarnCommand\] on EcCutscene wrapper | Wrap Easy Cutscene in command methods. |
| LocalizedStringReference | Custom LineProvider → LocalizationService | Build CoreLineProvider bridge. |
| SaveService (narrative) | Custom VariableStorageBehaviour | Build CoreVariableStorage bridge. |
| EventBus signals | DialogueRunner Unity Events | Wire onDialogueStart/Complete to EventBus. |
| InputReader (Interact) | LineAdvancer component | Wire InputReader to LineAdvancer adapter. |

# **4\. Integration Steps - Detailed**

## **4.1 Install Yarn Spinner for Unity**

Yarn Spinner for Unity 3.x is distributed via the Unity Package Manager. Add the following entry to your project's Packages/manifest.json:

{

"dependencies": {

"dev.yarnspinner.unity": "3.1.1"

}

}

The package registry entry to add to scopedRegistries:

{

"name": "Yarn Spinner",

"url": "<https://pkg.yarnspinner.dev/>",

"scopes": \["dev.yarnspinner"\]

}

After installation, the editor imports .yarn files automatically and can generate YarnProject assets via the Assets → Create → Yarn Spinner → Yarn Project menu. The Yarn Spinner Editor extension (VS Code plugin or standalone Yarn Spinner Editor) is recommended for writers.

## **4.2 Replace DialogueManager with DialogueRunner**

DialogueRunner is a MonoBehaviour that lives on a prefab in the scene. It is the hub that everything connects to. Instead of registering DialogueManager with ServiceLocator, register a wrapper that delegates to DialogueRunner:

// IDialogueService.cs - new interface (same surface as today)

public interface IDialogueService {

void StartDialogue(string nodeName);

void Stop();

bool IsRunning { get; }

event Action OnDialogueStarted;

event Action OnDialogueCompleted;

}

// YarnDialogueService.cs - wraps DialogueRunner

public class YarnDialogueService : MonoBehaviour, IDialogueService {

\[SerializeField\] private DialogueRunner \_runner;

public bool IsRunning => \_runner.IsDialogueRunning;

public event Action OnDialogueStarted;

public event Action OnDialogueCompleted;

private void Awake() {

\_runner.onDialogueStart.AddListener(() => OnDialogueStarted?.Invoke());

\_runner.onDialogueComplete.AddListener(() => OnDialogueCompleted?.Invoke());

}

public void StartDialogue(string nodeName) => \_runner.StartDialogue(nodeName);

public void Stop() => \_runner.Stop();

}

Register in CoreServicesInstaller:

locator.Register&lt;IDialogueService&gt;(yarnDialogueService);

## **4.3 Refactor DialogueBox into a DialoguePresenter**

DialogueBox currently implements IDialogueView. In Yarn Spinner 3, the equivalent is DialoguePresenterBase. The API changes are:

| **Old IDialogueView method** | New DialoguePresenterBase equivalent |
| --- | --- |

| UniTask Show(DialogueLine initialLine) | PrepareForLines(IEnumerable&lt;string&gt; lineIDs, CancellationToken) - optional pre-warm |
| --- | --- |
| UniTask ShowLine(DialogueLine line) | RunLineAsync(LocalizedLine line, LineCancellationToken token) |
| UniTask&lt;int&gt; ShowChoices(List&lt;DialogueChoice&gt;) | RunOptionsAsync(DialogueOption\[\] opts, CancellationToken) |
| void SkipTyping() | Request via token.IsNextLineRequested (poll in typing loop) |
| void Hide() | OnDialogueCompleteAsync() - called by runner when done |

The key change in RunLineAsync is how skip/advance is handled. Instead of subscribing and unsubscribing InputReader delegates per line (which caused the closure allocation bug), the presenter polls the cancellation token in its typing loop:

public override async UniTask RunLineAsync(

LocalizedLine line, LineCancellationToken token) {

UpdateSpeakerVisuals(line); // resolve portrait, name from metadata

string text = line.TextWithoutCharacterName.Text;

// Set full text once - no Substring allocation

\_dialogueLabel.text = text;

// Reveal by adjusting visible character range via rich text

for (int i = 0; i <= text.Length; i++) {

if (token.IsNextLineRequested) break; // skip: no extra subscriptions

SetVisibleCharacters(i);

await UniTask.Delay(\_typingDelayMs, cancellationToken: token.HurryUpToken);

}

SetVisibleCharacters(text.Length); // ensure full text shown

// Wait for advance - token does this natively

await token.UntilNextLineAsync();

}

The LineCancellationToken replaces the entire WaitForAdvance method, the \_advanceSignal UniTaskCompletionSource, and both InputReader subscription/unsubscription blocks - all the input management complexity in the current system collapses to one await.

## **4.4 Bridge InputReader to Yarn Spinner's LineAdvancer**

Yarn Spinner ships a LineAdvancer component that calls DialogueRunner to advance or hurry lines. By default it uses Unity's legacy Input.GetKeyDown or Input Actions directly. Since Core uses InputReader, the cleanest bridge is a thin adapter MonoBehaviour:

public class CoreLineAdvancer : MonoBehaviour {

\[SerializeField\] private DialogueRunner \_runner;

\[Inject\] private InputReader \_inputReader;

private void OnEnable() {

\_inputReader.SubscribePerformed("Interact", OnInteract);

\_inputReader.SubscribePerformed("Skip", OnInteract);

}

private void OnDisable() {

\_inputReader.UnsubscribePerformed("Interact", OnInteract);

\_inputReader.UnsubscribePerformed("Skip", OnInteract);

}

private void OnInteract(InputAction.CallbackContext \_) {

if (\_runner.IsDialogueRunning)

\_runner.RequestNextLine(); // Yarn 3 API: hurry/advance

}

}

This replaces the per-line lambda closure allocations in ProcessDialogueLine and the WaitForAdvance method entirely. One persistent subscription, no per-line allocation.

## **4.5 Build a CoreLineProvider to Bridge LocalizationService**

Yarn Spinner resolves line IDs to text through a LineProvider. The built-in TextLineProvider uses Unity's string tables. Since Core has its own LocalizationService, implement a custom provider:

public class CoreLineProvider : LineProviderBehaviour {

\[Inject\] private LocalizationService \_localization;

public override bool LinesAvailable => true;

public override LocalizedLine GetLocalizedLine(Yarn.Line line) {

// line.ID is the stable #line: tag from the .yarn file

string text = \_localization.GetString(line.ID, fallback: line.ID);

return new LocalizedLine {

TextID = line.ID,

RawText = text,

Substitutions = line.Substitutions,

};

}

}

The localization CSV files exported by Core's existing CSVLocalizationImporter can be mapped to Yarn's string table format - the columns are compatible (id, text, file, node). The Editor/Systems/Localization/CSVLocalizationImporter.cs may need a small update to emit #line: tag IDs, which Yarn generates automatically when you tag lines.

_Yarn Spinner 3 also ships a Unity Localization Line Provider that integrates with Unity's official Localization package. If Core ever migrates its LocalizationService to the Unity Localization package, this becomes the simplest path - just assign the Unity Localised Line Provider and the system works without a custom bridge._

## **4.6 Build a CoreVariableStorage to Bridge SaveService**

Yarn narrative variables (\$foundTheKey, \$hasMetAmira, \$clueCount) need to persist across sessions. Implement VariableStorageBehaviour backed by Core's ISaveService:

public class CoreVariableStorage : VariableStorageBehaviour {

\[Inject\] private ISaveService \_saveService;

private ProgressData \_data; // Core's save data container

private void Start() {

\_data = \_saveService.GetProgressData();

}

public override bool TryGetValue&lt;T&gt;(string variableName, out T result) {

// Read from \_data.NarrativeVariables dictionary

if (\_data.NarrativeVariables.TryGetValue(variableName, out var raw)) {

result = (T)Convert.ChangeType(raw, typeof(T));

return true;

}

result = default; return false;

}

public override void SetValue(string variableName, string value) =>

\_data.NarrativeVariables\[variableName\] = value;

public override void SetValue(string variableName, float value) =>

\_data.NarrativeVariables\[variableName\] = value;

public override void SetValue(string variableName, bool value) =>

\_data.NarrativeVariables\[variableName\] = value;

public override void Clear() =>

\_data.NarrativeVariables.Clear();

}

ProgressData will need a NarrativeVariables dictionary added to it (Dictionary&lt;string, object&gt; or separate typed dictionaries). The SaveService already handles serialization - this bridge is the only new code required to make all Yarn variables persist automatically.

## **4.7 Replace DialogueCutsceneData and EcCutsceneManager Calls with YarnCommands**

The NullReferenceException bug and the direct coupling to Easy Cutscene both go away when scene direction moves to Yarn commands. Wrap the Easy Cutscene API in a component with \[YarnCommand\] attributes:

public class CutsceneCommandHandler : MonoBehaviour {

\[SerializeField\] private EcCutsceneManager \_manager;

// Called from Yarn: &lt;<expression amira worried&gt;>

\[YarnCommand("expression")\]

public void SetExpression(string characterName, string expression) {

var character = \_manager.getCharacterObject(characterName);

if (character != null) character.ChangeSpriteByName(expression);

}

// Called from Yarn: &lt;<move amira stage_left&gt;>

\[YarnCommand("move")\]

public IEnumerator MoveCharacter(string characterName, string transformId) {

var character = \_manager.getCharacterObject(characterName);

var t = \_manager.getCharaTransformSetting(transformId);

if (character == null || t == null) yield break;

character.SetCharacterMove(t.position, t.rotation, t.scale);

yield return new WaitForSeconds(0.3f); // await movement

}

// Called from Yarn: &lt;<play_audio footsteps&gt;>

\[YarnCommand("play_audio")\]

public void PlayAudio(string soundDataKey) {

// Bridge to AudioService

\_audioService.CreateSound().WithKey(soundDataKey).Play();

}

}

In .yarn scripts this looks like:

&lt;<expression amira worried&gt;>

Amira: I don't like this at all.

&lt;<move amira stage_left&gt;>

&lt;<expression hassan neutral&gt;>

Hassan: We don't have a choice.

This is a significant improvement in authoring experience. Writers control character staging directly in the dialogue script without needing to touch the Inspector or Unity scenes. The runtime null-check bug is gone because there are no data fields - commands either succeed or log an error.

## **4.8 Replace DialogueSequenceIdDrawer with Yarn Node Picker**

The current editor drawer calls AssetDatabase.FindAssets on every Inspector repaint frame (a performance bug documented in the audit). Yarn Spinner ships a \[YarnNode\] attribute and a corresponding property drawer that presents a dropdown of all node names from a designated YarnProject. Since the YarnProject is already compiled and cached by the editor, this lookup is O(1) - no database scan per frame.

DialogueTrigger becomes:

\[YarnNode(nameof(\_yarnProject))\]

\[SerializeField\] private string \_startNode;

\[SerializeField\] private YarnProject \_yarnProject;

The \[YarnNode\] attribute's drawer reads the already-compiled node list from the project asset without touching AssetDatabase.FindAssets.

## **4.9 Handle Character Portraits via Line Metadata**

Yarn Spinner does not have a native 'character portrait' concept - that is a game-specific concern. The recommended approach using Yarn's built-in systems is line metadata tags, which are visible to the Presenter via LocalizedLine.Metadata:

title: Sarcophagus_Examine

\---

Amira: This seal is ancient. #portrait:amira_worried #side:left

Hassan: We should be careful. #portrait:hassan_neutral #side:right

\===

In the Presenter's RunLineAsync:

private void UpdateSpeakerVisuals(LocalizedLine line) {

string portraitKey = line.GetMetadataValue("portrait"); // e.g. "amira_worried"

string side = line.GetMetadataValue("side"); // e.g. "left"

// Look up CharacterData by name, resolve portrait by emotion key

var (characterData, sprite) = \_characterRegistry.Resolve(portraitKey);

SetPortrait(side, characterData.CharacterName, sprite);

}

Alternatively, Yarn's markup system can express character data inline - the parser automatically extracts the character name from lines formatted as 'Character: text' and exposes it via LocalizedLine.CharacterName. This pairs with a CharacterRegistry ScriptableObject that maps names to CharacterData assets, removing the per-field CharacterData reference from each line entirely.

_Yarn Spinner 3 also ships an Assets and Localization system for associating audio clips and other Unity assets with specific line IDs. This is an alternative to the #portrait metadata approach and works well when voice acting is managed through the Yarn project rather than C# code._

# **5\. What Is Kept Unchanged**

Yarn Spinner replaces the dialogue data model and runtime, but Core's surrounding systems are unaffected:

- AudioService, SoundBuilder, SoundEmitter - used by \[YarnCommand\] methods, no change to AudioService itself.
- InputReader - wrapped by CoreLineAdvancer. The InputReader ScriptableObject and all its subscriptions remain identical.
- NavigationService and OverlayCanvas - DialogueBox still uses NavigationService.ShowOverlayAsync to open/close. The NavigationService is unaware of Yarn Spinner.
- LocalizationService - wrapped by CoreLineProvider. The service itself does not change.
- SaveService - wrapped by CoreVariableStorage. The SaveService and ProgressData need a NarrativeVariables dictionary added, but nothing else changes.
- EventBus - wire onDialogueStart and onDialogueComplete Unity Events from DialogueRunner to EventBus.Publish calls in YarnDialogueService.Awake.
- CharacterData ScriptableObjects - still used by the Presenter for portrait resolution. The movement/physics data separation recommended in the audit can be done independently.
- Easy Cutscene plugin - still used, but now called from \[YarnCommand\] methods rather than from DialogueManager internals. The coupling is contained.

# **6\. What Is Deleted**

- DialogueManager.cs - replaced by DialogueRunner (Yarn) + YarnDialogueService (thin wrapper).
- DialogueSequence.cs - replaced by .yarn node files.
- DialogueDatabase.cs - replaced by YarnProject asset.
- DialogueLine.cs - replaced by Yarn runtime's LocalizedLine.
- DialogueChoice.cs - replaced by Yarn runtime's DialogueOption.
- DialogueCutsceneData.cs - replaced by \[YarnCommand\] methods.
- CharacterUpdate.cs, PropUpdate.cs - replaced by \[YarnCommand\] parameters.
- IDialogueView.cs - replaced by Yarn's DialoguePresenterBase.
- DialogueSequenceIdAttribute.cs and DialogueSequenceIdDrawer.cs - replaced by \[YarnNode\] attribute.
- DialogueTrigger.cs - simplifies to ~5 lines calling runner.StartDialogue(nodeName).
- DialogueExample.cs - replaced by Yarn's own samples.

# **7\. Migration Scope Estimate**

The following is a rough breakdown of new code to write and existing code to delete or simplify, based on the files read in the audit.

| **Task** | **Effort** | **Risk** | **Notes** |
| --- | --- | --- | --- |
| Install Yarn Spinner, create YarnProject | 0.5 days | Low | Package manager + asset creation |
| Write CoreLineProvider (LocalizationService bridge) | 1 day | Low | ~60 lines, well-defined interface |
| Write CoreVariableStorage (SaveService bridge) | 1 day | Low | ~80 lines, well-defined interface |
| Write CoreLineAdvancer (InputReader bridge) | 0.5 days | Low | ~30 lines, thin adapter |
| Write YarnDialogueService (DialogueRunner wrapper) | 0.5 days | Low | ~50 lines, registers as IDialogueService |
| Refactor DialogueBox → DialoguePresenter | 3 days | Medium | API change; chance to fix all audit issues |
| Write CutsceneCommandHandler (\[YarnCommand\]s) | 2 days | Medium | Maps EcCutsceneManager API to Yarn commands |
| Add NarrativeVariables to ProgressData + SaveService | 0.5 days | Low | Dictionary field + serialization |
| Simplify DialogueTrigger | 0.5 days | Low | 5 lines replacing 90 |
| Port existing dialogue content to .yarn format | Variable | Medium | One .yarn file per sequence; tooling helps |
| QA and integration testing | 3 days | Medium | Test save/load, language switch, branching |

Total code engineering estimate: 9-12 days, excluding content porting. Content porting (converting existing dialogue sequences to .yarn format) depends entirely on how much dialogue already exists. The Yarn Spinner editor and VS Code extension significantly accelerate this with syntax highlighting, node graph preview, and error checking.

# **8\. One Decision to Make Before Starting**

Yarn Spinner for Unity 3.x is paid software when acquired through the Unity Asset Store (\$50 USD one-time). It is free when installed via the open-source package registry (npm/UPM). The GitHub repository and package registry versions are functionally identical - the Asset Store version adds convenience. The team should confirm which acquisition path is appropriate before beginning integration, as the package registry URL differs from the Asset Store package ID.

The open-source repository is: github.com/YarnSpinnerTool/YarnSpinner-Unity. The UPM registry is at pkg.yarnspinner.dev.

_End of document. All Yarn Spinner API references are based on Yarn Spinner 3.1 documentation (docs.yarnspinner.dev) as of March 2026._