using System.IO;

namespace EchoesOfTheVoid.Core.Persistence {
  /// <summary>
  /// Standard file system operations.
  /// </summary>
  public class StandardFileManager : IFileManager {
    public void Write(string path, string content) {
      File.WriteAllText(path, content);
    }

    public string Read(string path) {
      return File.ReadAllText(path);
    }

    public bool Exists(string path) {
      return File.Exists(path);
    }

    public void Delete(string path) {
      File.Delete(path);
    }
  }
}
