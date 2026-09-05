namespace Game.Systems.LevelGeneration.Editor
{
    // Exposed as a designer-facing toggle (Michael, 2026-09-04: "have this be
    // a setting so we can test both") specifically because different Levels
    // in this project use different CellSize conventions (this session found
    // Level_1399816449 baked at CellSize=1 against a wall/floor kit authored
    // for 2-unit cells) — the two modes respond differently to that.
    public enum FloorUvMode
    {
        // uv = worldX/Z divided by CellSize — ties the texture's tiling
        // density to world-space distance. Two rooms baked at different
        // CellSize values will show different texel density per cell.
        ContinuousWorldSpace,

        // Every cell's quad gets its own literal 0-1 UV range, independent of
        // CellSize or world position — guarantees exactly one texture
        // repetition per grid cell no matter what CellSize a given Level uses.
        PerCell
    }
}
