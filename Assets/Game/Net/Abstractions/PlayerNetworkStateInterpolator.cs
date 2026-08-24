using UnityEngine;

namespace Game.Net.Abstractions
{
    /// <summary>
    /// Interpolates between the two most recently received PlayerNetworkState samples instead of
    /// chasing the single latest value with a fixed-alpha lerp. The latter freezes whenever network
    /// updates stop arriving (the target simply stops moving) and then snaps once a new value lands
    /// — visible as "stutter" whenever updates arrive unevenly (jitter, same-machine CPU contention
    /// between the Editor and a standalone client, occasional loss on the Unreliable RPC channel).
    /// Pure math, no NetworkBehaviour/Unity-lifecycle dependency, so it's independently testable.
    /// </summary>
    public static class PlayerNetworkStateInterpolator
    {
        public static PlayerNetworkState Interpolate(
            double prevTime, in PlayerNetworkState prevState,
            double latestTime, in PlayerNetworkState latestState,
            double renderTime)
        {
            // No real span to interpolate across yet (first sample, or two samples landed at the
            // same timestamp) — just show the latest known state rather than divide by zero.
            if (latestTime <= prevTime) return latestState;

            float t = (float)((renderTime - prevTime) / (latestTime - prevTime));
            t = Mathf.Clamp01(t); // never extrapolate before prev or past latest — hold at the ends instead

            // Discrete/boolean fields (MoveInput, JumpPressed, IsGrounded, ...) aren't meaningful to
            // blend — always take them from the latest sample. Only Position/Rotation are continuous.
            PlayerNetworkState result = latestState;
            result.Position = Vector3.Lerp(prevState.Position, latestState.Position, t);
            result.Rotation = Quaternion.Slerp(prevState.Rotation, latestState.Rotation, t);
            return result;
        }
    }
}
