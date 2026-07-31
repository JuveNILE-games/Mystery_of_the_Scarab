namespace Game.Player.States.Grounded{
    public class LandState : PlayerState
    {
        public LandState() : base("Land") { }
        
        public override void OnEnter()
        {
            base.OnEnter();
            if (Owner != null)
            {
                Owner.VerticalVelocity = Owner.Data.Value != null ? Owner.Data.Value.GroundStickForce : -5f;
            }
            if (Animator != null) Animator.Play("Land");
        }
    }
}
