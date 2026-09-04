namespace Game.Systems.LevelGeneration.Editor
{
    // Mirrors CONTEXT.md's Euclidean / Felt Non-Euclidean / Hidden Non-Euclidean
    // Connection vocabulary (SCARAB-001). The base topology algorithm only ever
    // produces Euclidean connections — promoting one to Felt or Hidden is a
    // curation-time decision made afterward, not something this algorithm does.
    public enum ConnectionKind
    {
        Euclidean,
        NonEuclideanFelt,
        NonEuclideanHidden
    }
}
