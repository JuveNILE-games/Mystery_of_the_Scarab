using System;
using UnityEngine;

namespace NewInputByReference.Examples
{
    public class UIControlsDefaultButton : MonoBehaviour
    {
        public static event Action OnClicked;
        
        public void OnChangeToDefault()
        {
            NewInput.ResetRebinds();
            OnClicked?.Invoke();
        }
    }
}