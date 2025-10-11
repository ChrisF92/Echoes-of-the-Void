using UnityEngine;

using EchoesOfTheVoid.Core.Roster.Progression.Contracts;
using EchoesOfTheVoid.Core.Roster.Progression.Stats;

namespace EchoesOfTheVoid.Core.Roster.Progression.Definitions {
  [CreateAssetMenu(fileName = "EchoProgressionProfile", menuName = "Roster/Progression/Echo Progression Profile")]
  public class EchoProgressionProfile : ScriptableObject {
    [SerializeField] private LevelProgressionDefinition _levelProgression;
    [SerializeField] private SkillTreeDefinition _skillTree;
    [SerializeField] private StatProgressionDefinition _statProgression;

    public ILevelProgression LevelProgression => _levelProgression;
    public IEchoSkillTreeDefinition SkillTree => _skillTree;
    public IStatProgression StatProgression => _statProgression;

    public bool IsValid => _levelProgression != null && _skillTree != null && _statProgression != null;
  }
}
