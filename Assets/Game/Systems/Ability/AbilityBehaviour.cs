using UnityEngine;
using UnityEngine.Events;
using System;
using Game;

[RequireComponent(typeof(PlayerInteractor))]
public abstract class AbilityBehaviour : MonoBehaviour
{
    public AbilityData data;
    public bool IsAvailable => Time.time >= nextAvailableTime && enabled;
    public bool IsActive { get; protected set; }
    protected float nextAvailableTime = 0f;
    protected PlayerInteractor owner;

    public UnityEvent<PlayerInteractor> OnUsed;
    public UnityEvent<PlayerInteractor> OnStart;
    public UnityEvent<PlayerInteractor> OnStop;

    protected virtual void Awake() { owner = GetComponent<PlayerInteractor>(); }

    public virtual void TryUse()
    {
        if (!IsAvailable) return;
        if (!CanUse()) return;
        if (data.isToggle) { if (IsActive) StopAbility(); else StartAbility(); }
        else Use();
    }

    protected virtual bool CanUse() { return true; }

    protected virtual void Use()
    {
        nextAvailableTime = Time.time + (data != null ? data.cooldown : 0f);
        OnUsed?.Invoke(owner);
        if (data != null && data.effectPrefab != null) Instantiate(data.effectPrefab, owner.transform.position, Quaternion.identity);
    }

    protected virtual void StartAbility()
    {
        IsActive = true;
        nextAvailableTime = Time.time + (data != null ? data.cooldown : 0f);
        OnStart?.Invoke(owner);
    }

    protected virtual void StopAbility()
    {
        IsActive = false;
        OnStop?.Invoke(owner);
    }

    public virtual void ServerUse(PlayerInteractor serverValidatedOwner)
    {
        Use();
    }
}
