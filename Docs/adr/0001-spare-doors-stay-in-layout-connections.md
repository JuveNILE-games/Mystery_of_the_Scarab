# Spare and Pocket doors stay in `layout.Connections`, not a separate list

SCARAB-014 needs Spare Doors (including Isolated Pocket entrance/exit doors) that have no real destination Room until a curator pairs them via Connection Curation. The obvious alternative was a new parallel `Layout.SpareDoors` list, since `LevelBaker.Bake()` does an unguarded `roomDefsById[connection.ToRoomId]` lookup for every `layout.Connections` entry today and would throw on an unresolved `ToRoomId`.

We kept them as ordinary `LayoutConnection` entries instead, with `ToRoomId` left as an unresolved sentinel until curated: a Spare Door is already half of a Connection to the rest of the Level, so a second parallel connection type for the same concept would be redundant. The actual fix is narrower — make `Bake()`'s existing loop tolerate an unresolved `ToRoomId` (skip that `RoomConnection` entry) instead of throwing.
