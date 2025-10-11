using System.Collections.Generic;

namespace EchoesOfTheVoid.Core.Roster.Progression.Results {
  public readonly struct EchoExperienceGainResult {
    public EchoExperienceGainResult(int experienceGained, int levelsGained, IReadOnlyList<int> levelsReached, int skillPointsGranted) {
      ExperienceGained = experienceGained;
      LevelsGained = levelsGained;
      LevelsReached = levelsReached;
      SkillPointsGranted = skillPointsGranted;
    }

    public int ExperienceGained { get; }
    public int LevelsGained { get; }
    public IReadOnlyList<int> LevelsReached { get; }
    public int SkillPointsGranted { get; }
    public bool LeveledUp => LevelsGained > 0;
  }
}
