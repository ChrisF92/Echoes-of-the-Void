using EchoesOfTheVoid.Core.Roster.Progression.Definitions;

namespace EchoesOfTheVoid.Core.Roster.Progression.Results {
  public readonly struct SkillUnlockResult {
    public SkillUnlockResult(bool success, SkillTreeNodeDefinition node, int skillPointsSpent, string errorMessage) {
      Success = success;
      Node = node;
      SkillPointsSpent = skillPointsSpent;
      ErrorMessage = errorMessage ?? string.Empty;
    }

    public bool Success { get; }
    public SkillTreeNodeDefinition Node { get; }
    public int SkillPointsSpent { get; }
    public string ErrorMessage { get; }
  }
}
