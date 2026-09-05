using System.Collections.Generic;
using Game.Systems.LevelSystem.Runtime;
using UnityEngine;

namespace Game.Systems.LevelGeneration.Editor
{
    // A Layout's connection edge. DoorwayId is generated here so it can be threaded
    // unchanged through Room Shell assembly's DoorwayMarker (SCARAB-006) and later
    // into a baked RoomConnection.doorwayId (SCARAB-011) without ever diverging.
    public class LayoutConnection
    {
        public string FromRoomId;
        public string ToRoomId;
        public Vector2Int DoorCell;

        // The doorway cells, widened along the shared border to fit Door.prefab's
        // true 3-cell-wide module (SCARAB-010 follow-up) where the border is long
        // enough — always includes DoorCell; falls back to just DoorCell (a
        // single-cell door) when the border is too short to widen.
        public List<Vector2Int> DoorCells = new();
        public string DoorwayId;
        public ConnectionKind Kind = ConnectionKind.Euclidean;
        public bool IsLoopEdge;
    }
}
