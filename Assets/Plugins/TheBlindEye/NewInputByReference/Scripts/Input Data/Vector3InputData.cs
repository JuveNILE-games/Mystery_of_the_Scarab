using UnityEngine;

namespace NewInputByReference
{
    [CreateAssetMenu(fileName = "NewVector3InputData", menuName = "New Input By Reference/Vector3 Input Data")]
    public class Vector3InputData : InputData
    {
        public Vector3 Vector3 => NewInput.GetVector3(this);
        
        protected override void OnValidate()
        {
            if (ControlType is "Vector3")
                return;
            
            base.OnValidate();            
        }
    }
}