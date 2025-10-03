using System.Collections.Generic;
using EchoesOfTheVoid.Core.Combat.Effects;

namespace EchoesOfTheVoid.Core.Combat.Results {
  public class SkillResult {
    public bool IsSuccess { get; private set; }
    public string Message { get; private set; }
    public List<CombatEffect> Effects { get; private set; } = new();

    public static SkillResult Success(string message, List<CombatEffect> effects = null) {
      return new SkillResult {
        IsSuccess = true,
        Message = message,
        Effects = effects ?? new List<CombatEffect>()
      };
    }

    public static SkillResult Failed(string message) {
      return new SkillResult { IsSuccess = false, Message = message, Effects = new List<CombatEffect>() };
    }
  }
}
