using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerAdapter))]
public class PlayerInputHandler : MonoBehaviour
{
    PlayerAdapter adapter;
    PlayerInteractor interactor;
    PlayerAbilities abilities;
    PlayerInput playerInput;

    void Awake()
    {
        adapter = GetComponent<PlayerAdapter>();
        interactor = GetComponent<PlayerInteractor>();
        abilities = GetComponent<PlayerAbilities>();
        playerInput = GetComponent<PlayerInput>();
    }

    public void OnMove(InputValue value)
    {
        if (!adapter) return;
        if (!adapter.GetComponent<PlayerAdapter>()) return;
        if (!adapter.GetTransform()) return;
        if (!adapter.GetComponent<PlayerAdapter>()) return;
        if (!adapter.gameObject.activeInHierarchy) return;
        if (!adapter.GetComponent<PlayerAdapter>().GetComponent<PlayerInteractor>().IsControlled) return;
        var v = value.Get<Vector2>();
        SendMessage("OnMoveInput", v, SendMessageOptions.DontRequireReceiver);
    }

    public void OnInteract(InputValue value)
    {
        if (!adapter) return;
        if (!adapter.GetTransform()) return;
        if (!adapter.GetComponent<PlayerInteractor>().IsControlled) return;
        if (value.isPressed) interactor?.TryLocalInteract();
    }

    public void OnAbilityPrimary(InputValue value)
    {
        if (!adapter) return;
        if (!adapter.GetComponent<PlayerInteractor>().IsControlled) return;
        if (value.isPressed) abilities?.GetContextualAbility()?.TryUse();
    }
}
