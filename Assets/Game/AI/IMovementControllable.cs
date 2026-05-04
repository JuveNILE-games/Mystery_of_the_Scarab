using UnityEngine;

namespace Game.AI
{
    /// <summary>
    /// Interface for any state machine or movement component that can be driven by external input.
    ///
    /// All movement is expressed in world-space XZ. The camera-relative projection for human
    /// input is done once at the input boundary (PlayerInputInitializer) so that neither states
    /// nor AI systems ever need to touch Camera.main. This also means AI input (NavMesh
    /// desiredVelocity, already world-space) flows through without any conversion overhead.
    /// </summary>
    public interface IMovementControllable
    {
        /// <summary>
        /// Set the world-space movement direction. Y component is ignored — vertical movement
        /// is handled centrally by PlayerStateMachine runtime motion (gravity, jump, etc.).
        /// <para>
        /// For human players: called by PlayerInputInitializer after projecting stick/WASD input
        /// onto the current camera's XZ plane.
        /// For AI: called by AIMovementBridge with NavMeshAgent.desiredVelocity.xz directly.
        /// </para>
        /// </summary>
        /// <param name="worldDir">World-space direction, magnitude 0–1. Zero stops movement.</param>
        void OnMoveWorldSpace(Vector3 worldDir);
    }
}
