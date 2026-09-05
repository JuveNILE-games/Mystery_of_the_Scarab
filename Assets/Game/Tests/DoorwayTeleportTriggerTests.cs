using System.Collections.Generic;
using Game.Player;
using Game.Systems.LevelSystem.Runtime;
using NetCore.Abstractions;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    // Exercises DoorwayTeleportTrigger.Evaluate directly (SCARAB-012) — the pure decision
    // logic (ownership gate, player check, re-entrancy cooldown, pair lookup) that would
    // otherwise have zero test coverage, since OnTriggerEnter itself needs a running scene
    // + physics and isn't a useful seam.
    public class DoorwayTeleportTriggerTests
    {
        private readonly List<GameObject> _spawned = new();

        [TearDown]
        public void DestroySpawnedObjects()
        {
            foreach (var go in _spawned)
                if (go != null)
                    Object.DestroyImmediate(go);
            _spawned.Clear();
        }

        [Test]
        public void Evaluate_ShouldTeleport_ForPlayerColliderWithNoBlockingGate()
        {
            var (triggerA, _) = CreatePairedTriggers();
            var player = CreatePlayer();

            var verdict = triggerA.Evaluate(player.GetComponent<Collider>(), Time.time);

            Assert.IsTrue(verdict.ShouldTeleport);
            Assert.AreSame(triggerA.PairedTrigger, verdict.PairedTrigger);
            Assert.NotNull(verdict.StateMachine);
        }

        [Test]
        public void Evaluate_False_ForNonPlayerCollider()
        {
            var (triggerA, _) = CreatePairedTriggers();
            var nonPlayer = CreateGameObject("NonPlayer");
            var col = nonPlayer.AddComponent<BoxCollider>();

            var verdict = triggerA.Evaluate(col, Time.time);

            Assert.IsFalse(verdict.ShouldTeleport);
        }

        [Test]
        public void Evaluate_False_WhenOwnershipGateBlocksLocalControl()
        {
            var (triggerA, _) = CreatePairedTriggers();
            var player = CreatePlayer();
            var gate = player.AddComponent<FakeOwnershipGate>();
            gate.CanAcceptLocalControl = false;

            var verdict = triggerA.Evaluate(player.GetComponent<Collider>(), Time.time);

            Assert.IsFalse(verdict.ShouldTeleport);
        }

        [Test]
        public void Evaluate_False_DuringCooldownAfterBeginCooldown()
        {
            var (triggerA, _) = CreatePairedTriggers();
            var player = CreatePlayer();

            triggerA.BeginCooldown(Time.time);
            var verdict = triggerA.Evaluate(player.GetComponent<Collider>(), Time.time);

            Assert.IsFalse(verdict.ShouldTeleport, "Evaluate must refuse to teleport again immediately after BeginCooldown.");
        }

        private (DoorwayTeleportTrigger, DoorwayTeleportTrigger) CreatePairedTriggers()
        {
            var a = CreateGameObject("DoorA").AddComponent<DoorwayTeleportTrigger>();
            var b = CreateGameObject("DoorB").AddComponent<DoorwayTeleportTrigger>();
            a.PairedTrigger = b;
            b.PairedTrigger = a;
            return (a, b);
        }

        private GameObject CreatePlayer()
        {
            var player = CreateGameObject("Player");
            player.AddComponent<PlayerStateMachine>();
            return player;
        }

        private GameObject CreateGameObject(string name)
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            return go;
        }

        private class FakeOwnershipGate : MonoBehaviour, INetworkOwnershipGate
        {
            public bool CanAcceptLocalControl { get; set; } = true;
        }
    }
}
