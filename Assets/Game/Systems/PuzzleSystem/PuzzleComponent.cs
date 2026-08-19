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

    [Header("AI Interaction")]
    [SerializeField] private bool _availableToAI = true;
    [SerializeField] private bool _requiresHolding = false;

    // ── IPuzzleCondition ─────────────────────────────────────────────
    public string ConditionId => _conditionId;
    public bool IsMet => _isMet;
    public event Action<IPuzzleCondition> OnConditionChanged;

    // ── State ────────────────────────────────────────────────────────
    protected bool _isMet;
    protected bool _isLocked;
    protected bool _isActive;

    // ── AI ───────────────────────────────────────────────────────────
    /// <summary>
    /// Returns true when this component can be targeted by the companion AI.
    /// Set to false for components that the player must activate (narrative gates, etc.)
    /// </summary>
    public virtual bool IsAvailableForAI() => _availableToAI && _isActive && !_isLocked;
    public bool RequiresHolding => _requiresHolding;

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

    // ── Network sync (decision, 2026-08-19: still unbuilt, tracked work item) ──
    // Per-component condition state (this specific lever/plate/button) currently has NO
    // network sync — only the puzzle's final aggregate solved state is server-confirmed,
    // via NetworkPuzzleRoomController/PuzzleController.OnSolvedConfirmed (see AGENTS.md's
    // Multiplayer section). That's a real gap for THIS game's design, not a deferred nice-
    // to-have: co-op puzzles are specced around one player watching a mechanism the other
    // player triggers in real time (lever -> door, plate -> gate). Without component-level
    // sync, two clients can genuinely diverge mid-puzzle until the root condition happens
    // to resolve. A "NetworkPuzzleComponent" override point was never built; this virtual
    // hook is the intended seam for it. Scoped as roadmap Phase 2 work (or earlier if new
    // puzzle content ships first) — see the Scarab Engineering Roadmap.
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