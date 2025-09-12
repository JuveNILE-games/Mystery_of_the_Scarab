using UnityEngine;
#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#endif

namespace Obvious.Soap
{
    [CreateAssetMenu(fileName = "scriptable_variable_int.asset", menuName = "Soap/ScriptableVariables/int")]
    public class IntVariable : ScriptableVariable<int>
    {
        [Tooltip("Clamps the value of this variable to a minimum and maximum.")] 
#if ODIN_INSPECTOR
        [PropertyOrder(5)]
#endif
        [SerializeField]
        private bool _isClamped = false;
        public bool IsClamped => _isClamped;

        [Tooltip("If clamped, sets the minimum and maximum")] 
        [SerializeField]
#if ODIN_INSPECTOR
        [ShowIf("_isClamped", true)]
        [PropertyOrder(6)][Indent]
#endif
        private IntReference _min = new IntReference(0,true);
        public IntReference MinReference => _min;
        public int Min => _min.Value;
        
        [Tooltip("If clamped, sets the minimum and maximum")] 
        [SerializeField]
#if ODIN_INSPECTOR
        [ShowIf("_isClamped", true)]
        [PropertyOrder(7)][Indent]
#endif
        private IntReference _max = new IntReference(int.MaxValue,true);
        public IntReference MaxReference => _max;
        public int Max => _max.Value;
        
        /// <summary>
        /// Returns the percentage of the value between the minimum and maximum.
        /// </summary>
        public float Ratio => Mathf.InverseLerp( _min.Value, _max.Value, _value);

        public override void Save()
        {
            PlayerPrefs.SetInt(Guid, Value);
            base.Save();
        }

        public override void Load()
        {
            Value = PlayerPrefs.GetInt(Guid, DefaultValue);
            base.Load();
        }

        public void Add(int value)
        {
            Value += value;
        }

        public override int Value
        {
            get => base.Value;
            set
            {
                var clampedValue = _isClamped ? Mathf.Clamp(value, _min.Value, _max.Value) : value;
                base.Value = clampedValue;
            }
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            if (IsClamped)
            {
                var clampedValue = Mathf.Clamp(Value, _min.Value, _max.Value);
                if (Value < clampedValue || Value > clampedValue)
                    Value = clampedValue;
            }

            base.OnValidate();
        }
#endif
    }
}