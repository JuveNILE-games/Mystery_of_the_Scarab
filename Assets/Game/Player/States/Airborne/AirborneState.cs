using Core.Systems.StateMachine;
using Game.Player;
using Game.Player.States;
using UnityEngine;

/// <summary>
    /// Hierarchical state for airborne movement.
    /// Contains: Falling
    /// </summary>
public class AirborneState : HierarchicalState<PlayerStateMachine, PlayerState>
{
    public override void OnEnter()
    {
        base.OnEnter();
        Debug.Log("[PlayerSM] Entered Airborne state");
    }

    public override void OnUpdate()
    {
        base.OnUpdate();

        if (Owner == null || Owner.Data.Value == null)
        {
            return;
        }

        if (!Owner.IsJumpPressed && Owner.VerticalVelocity > 0f)
        {
            Owner.VerticalVelocity += Owner.Data.Value.Gravity * Owner.Data.Value.JumpCutMultiplier * Time.deltaTime;
        }
    }
}
