using UnityEngine;
using NewInputByReference.Examples;

public class DoubleJumpAbility : AbilityBehaviour
{
    public float extraForce = 6f;
    bool jumpedOnce = false;
    PlayerMovement mover;

    protected override void Awake()
    {
        base.Awake();
        mover = GetComponent<PlayerMovement>();
        if (mover != null) mover.OnLanded += OnLanded;
    }

    public override void TryUse()
    {
        if (mover != null && !mover.IsGrounded && !jumpedOnce)
        {
            mover.AddVerticalVelocity(extraForce);
            jumpedOnce = true;
            nextAvailableTime = Time.time + (data != null ? data.cooldown : 0f);
            OnUsed?.Invoke(owner);
        }
    }

    void OnLanded() { jumpedOnce = false; }
}
