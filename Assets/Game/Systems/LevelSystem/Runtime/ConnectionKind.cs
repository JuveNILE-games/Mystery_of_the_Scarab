namespace Game.Systems.LevelSystem.Runtime
{
    // Mirrors CONTEXT.md's Euclidean / Felt Non-Euclidean / Hidden Non-Euclidean
    // Connection vocabulary (SCARAB-001). The base topology algorithm only ever
    // produces Euclidean connections — promoting one to Felt or Hidden is a
    // curation-time decision made afterward, not something this algorithm does.
    // Lives in the runtime assembly (not LevelGeneration.Editor, which is
    // Editor-only) because DoorwayTeleportTrigger needs it at Player runtime
    // (SCARAB-012).
    public enum ConnectionKind
    {
        Euclidean,
        NonEuclideanFelt,
        NonEuclideanHidden
    }
}
