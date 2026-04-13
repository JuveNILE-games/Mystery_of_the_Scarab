using System;
using Game.Systems.PuzzleSystem.Interfaces;
using UnityEngine;

namespace Game.Systems.PuzzleSystem{
    /// <summary>
/// Base class for all physical puzzle elements in the scene (levers, buttons, etc.).
/// Implements IPuzzleCondition so it can be a leaf in a LogicGate tree.
/// Network-ready: override the virtual sync methods to hook in PurrNet.
/// </summary>
[RequireComponent(typeof(Collider))]
public abstract class PuzzleComponent : MonoBehaviour, IPuzzleCondition
{
    [Header("Puzzle Component")]
    [SerializeField] private string _conditionId;
    [SerializeField] private bool _startsActive = true;

    // ── IPuzzleCondition ─────────────────────────────────────────────
    public string ConditionId => _conditionId;
    public bool IsMet => _isMet;
    public event Action<IPuzzleCondition> OnConditionChanged;

    // ── State ────────────────────────────────────────────────────────
    protected bool _isMet;
    protected bool _isLocked;
    protected bool _isActive;

    // ── Lifecycle ────────────────────────────────────────────────────
    protected virtual void Awake()
    {
        _isActive = _startsActive;
        PuzzleComponentRegistry.Register(this);
    }

    protected virtual void OnDestroy() => PuzzleComponentRegistry.Unregister(this);

    public virtual void Reset() => SetState(false);

    public void SetActive(bool active) { _isActive = active; }
    public void SetLocked(bool locked) { _isLocked = locked; OnLockChanged(locked); }

    // ── State mutation (call from subclasses or network) ─────────────
    protected void SetState(bool met)
    {
        if (_isMet == met) return;
        _isMet = met;

        // Hook: authoritative clients broadcast here; network wrapper overrides this.
        OnStateChanged(met);
        OnConditionChanged?.Invoke(this);
    }

    // ── Virtual hooks (override in subclasses for visuals/audio) ─────
    protected virtual void OnStateChanged(bool newState) { }
    protected virtual void OnLockChanged(bool locked) { }

    // ── Network sync hooks (override in NetworkPuzzleComponent) ──────
    /// <summary>Call this from a subclass instead of SetState when you want
    /// the change to be network-authoritative.</summary>
    protected virtual void RequestStateChange(bool met) => SetState(met);

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (string.IsNullOrEmpty(_conditionId))
            _conditionId = $"{GetType().Name}_{gameObject.name}";
    }
#endif
}
}