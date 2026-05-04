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
        private INetworkStateSource<PlayerNetworkState> _source;
        private INetworkStateSink<PlayerNetworkState>   _sink;
        private PlayerNetworkState _latestRemoteState;
        private bool               _hasRemoteState;

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
        }

        protected override void TryCaptureAndSend()
        {
            if (_source != null && _source.TryCapture(out var state))
                SyncStateRpc(state);
        }

        protected override void ApplyLatestIfAvailable(NetworkSyncContext ctx)
        {
            if (_hasRemoteState && _sink != null)
                _sink.Apply(_latestRemoteState, ctx);
        }

        protected override void SetInputEnabled(bool enabled)
        {
            // Reach through to the bridge — safe because _sinkComponent is always
            // expected to be a PlayerNetworkStateBridge on this prefab.
            (_sinkComponent as PlayerNetworkStateBridge)?.SetLocalInputEnabled(enabled);
        }

        // ── RPC ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Broadcast owner state to all non-owner observers each FixedUpdate.
        /// excludeOwner avoids the owner receiving its own RPC and running the early-return guard.
        /// </summary>
        [ObserversRpc(excludeOwner: true)]
        private void SyncStateRpc(PlayerNetworkState state)
        {
            _latestRemoteState = state;
            _hasRemoteState    = true;
        }
    }
}
