using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Systems.LevelSystem.Runtime
{
    // Per-level curation data (SCARAB-012): a curator hand-picks which already-baked,
    // ordinary doors should secretly lead somewhere else instead of the physically-adjacent
    // room they look like they lead to. Plain List<>/enum fields are hand-editable via
    // Unity's default Inspector — no custom EditorWindow UI needed. One asset per generated
    // level (e.g. Generated/Level_{seed}/ConnectionCuration.asset).
    [CreateAssetMenu(fileName = "ConnectionCuration", menuName = "Scarab/Level Connection Curation")]
    public class LevelConnectionCuration : ScriptableObject
    {
        // Checked against the regenerated Layout.Seed before applying — guards against
        // applying the wrong level's curation asset against the wrong bake.
        public int Seed;
        public List<CuratedConnectionPairing> Pairings = new();
    }

    [Serializable]
    public class CuratedConnectionPairing
    {
        public string DoorwayIdA;
        public string DoorwayIdB;
        public ConnectionKind Kind = ConnectionKind.NonEuclideanHidden;
    }
}
