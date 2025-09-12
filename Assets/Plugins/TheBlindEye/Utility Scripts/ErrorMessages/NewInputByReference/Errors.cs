namespace TheBlindEye.Utility.NewInputByReference
{
    public class Error01 : ErrorMessage
    {
        #if UNITY_EDITOR
            protected sealed override string Message
            {
                get =>
                    $"Input Action is {COLOR_RED}Null{COLOR_END}. " +
                    $"{COLOR_YELLOW}Ensure that an Input Action is assigned to all Input Data, " +
                    $"or if there is an Input Handler Script and a Player Input Script attached to the same game object in the scene.{COLOR_END}";
                set { }
            }
        #endif
        
        public Error01() : base(AssetName.NewInputByReference)
        { }
    }
    
    public class Error02 : ErrorMessage
    {
        #if UNITY_EDITOR
            protected sealed override string Message { get; set; }
        #endif
        
        public Error02(string actionName, string receivedControlType, string expectedControlType, string actionMap) 
            : base(AssetName.NewInputByReference)
        {
            #if UNITY_EDITOR
                Message = $"Invalid Control Type for {COLOR_YELLOW}Action {actionName}{COLOR_END}. " +
                          $"Got {COLOR_RED}{receivedControlType}{COLOR_END}, expected {COLOR_GREEN}{expectedControlType}{COLOR_END}. " +
                          $"Action Map: {COLOR_YELLOW}{actionMap}{COLOR_END}.";
            #endif
        }
    }
    
    public class Error03 : ErrorMessage
    {
        #if UNITY_EDITOR
            protected sealed override string Message
            {
                get =>
                    $"{COLOR_RED}The Input System Package is outdated, " +
                    $"and it must be updated to version 1.1.1 or above in order to save/load the rebinds.{COLOR_END}";
                set { }
            }
        #endif
        
        public Error03() : base(AssetName.NewInputByReference)
        { }
    }
    
    public class Error04 : ErrorMessage
    {
        #if UNITY_EDITOR
            protected sealed override string Message
            {
                get =>
                    $"{COLOR_RED}No BindingsImages Scriptable Object found.{COLOR_END} " +
                    $"{COLOR_YELLOW}Generate one using the Generate Bindings Images Window, and assign it using NewInput.SetBindingsImages function, " +
                    $"or in the bindingsImages field of the InputHandler.{COLOR_END}";
                set { }
            }
        #endif
        
        public Error04() : base(AssetName.NewInputByReference)
        { }
    }

    public class Error05 : ErrorMessage
    {
        #if UNITY_EDITOR
            protected sealed override string Message { get; set; }
        #endif
        
        public Error05(string actionName, int bindingIndex, int bindingsCount) : base(AssetName.NewInputByReference)
        {
            #if UNITY_EDITOR
                Message = $"StartRebinding function was called for the {COLOR_YELLOW}Input Action {actionName}{COLOR_END}. " +
                          $"The assigned bindingIndex parameter is {COLOR_RED}{bindingIndex}{COLOR_END}, " +
                          $"and and it should be {COLOR_GREEN}< {bindingsCount}{COLOR_END}.";
            #endif
        }
    }
    
    public class Error06 : ErrorMessage
    {
        #if UNITY_EDITOR
            protected sealed override string Message { get; set; }
        #endif
        
        public Error06(string actionName) : base(AssetName.NewInputByReference)
        {
            #if UNITY_EDITOR
                Message = $"Invalid Control Type for {COLOR_YELLOW}Action {actionName}{COLOR_END}.";
            #endif
        }
    }

    public class Error07 : ErrorMessage
    {
        #if UNITY_EDITOR
            protected override bool IsLogError { get; } = false;
            
            protected sealed override string Message
            {
                get =>
                    $"{COLOR_RED}No Player Input found.{COLOR_END} " +
                    $"{COLOR_YELLOW}There must be an Input Handler Script and a Player Input Script attached to the same game object in the scene.{COLOR_END}";
                set { }
            }
        #endif
        
        public Error07() : base(AssetName.NewInputByReference)
        { }
    }
}