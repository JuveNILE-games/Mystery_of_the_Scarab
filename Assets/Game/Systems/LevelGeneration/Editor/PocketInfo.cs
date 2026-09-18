using System.Collections.Generic;

namespace Game.Systems.LevelGeneration.Editor
{
    // An Isolated Pocket (SCARAB-014, see CONTEXT.md): 1-3 mutually-adjacent Rooms
    // deliberately excluded from the Layout's main connectivity graph. Single
    // source of truth for pocket membership/roles — the graphRooms exclusion
    // filter, curator tooling, and reachability validation all derive from this
    // rather than tracking pocket-ness independently on LayoutRoomNode/LayoutConnection.
    public class PocketInfo
    {
        public string PocketId;
        public List<string> RoomIds = new();
        public string EntranceDoorwayId;

        // Equals EntranceDoorwayId for a size-1 pocket (one door serves both roles).
        public string ExitDoorwayId;
    }
}
