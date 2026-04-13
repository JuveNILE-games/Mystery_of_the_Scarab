using System.Collections.Generic;

namespace Game.Systems.LogicSystem.Interfaces
{
    public interface ILogicNode{
        bool Evaluate();
        IEnumerable<ILogicNode> GetChildren();
    }
}
