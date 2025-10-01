using EchoesOfTheVoid.Core.Combat.Results;
using EchoesOfTheVoid.Core.Inventory.Results;

namespace EchoesOfTheVoid.Core.Combat.Extensions {
  public static class ResultExtensions {
    public static ActionResult ToActionResult(this SkillResult skillResult) {
      return skillResult.IsSuccess
        ? ActionResult.Success(skillResult.Message)
        : ActionResult.Failed(skillResult.Message);
    }

    public static ActionResult ToActionResult(this ItemResult itemResult) {
      return itemResult.IsSuccess
        ? ActionResult.Success(itemResult.Message)
        : ActionResult.Failed(itemResult.Message);
    }
  }
}
