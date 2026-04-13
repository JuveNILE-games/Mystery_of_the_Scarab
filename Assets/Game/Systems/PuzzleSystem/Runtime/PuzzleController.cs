using System;
using System.Collections;
using System.Collections.Generic;
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

    public event Action<PuzzleController> OnSolved;
    public event Action<PuzzleController> OnFailed;
    public event Action<PuzzleController> OnReset;

    private readonly List<IPuzzleCondition> _resolvedConditions = new();
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

        // Check prerequisite
        // (PuzzleRoomController handles locking until prerequisite is met)

        // Walk the conditionDescriptors list and resolve each one.
        foreach (var descriptor in _definition.conditionDescriptors)
        {
            if (PuzzleComponentRegistry.TryGet(descriptor.ConditionId, out var condition))
            {
                _resolvedConditions.Add(condition);
                condition.OnConditionChanged += _ => Evaluate();
            }
            // InlineConditionDescriptor: the condition is already inline, register it too.
            if (descriptor is InlineConditionDescriptor inl && inl.condition != null)
            {
                PuzzleComponentRegistry.Register(inl.condition);
                _resolvedConditions.Add(inl.condition);
                inl.condition.OnConditionChanged += _ => Evaluate();
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
        // TODO: fire rewards, dialogue, SFX via a PuzzleRewardExecutor
        OnSolved?.Invoke(this);
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
}
}