using UnityEngine;

namespace NewInputByReference
{
    [CreateAssetMenu(fileName = "NewButtonInputData", menuName = "New Input By Reference/Button Input Data")]
    public class ButtonInputData : InputData
    {
        public bool ButtonPressed => NewInput.GetButtonDown(this);
        public bool ButtonHolding => NewInput.GetButton(this);
        public bool ButtonReleased => NewInput.GetButtonUp(this);
        
        protected override void OnValidate()
        {
            if (ControlType is "Button")
                return;
            
            base.OnValidate();            
        }
    }
}