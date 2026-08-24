using Core.Utility;
using NetCore.Abstractions;
using Game.Net.Abstractions;
using Game.Player;
using UnityEngine;

namespace Game.Net
{
    [DisallowMultipleComponent]
    public class PlayerNetworkStateBridge : MonoBehaviour,
        INetworkStateSource<PlayerNetworkState>,
        INetworkStateSink<PlayerNetworkState>
    {
        [SerializeField] private PlayerInputInitializer _playerInput;
        [SerializeField] private PlayerStateMachine     _stateMachine;
        [SerializeField] private float _positionLerpSpeed = 15f;
        [SerializeField] private float _rotationLerpSpeed = 15f;

        // ── INetworkStateSource ──────────────────────────────────────────────

        public bool TryCapture(out PlayerNetworkState state)
        {
            state = default;
            if (_stateMachine == null) return false;

            state.Position         = transform.position;
            state.Rotation         = transform.rotation;
            state.MoveInput        = _stateMachine.WorldMoveInput;
            state.JumpPressed      = _stateMachine.IsJumpPressed;
            state.SprintPressed    = _stateMachine.IsSprintPressed;
            state.PrimaryPressed   = _stateMachine.IsPrimaryAbilityPressed;
            state.SecondaryPressed = _stateMachine.IsSecondaryAbilityPressed;
            state.IsGrounded       = _stateMachine.IsGrounded;
            return true;
        }

        // ── INetworkStateSink ────────────────────────────────────────────────

        public void Apply(in PlayerNetworkState state, in NetworkSyncContext context)
        {
            transform.position = Vector3.Lerp(
                transform.position, state.Position,
                context.DeltaTime * _positionLerpSpeed);

            transform.rotation = Quaternion.Slerp(
                transform.rotation, state.Rotation,
                context.DeltaTime * _rotationLerpSpeed);

            if (_stateMachine == null) return;

            _stateMachine.SetReplicatedGrounded(state.IsGrounded);
            _stateMachine.OnMoveWorldSpace(state.MoveInput);
            _stateMachine.OnJump(state.JumpPressed);
            _stateMachine.OnSprint(state.SprintPressed);
            _stateMachine.OnPrimaryAbility(state.PrimaryPressed);
            _stateMachine.OnSecondaryAbility(state.SecondaryPressed);
        }

        // ── Input/physics gating (called by adapter on spawn) ────────────────

        public void SetLocalInputEnabled(bool enabled)
        {
            if (_playerInput != null)
            {
                _playerInput.enabled = enabled;
                if (!enabled) _playerInput.ClearInputState();
            }

            // Same boolean, same moment (owner vs non-owner) — a non-owned copy must never run
            // its own local physics simulation (CheckGrounded/ApplyMovement), only Apply()'s
            // lerp + the replicated grounded state above should ever move/ground-check it.
            _stateMachine?.SetPhysicsEnabled(enabled);

            // ControlSwitcher (Solo-only) sets this for the local-multiplayer path; for LAN/Online
            // there's no ControlSwitcher, so the local owner's own spawn/ownership-change is the
            // only signal available to point the camera at it.
            if (enabled && SceneCamera.Instance != null)
            {
                SceneCamera.Instance.TrackingTarget.Value = transform;
            }
        }
    }
}
