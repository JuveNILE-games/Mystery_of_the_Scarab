using AYellowpaper.SerializedCollections;
using UnityEngine;

namespace Core.Utility{
    [CreateAssetMenu(fileName = "ScriptableDictionary", menuName = "Core/Scriptable Objects/ScriptableDictionary")]
    public class ScriptableDictionary : ScriptableObject
    {
        public SerializedDictionary<ScriptableObject, ScriptableObject> dictionary;
    }
}
