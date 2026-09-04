using System.Collections.Generic;
using UnityEngine;

namespace Game.Systems.LevelGeneration.Editor
{
    // A Layout's room node — an abstract graph entry (shape/size/role), transient
    // until baked (see CONTEXT.md's Layout entry). Not yet a RoomDefinition asset.
    public class LayoutRoomNode
    {
        public string RoomId;
        public RoomRole Role = RoomRole.Normal;
        public bool IsEntry;
        public bool IsExit;
        public List<Vector2Int> FootprintCells = new();
    }
}
