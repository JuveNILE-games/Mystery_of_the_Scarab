using Core.Systems.Navigation;
using UnityEngine;

namespace Game.Systems.LevelSystem.Runtime
{
    // Shared, game-wide "feel" config for non-Euclidean doorway crossings (SCARAB-012) —
    // deliberately separate from LevelConnectionCuration, which is per-level pairing data.
    // Retuning how Felt/Hidden crossings look shouldn't require editing every level's
    // curation asset.
    [CreateAssetMenu(fileName = "NonEuclideanTransitionDefaults", menuName = "JuveNILE Games/Mystery of the Scarab/Level System/Generator/Non-Euclidean Transition Defaults")]
    public class NonEuclideanTransitionDefaults : ScriptableObject
    {
        public TransitionDefinition FeltTransition;
        public TransitionDefinition HiddenTransition;
    }
}
