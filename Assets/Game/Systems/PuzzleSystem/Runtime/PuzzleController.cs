using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Game.Systems.LogicSystem;
using Game.Systems.LogicSystem.Interfaces;
using Game.Systems.PuzzleSystem.Definitions;
using Game.Systems.PuzzleSystem.Interfaces;
using UnityEngine;

namespace Game.Systems.PuzzleSystem.Runtime{
    public class PuzzleController : MonoBehaviour
{
    [SerializeField] private PuzzleDefinition _definition;

    public PuzzleDefinition Definition => _definition;
    public bool IsSolved { get; private set; }
    public bool IsFailed { get; private set; }
    public bool IsSolveConfirmed { get; private set; }

    /// <summary>
    /// Fires the instant this client's own condition tree evaluates as solved — immediate,
    /// not network-confirmed. Fine for local/visual reactions (animate the lever, play a
    /// sting), but two clients can independently evaluate this at slightly different times.
    /// Reward-firing / door-unlocking / anything with a real game-state side effect should
    /// subscribe to <see cref="OnSolvedConfirmed"/> instead so it only happens once.
    /// </summary>
    public event Action<PuzzleController> OnSolved;
    public event Action<PuzzleController> OnFailed;
    public event Action<PuzzleController> OnReset;

    /// <summary>
    /// Fires once this puzzle's solved state has been network-confirmed (via
    /// NetworkPuzzleRoomController), or immediately if there's no NetworkManager in the scene
    /// (single-player). Idempotent — only ever fires once per solve.
    /// </summary>
    public event Action<PuzzleController> OnSolvedConfirmed;

    private readonly List<IPuzzleCondition> _resolvedConditions = new();
    private Action<IPuzzleCondition> _evaluateHandler;
    private Coroutine _timerRoutine;

    private void Start() => Initialize();

    public void Initialize(PuzzleDefinition definition)
    {
        _definition = definition;
        Initialize();
    }

    public void Initialize()
    {
        if (_definition == null) return;

        // Unsubscribe old handler before clearing to prevent double-subscription (§1)
        if (_evaluateHandler != null)
        {
            foreach (var c in _resolvedConditions)
                c.OnConditionChanged -= _evaluateHandler;
        }

        _resolvedConditions.Clear();
        _evaluateHandler = _ => Evaluate();

        // Walk the conditionDescriptors list and resolve each one.
        foreach (var descriptor in _definition.conditionDescriptors)
        {
            if (PuzzleComponentRegistry.TryGet(descriptor.ConditionId, out var condition))
            {
                _resolvedConditions.Add(condition);
                condition.OnConditionChanged += _evaluateHandler;
            }
            // InlineConditionDescriptor: the condition is already inline, register it too.
            if (descriptor is InlineConditionDescriptor inl && inl.condition != null)
            {
                PuzzleComponentRegistry.Register(inl.condition);
                _resolvedConditions.Add(inl.condition);
                inl.condition.OnConditionChanged += _evaluateHandler;
            }
        }

        // Resolve LogicLeaf references in the tree
        ResolveLogicTree(_definition.rootNode);

        if (_definition.canFail && _definition.timeLimit > 0f)
            _timerRoutine = StartCoroutine(FailTimer());

        Evaluate();
    }

    private void ResolveLogicTree(ILogicNode node)
    {
        if (node is LogicLeaf leaf)
        {
            if (PuzzleComponentRegistry.TryGet(leaf.conditionId, out var c))
                leaf.ResolvedCondition = c;
            return;
        }
        foreach (var child in node.GetChildren())
            ResolveLogicTree(child);
    }

    private void Evaluate()
    {
        if (IsSolved || IsFailed) return;
        if (_definition.rootNode == null) return;

        if (_definition.rootNode.Evaluate())
            Solve();
    }

    private void Solve()
    {
        IsSolved = true;
        if (_timerRoutine != null) { StopCoroutine(_timerRoutine); _timerRoutine = null; }
        OnSolved?.Invoke(this);
    }

    /// <summary>
    /// Called by NetworkPuzzleRoomController once the server has confirmed this puzzle's
    /// solve (or immediately, if nothing is listening to broker that confirmation — e.g. no
    /// NetworkManager in the scene). Idempotent so a duplicate confirmation is a no-op.
    /// </summary>
    public void ConfirmSolved()
    {
        if (IsSolveConfirmed) return;
        IsSolveConfirmed = true;
        OnSolvedConfirmed?.Invoke(this);
    }

    public void Fail()
    {
        IsFailed = true;
        OnFailed?.Invoke(this);
    }

    public void ResetPuzzle()
    {
        IsSolved = false;
        IsFailed = false;
        IsSolveConfirmed = false;
        foreach (var c in _resolvedConditions) c.Reset();
        if (_timerRoutine != null) StopCoroutine(_timerRoutine);
        if (_definition.canFail && _definition.timeLimit > 0f)
            _timerRoutine = StartCoroutine(FailTimer());
        OnReset?.Invoke(this);
    }

    private IEnumerator FailTimer()
    {
        yield return new WaitForSeconds(_definition.timeLimit);
        if (!IsSolved) Fail();
    }

    public IEnumerable<IPuzzleCondition> GetUnmetConditions()
        => _resolvedConditions.Where(c => !c.IsMet);
}
}