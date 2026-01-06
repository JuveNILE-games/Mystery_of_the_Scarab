using UnityEngine;

namespace Game.Events
{
    [System.Serializable]
    public struct ControlChanged
    {
        public int newIndex;
        public Transform newTransform;
    }
}
