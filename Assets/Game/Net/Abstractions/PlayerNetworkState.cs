using UnityEngine;

namespace Game.Net.Abstractions
{
    /// <summary>
    /// Blittable snapshot of player state broadcast each FixedUpdate by the owner.
    /// Keep fields simple (Vector3 / Quaternion / bool) to ensure PurrNet RPC
    /// serialization remains trivial.
    /// </summary>
    public struct PlayerNetworkState : System.IEquatable<PlayerNetworkState>
    {
        public Vector3    Position;
        public Quaternion Rotation;
        public Vector3    MoveInput;
        public bool       JumpPressed;
        public bool       SprintPressed;
        public bool       PrimaryPressed;
        public bool       SecondaryPressed;

        // Owner's real CheckGrounded() result. Non-owners never run local ground detection
        // (see PlayerStateMachine.SetPhysicsEnabled) — without this, their own state machine's
        // Grounded/Airborne transition (which needs to know "am I grounded") would have nothing
        // to read, and CanStartJump() would never see a valid ground contact for the Jump
        // animation to trigger on the remote copy.
        public bool       IsGrounded;

        /// <summary>
        /// Field-wise equality using Vector3/Quaternion's own epsilon-based ==, so tiny
        /// floating-point noise doesn't count as a change. Used to skip redundant RPC
        /// broadcasts when nothing has actually moved (see PurrNetPlayerStateSyncAdapter).
        /// </summary>
        public bool Equals(PlayerNetworkState other)
        {
            return Position == other.Position
                && Rotation == other.Rotation
                && MoveInput == other.MoveInput
                && JumpPressed == other.JumpPressed
                && SprintPressed == other.SprintPressed
                && PrimaryPressed == other.PrimaryPressed
                && SecondaryPressed == other.SecondaryPressed
                && IsGrounded == other.IsGrounded;
        }

        public override bool Equals(object obj) => obj is PlayerNetworkState other && Equals(other);

        public override int GetHashCode() => System.HashCode.Combine(Position, Rotation, MoveInput,
            JumpPressed, SprintPressed, PrimaryPressed, SecondaryPressed, IsGrounded);
    }
}
