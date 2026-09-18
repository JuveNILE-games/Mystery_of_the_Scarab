using System.Collections.Generic;

namespace Game.Systems.LevelGeneration.Editor
{
    // The generation algorithm's raw output (see CONTEXT.md's Layout entry) —
    // exists only in memory until SCARAB-011's Bake step commits it to real assets.
    public class Layout
    {
        public int Seed;
        public GenerationParameters Parameters;
        public List<LayoutRoomNode> Rooms = new();
        public List<LayoutConnection> Connections = new();
        public List<PocketInfo> Pockets = new();

        public LayoutRoomNode FindRoom(string roomId) => Rooms.Find(r => r.RoomId == roomId);
    }
}
