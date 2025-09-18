using System.Collections.Generic;

using EchoesOfTheVoid.Core.Combat.Effects;

namespace EchoesOfTheVoid.Core.Combat.Results
{
  public class ActionResult
  {
    public bool IsSuccess { get; private set; }
    public string Message { get; private set; }
    public List<CombatEffect> Effects { get; private set; } = new();

    public static ActionResult Success(string message, List<CombatEffect> effects = null)
    {
      return new ActionResult
      {
        IsSuccess = true,
        Message = message,
        Effects = effects ?? new List<CombatEffect>()
      };
    }

    public static ActionResult Failed(string message)
    {
      return new ActionResult { IsSuccess = false, Message = message };
    }
  }
}

