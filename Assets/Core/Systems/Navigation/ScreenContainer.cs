using UnityEngine;

namespace Core.Systems.Navigation{
    public class ScreenContainer : MonoBehaviour{
        

        private void Awake(){
            if (ScreenNavigator.Instance )
            {
                ScreenNavigator.Instance.RegisterScreenContainer(gameObject.transform);
            }
        }
    }
}