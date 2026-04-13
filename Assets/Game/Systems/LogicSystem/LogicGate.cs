using System;
using System.Collections.Generic;
using System.Linq;
using Game.Systems.LogicSystem.Enums;
using Game.Systems.LogicSystem.Interfaces;
using UnityEngine;

namespace Game.Systems.LogicSystem{
    [Serializable]
    public class LogicGate : ILogicNode
    {
        public LogicGateType gateType; // AND, OR, NOT, XOR, NAND, NOR

        [SerializeReference]
        public List<ILogicNode> children = new();

        public bool Evaluate() => gateType switch
        {
            LogicGateType.AND  => children.All(c => c.Evaluate()),
            LogicGateType.OR   => children.Any(c => c.Evaluate()),
            LogicGateType.NOT  => children.Count > 0 && !children[0].Evaluate(),
            LogicGateType.XOR  => children.Count(c => c.Evaluate()) % 2 == 1,
            LogicGateType.NAND => !children.All(c => c.Evaluate()),
            LogicGateType.NOR  => !children.Any(c => c.Evaluate()),
            _ => false
        };

        public IEnumerable<ILogicNode> GetChildren() => children;
    }
}