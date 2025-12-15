namespace Game.Player.States.Grounded{
    public class LandState : PlayerState
    {
        public LandState() : base("Land") { }
        
        public override void OnEnter()
        {
            base.OnEnter();
            Animator?.Play("Land");
        }
    }
}