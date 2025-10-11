using EchoesOfTheVoid.Core.Roster.Data;
using EchoesOfTheVoid.Core.Roster.Progression.Results;

namespace EchoesOfTheVoid.Core.Roster.Progression.Contracts {
  public interface IEchoProgressionService {
    EchoExperienceGainResult GrantExperience(PlayerEchoData echo, ILevelProgression progression, int experience);
    SkillUnlockResult TryUnlockNode(PlayerEchoData echo, IEchoSkillTreeDefinition skillTree, string nodeId);
    void InitializeEcho(PlayerEchoData echo, IEchoSkillTreeDefinition skillTree);
  }
}
