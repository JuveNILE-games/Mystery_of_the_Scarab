using UnityEngine;
using Obvious.Soap;

namespace Game.Events
{
    [CreateAssetMenu(fileName = "ScriptableEventControlChanged", menuName = "Soap/ScriptableEvents/ControlChanged")]
    public class ScriptableEventControlChanged : ScriptableEvent<ControlChanged>
    {
    }
}
