namespace EchoesOfTheVoid.Core.Combat.Results {
  public class SkillResult {
    public bool IsSuccess { get; private set; }
    public string Message { get; private set; }

    public static SkillResult SuccessResult(string message) {
      return new SkillResult { IsSuccess = true, Message = message };
    }

    public static SkillResult Failed(string message) {
      return new SkillResult { IsSuccess = false, Message = message };
    }

    public static SkillResult Success(string message) {
      return SuccessResult(message);
    }
  }
}

