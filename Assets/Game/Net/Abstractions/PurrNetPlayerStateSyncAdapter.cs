using NetCore.Abstractions;
using NetCore.Adapters.PurrNet;
using Game.Net.Abstractions;
using PurrNet;
using UnityEngine;

namespace Game.Net.Adapters
{
    [DisallowMultipleComponent]
    public class PurrNetPlayerStateSyncAdapter : PurrNetStateSyncAdapterBase
    {
        // ownerAuth: true — the owning client captures and broadcasts state, matching this
        // project's deliberate client-authoritative co-op design (see root AGENTS.md's
        // Multiplayer section). Replaces the hand-rolled [ObserversRpc] pipeline: SyncVar's
        // own .value setter does dirty-checking via PurrEquality<T>.Default.Equals, which
        // respects PlayerNetworkState's IEquatable<T> implementation (added for the interim
        // Phase 0 guard, reused here rather than discarded) — plus rate-limited unreliable
        // sends with a final reliable flush, and late-joiner catch-up (OnObserverAdded), none
        // of which the old hand-rolled RPC had.
        [SerializeField] private SyncVar<PlayerNetworkState> _networkState = new(ownerAuth: true);

        private INetworkStateSource<PlayerNetworkState> _source;
        private INetworkStateSink<PlayerNetworkState>   _sink;

        // ── PurrNetStateSyncAdapterBase ──────────────────────────────────────

        protected override void OnBindComponents()
        {
            _source = _sourceComponent as INetworkStateSource<PlayerNetworkState>;
            _sink   = _sinkComponent   as INetworkStateSink<PlayerNetworkState>;

            if (_source == null)
                Debug.LogWarning($"[{nameof(PurrNetPlayerStateSyncAdapter)}] " +
                    $"_sourceComponent on {gameObject.name} does not implement " +
                    $"INetworkStateSource<PlayerNetworkState>.", this);

            if (_sink == null)
                Debug.LogWarning($"[{nameof(PurrNetPlayerStateSyncAdapter)}] " +
                    $"_sinkComponent on {gameObject.name} does not implement " +
                    $"INetworkStateSink<PlayerNetworkState>.", this);

            // Seed the SyncVar with the owner's real starting state before any observer can
            // see this object, so a just-spawned remote player never shows a zeroed-default
            // transform for even one frame.
            if (isOwner && _source != null && _source.TryCapture(out var initial))
                _networkState.value = initial;
        }

        protected override void TryCaptureAndSend()
        {
            if (_source != null && _source.TryCapture(out var state))
                _networkState.value = state;
        }

        protected override void ApplyLatestIfAvailable(NetworkSyncContext ctx)
        {
            _sink?.Apply(_networkState.value, ctx);
        }

        protected override void SetInputEnabled(bool enabled)
        {
            // Reach through to the bridge — safe because _sinkComponent is always
            // expected to be a PlayerNetworkStateBridge on this prefab.
            (_sinkComponent as PlayerNetworkStateBridge)?.SetLocalInputEnabled(enabled);
        }
    }
}
