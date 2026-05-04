using Game;
using UnityEngine;

// tiny example of hooking a RequiresAbility to a door
public class DoorController : MonoBehaviour
{
    public void Open(PlayerInteractor actor)
    {
        Debug.Log($"Door opened by {actor.name}");
        // animate, enable passage...
    }
}
