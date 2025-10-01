namespace EchoesOfTheVoid.Core.Persistence {

  /// <summary>
  /// Defines contract for serializing/deserializing save data.
  /// </summary>
  public interface ISaveSerializer {
    string Serialize<T>(T data);
    T Deserialize<T>(string data);
  }
}