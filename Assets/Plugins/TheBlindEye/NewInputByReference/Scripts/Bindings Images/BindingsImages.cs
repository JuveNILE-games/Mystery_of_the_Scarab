using System.Collections.Generic;
using UnityEngine;
using NewInputByReference.Bindings;

namespace NewInputByReference
{
    public class BindingsImages : ScriptableObject
    {
        [SerializeField] public List<Binding> keyboard = new List<Binding>();
        [SerializeField] public List<Binding> gamepad = new List<Binding>();
        [SerializeField] public List<Binding> mouse = new List<Binding>();
        
        public void AddBinding(Binding binding, KeyType keyType) => GetList(keyType).Add(binding);
        
        public void SetSprite(Sprite sprite, int index, KeyType keyType)
        {
            var list = GetList(keyType);
            if (index >= list.Count)
                return;
            
            var binding = list[index];
            if(binding.Sprite != sprite)
                list[index] = new Binding(binding.Name, sprite);
        }

        public Sprite GetBindingSprite(string bindingName, KeyType keyType)
        {
            foreach (var binding in GetList(keyType))
            {
                if (string.Equals(binding.Name, bindingName))
                    return binding.Sprite;
            }

            return null;
        }
        
        public Sprite GetBindingSprite(int index, KeyType keyType)
        {
            var list = GetList(keyType);
            return index < list.Count ? list[index].Sprite : null;
        }

        private List<Binding> GetList(KeyType keyType)
        {
            return keyType switch
            {
                KeyType.Keyboard => keyboard,
                KeyType.Gamepad => gamepad,
                KeyType.Mouse => mouse,
                _ => null
            };
        }
    }
}