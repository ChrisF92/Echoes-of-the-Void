namespace EchoesOfTheVoid.Core.Inventory.Results
{
  public class ItemResult
  {
    public bool IsSuccess { get; private set; }
    public string Message { get; private set; }

    public static ItemResult Success(string message)
    {
      return new ItemResult { IsSuccess = true, Message = message };
    }

    public static ItemResult Failed(string message)
    {
      return new ItemResult { IsSuccess = false, Message = message };
    }
  }
}
