# Benjamin & Carol: Mystery of the Scarab

A co-op puzzle game where two players take on the roles of treasure hunters Benjamin and Carol as they navigate through ancient Egyptian tombs, using their unique skills to solve puzzles and escape.

## Game Concept

In "Benjamin & Carol: Mystery of the Scarab", players control two characters, each with distinct abilities that must be used in tandem to solve intricate puzzles. The game is set in ancient Egyptian tombs where our heroes are trapped and must use their combined skills to escape.

### Core Characters

- **Benjamin**: A seasoned archaeologist with knowledge of ancient mechanisms and hieroglyphics
- **Carol**: An agile acrobat and expert in pressure-sensitive platforms and tight spaces

## Core Mechanics

### Character Abilities

Each character has unique abilities that are essential for solving puzzles:

#### Benjamin's Abilities
- **Historical Insight**: Can read and interpret hieroglyphics to reveal hidden mechanisms
- **Mechanical Mastery**: Can operate ancient machinery and levers
- **Heavy Lifting**: Can move heavy objects that Carol cannot

#### Carol's Abilities
- **Agility**: Can traverse narrow ledges and jump between platforms
- **Light Weight**: Can step on pressure plates without triggering heavy-object sensors
- **Lockpicking**: Can open locked chests and doors with her tools

### Puzzle Design

Puzzles require both characters to work together, with each player's unique abilities being necessary to progress:
- One player activates a mechanism while the other stands on a pressure plate
- One character reads clues while the other performs physical tasks
- Sequential puzzles where players must coordinate their actions across different areas

## Technical Architecture

### Core Systems

#### 1. Input Management System
The game uses a custom input management system built on Unity's new Input System:

- **[InputManager](file:///D:/Projects/JuveNILE-Games/Mystery_of_the_Scarab/Assets/Core/Systems/InputManagement/InputManager.cs#L8-L86)**: Central input handler that processes player actions
- **[InputReader](file:///D:/Projects/JuveNILE-Games/Mystery_of_the_Scarab/Assets/Core/Systems/InputManagement/InputReader.cs#L11-L120)**: ScriptableObject that manages input actions and subscriptions
- **InputAutoSubscriber**: Automatically subscribes methods to input actions using attributes

#### 2. Scene Management System
Handles loading and unloading of game scenes with support for multiplayer:

- **[SceneLoader](file:///D:/Projects/JuveNILE-Games/Mystery_of_the_Scarab/Assets/Core/Systems/SceneManagement/SceneLoader.cs)**: Manages scene transitions and loading, integrated with TaskRunner for task tracking
- **[NetworkedSceneLoader](file:///D:/Projects/JuveNILE-Games/Mystery_of_the_Scarab/Assets/MultiPlayer/SceneManagement/NetworkedSceneLoader.cs#L13-L109)**: Handles scene loading in multiplayer contexts, also integrated with TaskRunner for task tracking
- **Scene Groups**: Organized collections of scenes that load together

#### 3. Player System
Manages player characters and their interactions:

- **[PlayerController](file:///D:/Projects/JuveNILE-Games/Mystery_of_the_Scarab/Assets/Game/Player/PlayerController.cs#L5-L18)**: Controls player movement and actions
- **[PlayerInputInitializer](file:///D:/Projects/JuveNILE-Games/Mystery_of_the_Scarab/Assets/Game/Player/PlayerInputInitializer.cs#L5-L36)**: Initializes input readers for each player
- **State Management**: Tracks player states and abilities

#### 4. State Machine System
Handles complex character and game states:

- **Scriptable State Machines**: Configurable state machines using ScriptableObjects
- **Actions**: State behaviors that execute when entering, updating, or exiting states
- **Conditions**: Logic for transitioning between states

#### 5. Task Runner System
Manages asynchronous operations with tracking and completion callbacks:

- **[TaskRunner](file:///D:/Projects/JuveNILE-Games/Mystery_of_the_Scarab/Assets/Core/Systems/Runner/TaskRunner.cs#L8-L37)**: Central manager for running and tracking asynchronous tasks
- **[IRunningTask](file:///D:/Projects/JuveNILE-Games/Mystery_of_the_Scarab/Assets/Core/Systems/Runner/IRunningTask.cs#L4-L8)**: Interface for trackable tasks
- **[RunningTask](file:///D:/Projects/JuveNILE-Games/Mystery_of_the_Scarab/Assets/Core/Systems/Runner/RunningTask.cs#L5-L28)**: Generic implementation of a trackable task
- **[ITaskResult](file:///D:/Projects/JuveNILE-Games/Mystery_of_the_Scarab/Assets/Core/Systems/Runner/ITaskResult.cs#L2-L30)**: Interface for task results with success/failure information

The TaskRunner is now integrated with both the [SceneLoader](file:///D:/Projects/JuveNILE-Games/Mystery_of_the_Scarab/Assets/Core/Systems/SceneManagement/SceneLoader.cs) and [NetworkedSceneLoader](file:///D:/Projects/JuveNILE-Games/Mystery_of_the_Scarab/Assets/MultiPlayer/SceneManagement/NetworkedSceneLoader.cs#L13-L109) to track scene loading operations and provide better error handling and monitoring capabilities for both local and networked scene loading.

#### 6. Service Locator System
Provides dependency injection and service management:

- **[ServiceLocator](file:///D:/Projects/JuveNILE-Games/Mystery_of_the_Scarab/Assets/Core/Systems/ServiceLocator/ServiceLocator.cs#L7-L211)**: Central registry for services with global and scene-level scopes
- **[Bootstrapper](file:///D:/Projects/JuveNILE-Games/Mystery_of_the_Scarab/Assets/Core/Systems/ServiceLocator/Bootstrapper.cs#L6-L22)**: Abstract base class for service initialization
- **[ServiceManager](file:///D:/Projects/JuveNILE-Games/Mystery_of_the_Scarab/Assets/Core/Systems/ServiceLocator/ServiceManager.cs#L5-L54)**: Internal service registry implementation
- **ServiceLocatorGlobal/Scene**: Specialized bootstrappers for global and scene-level service containers

### Multiplayer Framework

The game uses PurrNet for networking:

- **Networked Scene Loading**: Synchronized scene transitions across all clients
- **Player Synchronization**: Character positions and states synchronized in real-time
- **Event System**: Networked events for game progression and puzzle solving

## Project Structure

```
Assets/
├── Core/                 # Fundamental systems and utilities
│   ├── Systems/          # Core game systems (Input, Audio, Navigation, Scene Management)
│   └── Utility/          # Helper classes and base components
├── Game/                 # Game-specific content and logic
│   ├── Player/           # Player controllers and related components
│   └── Systems/          # Game-specific systems (State Machines, Abilities)
├── MultiPlayer/          # Networking and multiplayer-specific code
└── Plugins/              # Third-party libraries and tools
```

## Code Quality Assessment

After reviewing the codebase, here are some observations about the current state of the Core and Game scripts:

### Issues Identified

#### 1. Half-Baked Implementations
- **[PlayerController.cs](file:///D:/Projects/JuveNILE-Games/Mystery_of_the_Scarab/Assets/Game/Player/PlayerController.cs#L5-L18)**: Contains minimal implementation with empty Start() and Update() methods. The [ScriptableDictionary](file:///D:/Projects/JuveNILE-Games/Mystery_of_the_Scarab/Assets/Core/Utility/ScriptableDictionary.cs#L6-L10) field is defined but not used.
- **[HoldForAnimationAction.cs](file:///D:/Projects/JuveNILE-Games/Mystery_of_the_Scarab/Assets/Game/Systems/StateMachine/Actions/Scripts/HoldForAnimationAction.cs#L9-L33)**: Has complex nested conditional logic that could be simplified. Directly accesses dictionary values without proper null checking.

#### 2. Code Smells and Potential Issues
- **[InputManager.cs](file:///D:/Projects/JuveNILE-Games/Mystery_of_the_Scarab/Assets/Core/Systems/InputManagement/InputManager.cs#L8-L86)**: All the jump, sprint, and ability methods have identical debug log messages ("Sprint action triggered"), suggesting copy-paste errors.
- **[InputCondition.cs](file:///D:/Projects/JuveNILE-Games/Mystery_of_the_Scarab/Assets/Game/Systems/StateMachine/Conditions/Scripts/InputCondition.cs#L12-L55)**: Contains extensive null checking but then accesses properties without additional safety checks. The logic for checking if a button is pressed seems inefficient.
- **[SoundManager.cs](file:///D:/Projects/JuveNILE-Games/Mystery_of_the_Scarab/Assets/Core/Systems/AudioSystem/SoundManager.cs#L44-L47)**: The CanPlaySound method has a try-catch block that catches all exceptions but only logs a generic error message, which could hide important debugging information.

#### 3. Unnecessary Complexity
- **[InputAutoSubscriber.cs](file:///D:/Projects/JuveNILE-Games/Mystery_of_the_Scarab/Assets/Core/Systems/InputManagement/InputAutoSubscriber.cs#L7-L68)**: Uses reflection to automatically subscribe methods, which adds complexity and could impact performance. This might be over-engineering for most use cases.

#### 4. Awful Code Patterns
- **[SceneLoader.cs](file:///D:/Projects/JuveNILE-Games/Mystery_of_the_Scarab/Assets/Core/Systems/SceneManagement/SceneLoader.cs#L112-L123)**: The OverrideScenGroup method (note the typo) has a logic flaw where it awaits UnloadScenes() but doesn't properly handle potential failures. The method name itself contains a typo.
- **[PlayAnimationAction.cs](file:///D:/Projects/JuveNILE-Games/Mystery_of_the_Scarab/Assets/Game/Systems/StateMachine/Actions/Scripts/PlayAnimationAction.cs#L15-L34)**: Accesses _animation.name without checking if _animation is null first, which could lead to null reference exceptions.

#### 5. Naming Issues
- ~~**ServivceManager.cs**: File name contains a typo ("Servivce" instead of "Service")~~ - **FIXED**: File has been correctly renamed to ServiceManager.cs

### Runner System Assessment

The Runner system provides a framework for managing asynchronous operations with tracking and result handling:

#### Improvements Made

1. **Fixed Race Conditions**: Added proper locking mechanisms when modifying the runningTasks list to prevent race conditions.
2. **Improved Error Handling**: Added exception handling to tasks that converts exceptions to failure results, preventing unhandled exceptions.
3. **Fixed WaitForAllTasksAsync**: Revised the implementation to properly wait for all tasks using `Task.WhenAll` instead of `Task.WhenAny`.
4. **Standardized Namespaces**: Changed namespace from `Core.Scripts.Runner` to `Core.Systems.Runner` for consistency with other core systems.
5. **Added Task Count Tracking**: Added method to get the current number of running tasks.
6. **Fixed Unity Threading Issues**: Added `isBackground: false` parameter to TaskRunner calls in scene loading systems to ensure Unity API calls execute on the main thread.

#### Remaining Recommendations

1. **Add Task Cancellation Support**: Implement cancellation tokens for better control over long-running tasks.
2. **Add Task Prioritization**: Add support for prioritizing tasks in the execution queue.
3. **Add Progress Reporting**: Implement progress reporting for long-running tasks.
4. **Add Task Grouping**: Allow grouping of related tasks for better organization and management.

### Service Locator System Assessment

The Service Locator system provides dependency injection capabilities with global and scene-level scopes:

#### Issues Identified

1. **Performance Issues**:
   - The [ServiceLocator.ForSceneOf](file:///D:/Projects/JuveNILE-Games/Mystery_of_the_Scarab/Assets/Core/Systems/ServiceLocator/ServiceLocator.cs#L47-L65) method performs GameObject lookups every time it's called, which could be expensive
   - The [TryGetService](file:///D:/Projects/JuveNILE-Games/Mystery_of_the_Scarab/Assets/Core/Systems/ServiceLocator/ServiceLocator.cs#L140-L142) method has duplicate implementations

2. **Missing Features**:
   - No support for lazy initialization of services
   - No support for service lifecycle management (initialization/disposal)
   - Some methods return the ServiceLocator instance for chaining, while others don't, leading to API inconsistencies

#### Recommendations

1. ~~**Fix Typo**: Rename ServivceManager.cs to ServiceManager.cs~~ - **COMPLETED**: File has been correctly renamed to ServiceManager.cs
2. **Optimize Lookups**: Cache scene-level service locators to avoid repeated GameObject lookups
3. **Improve API Consistency**: Standardize the API and remove duplicate methods
4. **Add Lifecycle Management**: Implement service initialization and disposal patterns
5. **Add Lazy Initialization**: Support for lazy initialization of services

### Recommendations

1. **Complete Implementations**: Fill out the placeholder methods in PlayerController and other minimal implementations with actual functionality.
2. **Fix Copy-Paste Errors**: Correct the debug log messages in InputManager to accurately reflect the actions being processed.
3. **Improve Error Handling**: Enhance try-catch blocks with more specific exception handling and better error messages.
4. **Simplify Complex Logic**: Refactor the nested conditionals in animation actions to be more readable and maintainable.
5. **Fix Naming Issues**: Correct method names like "OverrideScenGroup" to "OverrideSceneGroup" and fix the "ServivceManager" typo.
6. ~~**Update Runner System Assessment**: Address race conditions and incomplete implementations in TaskRunner.~~ - **COMPLETED**: Fixed race conditions, improved error handling, and standardized namespaces in TaskRunner system.
7. **Update Service Locator Assessment**: Optimize performance and improve API consistency.
8. **Add Proper Null Checking**: Ensure all object references are properly checked before use to prevent runtime exceptions.
9. **Remove Unnecessary Reflection**: Consider if the reflection-based input subscription in InputAutoSubscriber is truly necessary or if a simpler approach would suffice.
10. ~~**Fix Race Conditions**: Add proper synchronization to the TaskRunner system.~~ - **COMPLETED**: Added proper synchronization to the TaskRunner system.
11. **Optimize Service Locator**: Improve performance of service lookups and standardize the API.

## Multiplayer System Assessment

### Current State

The multiplayer system is built on top of PurrNet and primarily focuses on scene management synchronization. The key components are:

1. **[NetworkedSceneLoader.cs](file:///D:/Projects/JuveNILE-Games/Mystery_of_the_Scarab/Assets/MultiPlayer/SceneManagement/NetworkedSceneLoader.cs#L13-L109)**: A NetworkBehaviour component that manages scene loading across the network
2. **[NetworkedSceneGroupManager.cs](file:///D:/Projects/JuveNILE-Games/Mystery_of_the_Scarab/Assets/MultiPlayer/SceneManagement/NetworkedSceneGroupManager.cs#L12-L117)**: Handles the actual networked scene loading operations using PurrNet's ScenesModule

### Integration with Core Systems

The multiplayer system builds upon the core scene management system:

- It reuses the same **SceneGroup** data structures from the core system
- It implements a similar **SceneGroupManager** pattern but with network-aware functionality
- It uses the same **ScriptableEvent** system for communication between components

### Integration with Game Systems

The multiplayer system currently has limited integration with game-specific systems:

- There's no networked player controller implementation
- No synchronization of player states or positions
- No networked puzzle state management
- No multiplayer-specific input handling

### Issues Identified in Multiplayer Code

#### 1. Incomplete Implementation
- **Missing Player Synchronization**: The multiplayer system only handles scene loading but doesn't include player synchronization, which is essential for a co-op game
- **Limited Network Events**: Only basic scene loading events are implemented, missing game-specific network events (puzzle states, player actions, etc.)

#### 2. Integration Gaps
- **No Player Network Components**: There are no networked player controller or input components, which are essential for a co-op game
- **Missing Game State Synchronization**: No mechanisms for synchronizing puzzle states, game progress, or other game-specific data across the network

#### 3. Architecture Concerns
- **Tight Coupling**: The [NetworkedSceneGroupManager](file:///D:/Projects/JuveNILE-Games/Mystery_of_the_Scarab/Assets/MultiPlayer/SceneManagement/NetworkedSceneGroupManager.cs#L12-L117) is tightly coupled to PurrNet's ScenesModule, making it difficult to test or modify
- **Inconsistent API**: The networked version uses PurrNet's scene loading while the core version uses Unity's SceneManager, leading to potential inconsistencies

#### 4. Half-Baked Components
- **Incomplete Network Implementation**: Only scene loading is networked, but no actual gameplay elements (players, puzzles, interactions) are synchronized
- **Missing Error Handling**: Network-specific error conditions (disconnects, timeouts, etc.) are not handled
- **No Fallback Mechanisms**: If network operations fail, there are no retry or recovery mechanisms

### Refactoring Progress

The scene management system has been successfully refactored to eliminate code duplication and improve architecture:

1. **Created Abstract Base Class**: [BaseSceneGroupManager](file:///D:/Projects/JuveNILE-Games/Mystery_of_the_Scarab/Assets/Core/Systems/SceneManagement/BaseSceneGroupManager.cs#L10-L117) contains all shared functionality
2. **Refactored Implementations**: Both [SceneGroupManager](file:///D:/Projects/JuveNILE-Games/Mystery_of_the_Scarab/Assets/Core/Systems/SceneManagement/SceneGroupManager.cs#L8-L34) and [NetworkedSceneGroupManager](file:///D:/Projects/JuveNILE-Games/Mystery_of_the_Scarab/Assets/MultiPlayer/SceneManagement/NetworkedSceneGroupManager.cs#L12-L45) now inherit from the base class
3. **Strategy Pattern**: Created [ISceneOperationStrategy](file:///D:/Projects/JuveNILE-Games/Mystery_of_the_Scarab/Assets/Core/Systems/SceneManagement/ISceneOperationStrategy.cs#L9-L22) interface with [LocalSceneOperationStrategy](file:///D:/Projects/JuveNILE-Games/Mystery_of_the_Scarab/Assets/Core/Systems/SceneManagement/LocalSceneOperationStrategy.cs#L8-L33) and [NetworkedSceneOperationStrategy](file:///D:/Projects/JuveNILE-Games/Mystery_of_the_Scarab/Assets/MultiPlayer/SceneManagement/NetworkedSceneOperationStrategy.cs#L9-L44) implementations
4. **Removed Arbitrary Delays**: Replaced `Task.Delay` calls with proper async progress monitoring
5. **Maintained Compatibility**: All existing APIs remain unchanged to prevent breaking existing code
6. **Fixed Unity Threading Issues**: Added `isBackground: false` parameter to TaskRunner calls in networked scene loading to ensure Unity API calls execute on the main thread.

### Remaining Recommendations for Multiplayer System

1. **Implement Player Synchronization**:
   - Create networked player controller components
   - Add player position and state synchronization
   - Implement networked input handling

2. **Add Game State Synchronization**:
   - Implement networked puzzle state management
   - Add synchronization for game progression and achievements
   - Create network events for game-specific actions

3. **Improve Architecture**:
   - Decouple the scene management logic from specific networking implementations
   - Create abstraction layers for network operations
   - Implement proper dependency injection for easier testing

4. **Enhance Error Handling**:
   - Add proper error handling for network disconnections
   - Implement retry mechanisms for failed scene loads
   - Add better logging for network-related issues
   - Handle timeout conditions gracefully

5. **Complete the Implementation**:
   - Add networked player prefabs and components
   - Implement player spawn/destroy synchronization
   - Add networked game state management
   - Create proper lobby and session management

## Setup Instructions

### Prerequisites
- Unity 2022.3 LTS or later
- Git for version control

### Initial Setup
1. Clone the repository
2. Open the project in Unity
3. Import all required packages through the Package Manager
4. Ensure all scenes are added to the Build Settings

### Development Workflow
1. Create feature branches for new functionality
2. Follow the existing code structure and naming conventions
3. Test changes locally before pushing
4. Update this README as new systems are implemented

## Development Roadmap

### Phase 1: Core Systems
- [ ] Implement character movement and basic interactions
- [ ] Create puzzle mechanics framework
- [ ] Develop scene loading and management system
- [ ] Set up multiplayer networking foundation

### Phase 2: Puzzle Implementation
- [ ] Design and implement first set of cooperative puzzles
- [ ] Create character-specific ability systems
- [ ] Develop puzzle progression tracking

### Phase 3: Content Creation
- [ ] Design Egyptian tomb environments
- [ ] Create character models and animations
- [ ] Implement audio system with atmospheric sounds
- [ ] Add UI for puzzle clues and game progression

### Phase 4: Polish and Release
- [ ] Optimize performance for target platforms
- [ ] Add visual effects and particle systems
- [ ] Implement save system
- [ ] Conduct playtesting and balance adjustments

## Contributing

This project is currently in early development. As we progress, contribution guidelines will be added here.

## License

This project is proprietary and not open source. All rights reserved.