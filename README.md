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

Full technical conventions, architecture patterns, and system-by-system detail live in
[AGENTS.md](AGENTS.md) — that's the maintained entry point for anyone (human or AI) working
on this codebase. In short:

- **Service-oriented core**: a `Locator`-based service container with DI (constructor
  injection for pure C# services, `[Inject]` for MonoBehaviours), booted via `Bootstrapper` →
  `BootPipeline` → ordered `IBootPhase`s.
- **Puzzle System**: `PuzzleDefinition`/`PuzzleComponent`/`LogicGate` tree, resolved and
  evaluated by `PuzzleController`.
- **Companion AI**: Unity Behavior Graph + NavMesh, sharing the same `PlayerStateMachine`
  input path as human players.
- **Dialogue**: Yarn Spinner via Core's `DialogueService`, with multiplayer-aware
  Initiator/Companion role resolution.
- **Multiplayer**: PurrNet, via NetCore's `INetworkService`/`INetworkStateSource<T>`/
  `INetworkStateSink<T>`/`INetworkOwnershipGate` abstractions — see AGENTS.md's Multiplayer
  section for the current API surface.

For a point-in-time audit of the codebase (what's solid, what's stubbed, what's stale),
see `Docs/codebase_audit_2026-07-30.md`.

## Project Structure

```
Assets/
├── Core/                 # git submodule — JuveNILE.Core framework (shared across projects)
├── NetCore/              # git submodule — JuveNILE.NetCore multiplayer layer
├── Game/                 # Mystery of the Scarab game-specific code
│   ├── AI/                # Companion behavior trees, puzzle observer
│   ├── Net/                # Game-specific PurrNet adapters (player state sync, puzzle authority)
│   ├── Player/             # PlayerStateMachine and movement states
│   ├── Story/              # Yarn dialogue files, narrative scripts
│   ├── Systems/            # Game systems (PuzzleSystem, LevelSystem, etc.)
│   └── UI/                 # Game UI views and presenters
├── Plugins/              # Third-party packages (PurrNet, Odin, Soap, etc.)
└── Scenes/
```

## Setup Instructions

### Prerequisites
- Unity 6 (6000.x), IL2CPP, C# 9 baseline
- Git for version control (this repo uses submodules — clone with `--recurse-submodules`)

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