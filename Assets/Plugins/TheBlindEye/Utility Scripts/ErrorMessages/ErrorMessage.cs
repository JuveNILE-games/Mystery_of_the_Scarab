using UnityEngine;

namespace TheBlindEye.Utility
{
    public abstract class ErrorMessage
    {
        #if UNITY_EDITOR
            protected const string COLOR_RED = "<color=#FF0000>";
            protected const string COLOR_YELLOW = "<color=#FFFF00>";
            protected const string COLOR_GREEN = "<color=#00FF00>";
            protected const string COLOR_END = "</color>";
            
            private string ErrorTag => _assetName + ' ' + GetType().Name + ": ";
            
            protected abstract string Message { get; set; }
            protected virtual bool IsLogError => true;
        #endif
        
        private readonly AssetName _assetName;

        protected ErrorMessage(AssetName assetName) => _assetName = assetName;
        
        public void Trow()
        {
            #if UNITY_EDITOR
                if(IsLogError)
                    Debug.LogError(ErrorTag + Message);
                else
                    Debug.Log(ErrorTag + Message);
            #endif
        }
    }
}