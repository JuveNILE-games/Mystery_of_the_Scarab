using Core.Systems.Navigation;
using Core.Systems.Services.Interfaces;
using Core.Utility.Attributes;
using Cysharp.Threading.Tasks;
using Game.Player;
using NetCore.Abstractions;
using UnityEngine;

namespace Game.Systems.LevelSystem.Runtime
{
    // Realizes SCARAB-009's non-Euclidean doorway mechanism (SCARAB-012): added only onto
    // doors LevelBaker.BakeConnections has curated as Felt/Hidden, never onto ordinary
    // Euclidean doors (see DoorwayMarker, which stays untouched/minimal). Kind/PairedTrigger
    // live here rather than on DoorwayMarker precisely because this component is the
    // sometimes-present half — DoorwayMarker is present on every door in the game.
    [RequireComponent(typeof(DoorwayMarker), typeof(BoxCollider))]
    public class DoorwayTeleportTrigger : MonoBehaviour
    {
        private const float CooldownSeconds = 0.5f;

        public ConnectionKind Kind;

        // Direct serialized reference, wired once by LevelBaker.BakeConnections while it
        // still holds both live objects in the same Editor session — deliberately not a
        // string DoorwayId + runtime registry lookup, which would need static
        // OnEnable/OnDisable bookkeeping and risks stale/duplicate-id entries for no benefit
        // here (both ends are always known at bake time).
        public DoorwayTeleportTrigger PairedTrigger;

        // Assigned externally by LevelBaker.BakeConnections when the trigger is added,
        // same as Kind/PairedTrigger above — hence public rather than [SerializeField]
        // private like a hand-authored Inspector field.
        public NonEuclideanTransitionDefaults TransitionDefaults;

        [Inject] private ITransitionService _transitionService;

        private float _cooldownUntil;

        // Confirmed live, 2026-09-05: Unity's untouched default 1x1x1 box only covered the
        // middle third of a 3-cell-wide widened doorway (Door.prefab's true module width,
        // per LayoutConnection's widening comment) — a player crossing through either outer
        // third missed the trigger entirely. Fixed by deriving the box from this door
        // instance's own mesh bounds, matching the widened multi-cell case AND the
        // single-cell fallback (LayoutConnection.DoorCells falls back to one cell when the
        // border is too short to widen) without hardcoding a cell count here.
        private void Reset()
        {
            var col = GetComponent<BoxCollider>();
            col.isTrigger = true;

            var renderers = GetComponentsInChildren<MeshRenderer>();
            if (renderers.Length == 0) return;

            var worldBounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                worldBounds.Encapsulate(renderers[i].bounds);

            // Transform each of the 8 world-space corners into this transform's local space
            // individually (handles the door's yaw rotation correctly) rather than naively
            // dividing by lossyScale, which would be wrong under rotation.
            Vector3? localMin = null, localMax = null;
            for (int x = 0; x < 2; x++)
            for (int y = 0; y < 2; y++)
            for (int z = 0; z < 2; z++)
            {
                var corner = new Vector3(
                    x == 0 ? worldBounds.min.x : worldBounds.max.x,
                    y == 0 ? worldBounds.min.y : worldBounds.max.y,
                    z == 0 ? worldBounds.min.z : worldBounds.max.z);
                var local = transform.InverseTransformPoint(corner);
                localMin = localMin.HasValue ? Vector3.Min(localMin.Value, local) : local;
                localMax = localMax.HasValue ? Vector3.Max(localMax.Value, local) : local;
            }

            col.center = (localMin.Value + localMax.Value) * 0.5f;
            col.size = localMax.Value - localMin.Value;
        }

        private void OnTriggerEnter(Collider other)
        {
            var verdict = Evaluate(other, Time.time);
            if (!verdict.ShouldTeleport) return;

            // Guard both ends — the player lands standing inside the destination
            // trigger's own volume, which would otherwise immediately re-fire it.
            BeginCooldown(Time.time);
            verdict.PairedTrigger.BeginCooldown(Time.time);

            if (_transitionService == null)
            {
                Debug.LogWarning($"[{nameof(DoorwayTeleportTrigger)}] ITransitionService not yet injected on " +
                                  $"{gameObject.name} — skipping teleport.", this);
                return;
            }

            var transitionDef = Kind == ConnectionKind.NonEuclideanFelt
                ? TransitionDefaults.FeltTransition
                : TransitionDefaults.HiddenTransition;

            _transitionService.PlayTransitionAsync(transitionDef, onMidpoint: () =>
            {
                // Mirrors PlayerNetworkStateBridge.Apply()'s existing pattern: the
                // CharacterController otherwise silently overrides a direct transform write.
                verdict.StateMachine.SetPhysicsEnabled(false);
                other.transform.SetPositionAndRotation(
                    verdict.PairedTrigger.transform.position, verdict.PairedTrigger.transform.rotation);
                verdict.StateMachine.SetPhysicsEnabled(true);
            }).Forget();
        }

        // Pure decision logic — no Unity lifecycle, no async, no side effects. Exercised
        // directly by an EditMode test with a fake Collider/PlayerStateMachine/ownership
        // gate; OnTriggerEnter above is a thin, deliberately untested adapter around it.
        // Public (not internal) so the test, in a separate assembly, can call it directly.
        public TeleportVerdict Evaluate(Collider other, float now)
        {
            if (now < _cooldownUntil) return TeleportVerdict.No;
            if (PairedTrigger == null) return TeleportVerdict.No; // e.g. mid-rebake, defensive

            // Sanctioned ownership-check pattern (see NetCore's AGENTS.md) — never read
            // NetworkIdentity.isOwner directly outside PurrNetStateSyncAdapterBase itself.
            var ownershipGate = other.GetComponentInParent<INetworkOwnershipGate>();
            if (ownershipGate != null && !ownershipGate.CanAcceptLocalControl) return TeleportVerdict.No;

            var stateMachine = other.GetComponentInParent<PlayerStateMachine>();
            if (stateMachine == null) return TeleportVerdict.No;

            return new TeleportVerdict(true, stateMachine, PairedTrigger);
        }

        // A named, meaningful operation in its own right (start this doorway's re-entrancy
        // guard) — not a test-only hook — which also happens to make the cooldown behavior
        // directly testable via Evaluate, without reaching into private state.
        public void BeginCooldown(float now) => _cooldownUntil = now + CooldownSeconds;
    }

    public readonly struct TeleportVerdict
    {
        public static readonly TeleportVerdict No = default;

        public readonly bool ShouldTeleport;
        public readonly PlayerStateMachine StateMachine;
        public readonly DoorwayTeleportTrigger PairedTrigger;

        public TeleportVerdict(bool shouldTeleport, PlayerStateMachine stateMachine, DoorwayTeleportTrigger pairedTrigger)
        {
            ShouldTeleport = shouldTeleport;
            StateMachine = stateMachine;
            PairedTrigger = pairedTrigger;
        }
    }
}
