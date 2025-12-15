using Game.Player.States;

public class FallingState : PlayerState
{
    public FallingState() : base("Falling") { }
        
    public override void OnEnter()
    {
        base.OnEnter();
        Animator?.Play("Fall");
    }
        
    public override void OnUpdate()
    {
        base.OnUpdate();
            
        // Additional fall behavior could go here
        // e.g., coyote time, fall speed limits, etc.
    }
}