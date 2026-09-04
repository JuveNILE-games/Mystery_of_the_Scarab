namespace Game.Systems.LevelGeneration.Editor
{
    public enum RoomRole
    {
        Normal,
        Puzzle,
        OpenSpace,
        Corridor,

        // Leftover cross-axis space reclaimed alongside a corridor split
        // (SCARAB-MAP-01 follow-up, 2026-09-03) — structural filler born from
        // carving the corridor, not a room the designer asked for via
        // RoomCount. Kept off RoomRole.Normal specifically so it stays
        // outside AssignPuzzleRooms' eligible pool (Normal-only), same as it
        // was already outside CarveNotches/MergeOpenSpaces by being added to
        // the layout after those passes run.
        Margin
    }
}
