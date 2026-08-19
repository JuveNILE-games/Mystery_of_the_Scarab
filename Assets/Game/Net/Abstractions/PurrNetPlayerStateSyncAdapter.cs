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
        private PlayerNetworkState _lastSentState;
        private bool               _hasSentState;

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
            if (_source == null || !_source.TryCapture(out var state))
                return;

            // Interim dirty-check: skip the RPC when nothing actually changed since the
            // last broadcast. This is a cheap stopgap for the per-tick full-state judder
            // finding — real delta compression arrives once state moves onto PurrNet's
            // native SyncVar<T>, which replaces this hand-rolled adapter entirely.
            if (_hasSentState && state.Equals(_lastSentState))
                return;

            SyncStateRpc(state);
            _lastSentState = state;
            _hasSentState  = true;
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
