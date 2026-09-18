using System.Collections.Generic;
using System.Linq;
using Game.Systems.LevelGeneration.Editor;
using Game.Systems.LevelSystem.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    // Regression/behavior coverage for LevelBaker.EnsureDefaultPocketCuration
    // (SCARAB-014 follow-up, 2026-09-06): a generated Isolated Pocket must be
    // reachable out of the box, not silently unreachable until a curator
    // happens to hand-author a pairing for it. Additive only — never touches a
    // pairing the curator already made by hand, never reuses an ordinary door
    // across two default pairings. Pure data (Layout + LevelConnectionCuration),
    // no bake/scene needed — the correct seam for this logic.
    public class LevelBakerDefaultPocketCurationTests
    {
        [Test]
        public void UncoveredPocket_GetsAPairingToAnOrdinaryDoor()
        {
            var layout = new Layout
            {
                Connections = new List<LayoutConnection>
                {
                    Ordinary("Doorway_000"),
                    Ordinary("Doorway_001"),
                },
                Pockets = new List<PocketInfo>
                {
                    new() { PocketId = "Pocket_000", EntranceDoorwayId = "Doorway_010", ExitDoorwayId = "Doorway_010" }
                }
            };
            var curation = ScriptableObject.CreateInstance<LevelConnectionCuration>();

            LevelBaker.EnsureDefaultPocketCuration(layout, curation);

            var pairing = curation.Pairings.Find(p => p.DoorwayIdA == "Doorway_010" || p.DoorwayIdB == "Doorway_010");
            Assert.NotNull(pairing, "Pocket_000's Entrance should have gained a default pairing.");
            var mainDoorId = pairing.DoorwayIdA == "Doorway_010" ? pairing.DoorwayIdB : pairing.DoorwayIdA;
            Assert.Contains(mainDoorId, new[] { "Doorway_000", "Doorway_001" },
                "The other side of the pairing should be one of the ordinary main-graph doors.");
            Assert.AreEqual(ConnectionKind.NonEuclideanHidden, pairing.Kind,
                "A default pairing should stay unobtrusive (Hidden) unless a curator later changes it.");
        }

        [Test]
        public void AlreadyCuratedPocket_GetsNoSecondPairing()
        {
            var layout = new Layout
            {
                Connections = new List<LayoutConnection> { Ordinary("Doorway_000"), Ordinary("Doorway_001") },
                Pockets = new List<PocketInfo>
                {
                    new() { PocketId = "Pocket_000", EntranceDoorwayId = "Doorway_010", ExitDoorwayId = "Doorway_010" }
                }
            };
            var curation = ScriptableObject.CreateInstance<LevelConnectionCuration>();
            curation.Pairings.Add(new CuratedConnectionPairing
                { DoorwayIdA = "Doorway_000", DoorwayIdB = "Doorway_010", Kind = ConnectionKind.NonEuclideanFelt });

            LevelBaker.EnsureDefaultPocketCuration(layout, curation);

            Assert.AreEqual(1, curation.Pairings.Count,
                "A pocket the curator already paired by hand must not gain a second, redundant pairing.");
            Assert.AreEqual(ConnectionKind.NonEuclideanFelt, curation.Pairings[0].Kind,
                "The curator's own choice of Kind must survive untouched.");
        }

        [Test]
        public void TwoUncoveredPockets_NeverShareTheSameMainGraphDoor()
        {
            var layout = new Layout
            {
                Connections = new List<LayoutConnection> { Ordinary("Doorway_000"), Ordinary("Doorway_001") },
                Pockets = new List<PocketInfo>
                {
                    new() { PocketId = "Pocket_000", EntranceDoorwayId = "Doorway_010", ExitDoorwayId = "Doorway_010" },
                    new() { PocketId = "Pocket_001", EntranceDoorwayId = "Doorway_020", ExitDoorwayId = "Doorway_021" }
                }
            };
            var curation = ScriptableObject.CreateInstance<LevelConnectionCuration>();

            LevelBaker.EnsureDefaultPocketCuration(layout, curation);

            Assert.AreEqual(2, curation.Pairings.Count, "Both uncovered pockets should gain a default pairing.");
            var mainDoorsUsed = curation.Pairings
                .SelectMany(p => new[] { p.DoorwayIdA, p.DoorwayIdB })
                .Where(id => id == "Doorway_000" || id == "Doorway_001")
                .ToList();
            Assert.AreEqual(2, mainDoorsUsed.Distinct().Count(),
                "The two pockets must be paired to two different ordinary doors, never the same one twice.");
        }

        [Test]
        public void PocketWithNoPlacedDoor_IsSkippedWithoutThrowing()
        {
            var layout = new Layout
            {
                Connections = new List<LayoutConnection> { Ordinary("Doorway_000") },
                Pockets = new List<PocketInfo> { new() { PocketId = "Pocket_000", EntranceDoorwayId = null, ExitDoorwayId = null } }
            };
            var curation = ScriptableObject.CreateInstance<LevelConnectionCuration>();

            Assert.DoesNotThrow(() => LevelBaker.EnsureDefaultPocketCuration(layout, curation));
            Assert.IsEmpty(curation.Pairings, "A pocket with no placed door has nothing to pair — skip, don't crash.");
        }

        private static LayoutConnection Ordinary(string doorwayId) => new()
        {
            FromRoomId = "Room_000", ToRoomId = "Room_001", DoorwayId = doorwayId,
            DoorCells = new List<Vector2Int> { Vector2Int.zero }, IsSpareDoor = false
        };
    }
}
