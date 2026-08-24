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
        // ownerAuth: true matches this project's client-authoritative co-op design (see AGENTS.md).
        [SerializeField] private SyncVar<PlayerNetworkState> _networkState = new(ownerAuth: true);

        // How far behind real time a non-owner renders, so ApplyLatestIfAvailable always has two
        // real received samples to interpolate between instead of chasing/extrapolating the latest
        // one. Slightly larger than one expected send interval to absorb normal jitter.
        [SerializeField] private float _interpolationDelay = 0.05f;

        private INetworkStateSource<PlayerNetworkState> _source;
        private INetworkStateSink<PlayerNetworkState>   _sink;

        private double _prevSampleTime;
        private PlayerNetworkState _prevSample;
        private double _latestSampleTime;
        private PlayerNetworkState _latestSample;

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

            // Seed the interpolation buffer too, so a fresh non-owner interpolates from its real
            // starting pose instead of a zeroed-default prevSample.
            _prevSampleTime = _latestSampleTime = Time.unscaledTimeAsDouble;
            _prevSample = _latestSample = _networkState.value;
            _networkState.onChanged += OnNetworkStateReceived;
        }

        private void OnNetworkStateReceived(PlayerNetworkState newState)
        {
            _prevSampleTime = _latestSampleTime;
            _prevSample     = _latestSample;
            _latestSampleTime = Time.unscaledTimeAsDouble;
            _latestSample     = newState;
        }

        protected override void TryCaptureAndSend()
        {
            if (_source != null && _source.TryCapture(out var state))
                _networkState.value = state;
        }

        protected override void ApplyLatestIfAvailable(NetworkSyncContext ctx)
        {
            if (_sink == null) return;

            double renderTime = Time.unscaledTimeAsDouble - _interpolationDelay;
            var interpolated = PlayerNetworkStateInterpolator.Interpolate(
                _prevSampleTime, _prevSample, _latestSampleTime, _latestSample, renderTime);
            _sink.Apply(interpolated, ctx);
        }

        protected override void SetInputEnabled(bool enabled)
        {
            // Reach through to the bridge — safe because _sinkComponent is always
            // expected to be a PlayerNetworkStateBridge on this prefab.
            (_sinkComponent as PlayerNetworkStateBridge)?.SetLocalInputEnabled(enabled);
        }
    }
}
