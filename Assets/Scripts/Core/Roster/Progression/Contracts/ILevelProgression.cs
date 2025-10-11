namespace EchoesOfTheVoid.Core.Roster.Progression.Contracts {
  public interface ILevelProgression {
    int GetExperienceRequiredForLevel(int currentLevel);
    int GetSkillPointsGrantedAtLevel(int level);
    bool IsMaxLevel(int level);
  }
}
