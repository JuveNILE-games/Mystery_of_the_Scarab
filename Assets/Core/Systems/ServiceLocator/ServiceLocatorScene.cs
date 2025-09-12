using UnityEngine;

namespace Core.Scripts.ServiceLocator{
    [AddComponentMenu("ServiceLocator/ServiceLocator Scene")]
    public class ServiceLocatorScene : Bootstrapper {
        protected override void Bootstrap() {
            Container.ConfigureForScene();            
        }
    }
}