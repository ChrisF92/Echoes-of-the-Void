using System.Collections.Generic;
using EchoesOfTheVoid.Core.Combat.Effects;

namespace EchoesOfTheVoid.Core.Inventory.Results {
  public class ItemResult {
    public bool IsSuccess { get; private set; }
    public string Message { get; private set; }
    public List<CombatEffect> Effects { get; private set; } = new();

    public static ItemResult Success(string message, List<CombatEffect> effects = null) {
      return new ItemResult {
        IsSuccess = true,
        Message = message,
        Effects = effects ?? new List<CombatEffect>()
      };
    }

    public static ItemResult Failed(string message) {
      return new ItemResult { IsSuccess = false, Message = message, Effects = new List<CombatEffect>() };
    }
  }
}
