using UnityEngine;
using Game.Player;

/// <summary>
/// Double-jump ability that integrates with the PlayerStateMachine.
/// Allows one additional jump while airborne by writing into PlayerStateMachine vertical velocity.
/// </summary>
public class DoubleJumpAbility : AbilityBehaviour
{
    [Header("Double Jump")]
    public float extraForce = 6f;

    private bool _jumpedOnce = false;
    private PlayerStateMachine _stateMachine;

    protected override void Awake()
    {
        base.Awake();
        _stateMachine = GetComponent<PlayerStateMachine>();
    }

    public override void TryUse()
    {
        if (_stateMachine == null) return;

        // Allow double jump only when airborne and hasn't already double-jumped
        if (!_stateMachine.IsGrounded && !_jumpedOnce)
        {
            _stateMachine.VerticalVelocity = extraForce;
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
