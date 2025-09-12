using UnityEngine;

namespace NewInputByReference
{
    [CreateAssetMenu(fileName = "NewVector2InputData", menuName = "New Input By Reference/Vector2 Input Data")]
    public class Vector2InputData : InputData
    {
        public Vector2 Vector2 => NewInput.GetVector2(this);
        
        protected override void OnValidate()
        {
            if (ControlType is "Vector2")
                return;
            
            base.OnValidate();            
        }
    }
}