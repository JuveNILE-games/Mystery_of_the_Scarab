using UnityEngine;

namespace NewInputByReference
{
    [CreateAssetMenu(fileName = "NewAxisInputData", menuName = "New Input By Reference/Axis Input Data")]
    public class AxisInputData : InputData
    {
        public float Axis => NewInput.GetAxis(this);
        
        protected override void OnValidate()
        {
            if(ControlType is "Axis")
                return;
            
            base.OnValidate();            
        }
    }
}