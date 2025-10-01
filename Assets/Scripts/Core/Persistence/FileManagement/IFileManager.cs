namespace EchoesOfTheVoid.Core.Persistence {
  /// <summary>
  /// Defines contract for file operations.
  /// </summary>
  public interface IFileManager {
    void Write(string path, string content);
    string Read(string path);
    bool Exists(string path);
    void Delete(string path);
  }
}
