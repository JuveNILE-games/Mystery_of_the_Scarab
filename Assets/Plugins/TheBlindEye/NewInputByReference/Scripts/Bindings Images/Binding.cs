using UnityEngine;
using TheBlindEye.Utility;

namespace NewInputByReference.Bindings
{
    public enum KeyType
    {
        Keyboard = 11,
        Gamepad = 10,
        Mouse = 8
    }
    
    [System.Serializable]
    public struct Binding
    {
        public Binding(string name, Sprite sprite)
        {
            Name = name;
            Sprite = sprite;
        }
        
        [field: SerializeField, ReadOnly]
        public string Name { get; private set; }
        
        [field: SerializeField]
        public Sprite Sprite { get; private set; }
    }
}