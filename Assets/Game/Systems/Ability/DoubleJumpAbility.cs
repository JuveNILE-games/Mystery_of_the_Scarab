using UnityEngine;
using Game.Player;

/// <summary>
/// Double-jump ability that integrates with the PlayerStateMachine.
/// Allows one additional jump while airborne, using CharacterController velocity injection.
/// </summary>
public class DoubleJumpAbility : AbilityBehaviour
{
    [Header("Double Jump")]
    public float extraForce = 6f;

    private bool _jumpedOnce = false;
    private PlayerStateMachine _stateMachine;
    private CharacterController _controller;

    protected override void Awake()
    {
        base.Awake();
        _stateMachine = GetComponent<PlayerStateMachine>();
        _controller = GetComponent<CharacterController>();
    }

    public override void TryUse()
    {
        if (_stateMachine == null || _controller == null) return;

        // Allow double jump only when airborne and hasn't already double-jumped
        if (!_stateMachine.IsGrounded && !_jumpedOnce)
        {
            // Inject upward velocity via CharacterController.Move
            // This works with the existing gravity system in the state machine
            _controller.Move(Vector3.up * extraForce * Time.deltaTime);
            _jumpedOnce = true;
            nextAvailableTime = Time.time + (data != null ? data.cooldown : 0f);
            OnUsed?.Invoke(owner);
        }
    }

    private void Update()
    {
        // Reset double-jump when we land
        if (_stateMachine != null && _stateMachine.IsGrounded && _jumpedOnce)
        {
            _jumpedOnce = false;
        }
    }
}
