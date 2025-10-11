using UnityEngine;

using EchoesOfTheVoid.Core.Roster.Progression.Contracts;

namespace EchoesOfTheVoid.Core.Roster.Progression.Definitions {
  [CreateAssetMenu(fileName = "EchoProgressionProfile", menuName = "Roster/Progression/Echo Progression Profile")]
  public class EchoProgressionProfile : ScriptableObject {
    [SerializeField] private LevelProgressionDefinition _levelProgression;
    [SerializeField] private SkillTreeDefinition _skillTree;

    public ILevelProgression LevelProgression => _levelProgression;
    public IEchoSkillTreeDefinition SkillTree => _skillTree;

    public bool IsValid => _levelProgression != null && _skillTree != null;
  }
}
