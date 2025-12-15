# Mystery of the Scarab - SaveSystem

Game-specific SaveSystem implementation for Mystery of the Scarab.

## Files

- **MysteryOfTheScarabSaveManager.cs** - Game-specific SaveManager extending the Core SaveService
- **ScarabSavePersister.cs** - Custom persister with game-specific save directory and file extension

## Usage

### Registering in Bootstrapper

Add to `Bootstrapper.cs` in the `RegisterServices()` method:

```csharp
// Mystery of the Scarab SaveSystem
var scarabPersister = new ScarabSavePersister();
var scarabSaveManager = new MysteryOfTheScarabSaveManager(scarabPersister);
advancedLocator.RegisterInstance<ISaveService>(scarabSaveManager);
```

### Loading a Save Slot

```csharp
var saveService = scope.Get<ISaveService>();

// User selects save slot from menu
await saveService.Load("save_slot_1");
```

### Accessing Save Data

```csharp
var persister = saveService.Persister;

// Save progress
persister.SetProgressElement("currentLevel", 5);
persister.SetProgressElement("puzzles", "sphinx_riddle", 1);

// Save preferences
persister.SetPreference("difficulty", "normal");
persister.SetPreference("musicVolume", "0.8");

// Get progress
int? level = persister.GetProgressElement("currentLevel");
var completedPuzzles = persister.GetChildProgressElements("puzzles");

// Trigger save
await saveService.SaveNow();
```

## Save File Location

Save files are stored at:
- **Windows**: `%USERPROFILE%\AppData\LocalLow\<CompanyName>\<ProductName>\MysteryOfTheScarab\`
- **Mac**: `~/Library/Application Support/<CompanyName>/<ProductName>/MysteryOfTheScarab/`
- **Linux**: `~/.config/unity3d/<CompanyName>/<ProductName>/MysteryOfTheScarab/`

Files use the `.scarab` extension.

## TODO

The current implementation is boilerplate. To customize:

1. **Add GameState reference** to MysteryOfTheScarabSaveManager
2. **Implement BeforeLoad()** to clear current game state
3. **Implement CustomLoad()** to apply loaded data to game state
4. **Implement InitializeVarListeners()** to set up auto-save on game events
5. **Add helper methods** for common save operations (e.g., SavePlayerProgress, GetLevelStars)

See the Core SaveSystem README for more details: `Assets/Core/Systems/SaveSystem/README.md`
