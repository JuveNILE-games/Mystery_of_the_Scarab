# Mystery of the Scarab

Game-level domain glossary for Mystery of the Scarab (the game repo, distinct from the `Core` submodule's own context at `Packages/com.juvenile.games.core/CONTEXT.md`).

## Language

### Level System (existing)

**Level**:
The top-level playable unit (`LevelDefinition`): an id, a scene, a set of `Room`s, an entry/exit room, and narrative hooks. A shipped, playable piece of the game.
_Avoid_: Dungeon, floor (not yet used in code; keep "Level" as the canonical term)

**Room**:
A `RoomDefinition`: a gameplay-facing node with an id, a scene root tag, and a list of `Connection`s to other Rooms. The unit puzzles, doors, and locks attach to.
_Avoid_: Chamber, node (Room is the established code term)

**Connection**:
A `RoomConnection`: a directed link from one Room to another via a doorway id, optionally locked until listed puzzles are solved.
_Avoid_: Edge, door (Connection is the data; the doorway is its physical marker in the scene)

**Doorway Marker**:
A `DoorwayMarker`: the physical marker a Connection's doorway id resolves to in the scene — a `doorwayId` string plus its own Transform as a position/orientation anchor, nothing more. Introduced by SCARAB-006 (no door system existed before it); its shape is still expected to evolve, e.g. once non-Euclidean connection realization (SCARAB-009) has real requirements.
_Avoid_: Door, doorway (those name the visual piece or the concept; Doorway Marker is specifically the anchor component identifying where a Connection's doorway is)

**Puzzle Room**:
A `PuzzleRoomDefinition`: a Room with one or more puzzles and a solve mode (`All` or `Any`) that gates when the room counts as solved.
_Avoid_: Challenge room

### Procedural Generation (new, this map)

**Seed**:
The deterministic input value consumed once, at generation time, to produce exactly one Layout. Not stored on any Room/Level asset after baking — it's an input to the bake, not a runtime concept.
_Avoid_: Random state, RNG (Seed is the input; the algorithm's internal RNG state is an implementation detail)

**Layout**:
The generation algorithm's raw output: an abstract graph of room nodes (shape/size/role) and the connections between them, before any of it is realized as scene geometry or `RoomDefinition` assets. Exists only transiently during a bake.
_Avoid_: Room graph, blueprint (Layout is the algorithm's output stage specifically, upstream of baking)

**Bake**:
The edit-time act of turning one Layout into real project content: scene geometry (Room Shells) plus populated `RoomDefinition`/`LevelDefinition` assets on disk. A baked Level is indistinguishable from a hand-built one to the rest of the game.
_Avoid_: Generate (too broad — generation produces a Layout; baking is the separate step that commits it to assets), build

**Room Shell**:
The generated 3D architecture for one Room — walls, floor, ceiling, doorways — assembled from Room Pieces by the adjacency/rule system, with no gameplay content yet.
_Avoid_: Room geometry, greybox (Shell is specifically the ruled-assembly output, not a placeholder)

**Room Piece**:
One modular kit prefab (e.g. WallCorner, WallStraight, Door, floor slab) that the rule system assembles into a Room Shell. Already exist in the project, placed today via PrefabBrush.
_Avoid_: Tile, module (Piece matches the existing PrefabBrush/kit vocabulary)

**Gameplay Layer**:
Hand-authored content (props, traps, puzzle objects, `Puzzle` wiring) placed on top of a baked Room Shell once its Layout is locked. Fixed relative to that specific bake — re-rolling the seed produces a new Layout, not a re-rolled Gameplay Layer.
_Avoid_: Dressing, set dressing (Gameplay Layer specifically includes functional puzzle/trap objects, not just visual dressing)

**Euclidean Connection**:
The default kind of Connection: its two Rooms reconcile in real, walkable 3D space, with no trick required to build or traverse it. The great majority of Connections in any Layout.
_Avoid_: Normal connection, regular connection (Euclidean names the specific spatial property at stake, established in resolving SCARAB-001)

**Felt Non-Euclidean Connection**:
A Connection deliberately built to not reconcile in real 3D space, where the player is meant to notice the space is spatially "wrong" — the game's one narrative device for the Seth/backrooms unease the tomb's premise calls for. Curated and rare by design: hand-picked per Layout, never something the generation algorithm introduces on its own.
_Avoid_: Impossible connection, glitch connection (Felt specifically means the wrongness is meant to register with the player; see Hidden Non-Euclidean Connection for the opposite intent)

**Hidden Non-Euclidean Connection**:
A Connection that does not reconcile in real 3D space but is built and dressed so the player never perceives anything unusual — purely a generation/baking convenience for joining Room Shells whose geometry wouldn't otherwise line up. Unlike the Felt kind, frequency is unconstrained: the generator may use it wherever convenient.
_Avoid_: Portal, loading trick (those name a possible realization mechanism, not yet decided — see SCARAB-009)

Note: how a Felt or Hidden Non-Euclidean Connection is actually realized as baked scene content (portal volume? camera cut? disguised geometry?) is still open — tracked as SCARAB-009, not decided yet. No implementation vocabulary for that mechanism exists here until it resolves.

## Open naming questions

- Whether a Level can span multiple scenes (loosening `LevelDefinition.levelScene` from one scene to several) is flagged by the user as needing its own grilling ticket — no term coined for a multi-scene Level yet.
