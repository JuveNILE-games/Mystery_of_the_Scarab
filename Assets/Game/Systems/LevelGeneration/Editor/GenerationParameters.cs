using System;

namespace Game.Systems.LevelGeneration.Editor
{
    // Designer-facing generation inputs (SCARAB-004). The seed fills in the rest
    // around these — room count/role mix and connectivity are not left fully
    // emergent.
    [Serializable]
    public class GenerationParameters
    {
        public int Seed;
        public int RoomCount = 8;
        public int PuzzleRoomCount = 2;
        public int OpenSpaceCount = 1;
        public int MinEntryExitDistance = 3;
        public int LoopCount = 2;

        // How many BSP splits reserve a wide, corridor-filled gap instead of the
        // usual bare 1-cell wall gap between two leaves (SCARAB-010 follow-up:
        // corridors were previously only a rare disconnection-fallback, never a
        // normal feature). Best-effort target, not a guarantee — a split whose
        // leaf is too small to fit a corridor-width gap falls back to a plain
        // 1-cell gap instead.
        public int CorridorCount = 2;

        // Corridor length, in cells, chosen uniformly per corridor between these
        // two bounds (SCARAB-010 follow-up: the original 2-4 hardcoded range read
        // as barely more than a wall's thickness next to rooms that are usually
        // MinLeafSize(3)+ per side).
        public int CorridorMinLength = 4;
        public int CorridorMaxLength = 8;

        // How many Normal-role rooms get a rectangular notch carved out of one
        // corner for shape variety, independent of open-space merging (SCARAB-010
        // follow-up). Best-effort target — a room too small to notch and stay
        // above MinLeafSize in both dimensions is skipped.
        public int IrregularRoomCount = 2;

        // BSP tuning, not a generation-parameter surfaced to the designer.
        public int MinLeafSize = 3;

        // Decorative dead-end doors on otherwise-normal, already-reachable rooms
        // (SCARAB-014) — no room behind them at generation time, bare exterior
        // void until a curator pairs one via LevelConnectionCuration. Purely
        // optional extra material for curation, unrelated to any room's own
        // reachability. Default 0 so existing generated Levels are unaffected.
        public int SpareDoorCount = 0;

        // How many Isolated Pockets (SCARAB-014) to carve out of the layout —
        // clusters of 1-3 mutually-adjacent Normal rooms deliberately excluded
        // from the main connectivity graph, reachable only via a curated
        // connection to one of the pocket's own Entrance/Exit doors. Default 0
        // so existing generated Levels are unaffected.
        public int IsolatedPocketCount = 0;
    }
}
