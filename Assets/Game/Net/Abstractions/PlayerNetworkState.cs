using UnityEngine;

namespace Game.Net.Abstractions
{
    /// <summary>
    /// Blittable snapshot of player state broadcast each FixedUpdate by the owner.
    /// Keep fields simple (Vector3 / Quaternion / bool) to ensure PurrNet RPC
    /// serialization remains trivial.
    /// </summary>
    public struct PlayerNetworkState
    {
        public Vector3    Position;
        public Quaternion Rotation;
        public Vector3    MoveInput;
        public bool       JumpPressed;
        public bool       SprintPressed;
        public bool       PrimaryPressed;
        public bool       SecondaryPressed;
    }
}
