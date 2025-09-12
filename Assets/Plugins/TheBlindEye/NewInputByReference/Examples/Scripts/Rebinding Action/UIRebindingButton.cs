using UnityEngine;
using UnityEngine.UI;

namespace NewInputByReference.Examples
{
    public class UIRebindingButton : MonoBehaviour
    {
        public static bool InRebinding { get; private set; }
        
        // TODO: In 1.3.2, an editor script will combine "bindingIndex" and "allCompositeBindings" into a single dropdown.
        [Header("Settings")]
        [SerializeField] private InputData inputData;
        [SerializeField] private int bindingIndex;
        [SerializeField] private bool allCompositeBindings;
        [Space(10f)]
        [SerializeField] private InputData cancelInputData;
        [SerializeField] private InputData excludedInputData;

        private Text _textUI;

        private void Start()
        {
            _textUI = GetComponentInChildren<Text>();
            SetTextUI();
        } 
 
        private void OnEnable() => UIControlsDefaultButton.OnClicked += ResetRebinds;
        private void OnDisable() => UIControlsDefaultButton.OnClicked -= ResetRebinds;

        // Key -> Button Component -> On Click() 
        public void StartRebinding()
        {
            InRebinding = true;
            _textUI.enabled = false;
            
            //NewInput.StartRebinding(inputData, bindingIndex, allCompositeBindings, OnComplete, cancelInputData, excludedInputData);
            
            // Called when the rebinding is finished    
            void OnComplete()
            {
                SetTextUI();
                InRebinding = false;
                _textUI.enabled = true;
            }
        } 

        // Reset Button -> Button Component -> On Click() 
        public void ResetRebind()
        {
            NewInput.ResetRebind(inputData);
            SetTextUI();
        }

        // Called when the Default Button is pressed
        private void ResetRebinds()
        {
            SetTextUI();
        }
        
        // Sets "textUI"'s text to the current binding name of the Input Action indicated by "inputData" (e.g. E, Tab, A, W, ...)
        private void SetTextUI()
        {
            _textUI.text = NewInput.GetBindingName(inputData, bindingIndex, allCompositeBindings, NameOptions.UseShortNames);
        }
    }
}