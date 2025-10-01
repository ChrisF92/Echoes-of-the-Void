using UnityEngine;

namespace EchoesOfTheVoid.Core.Persistence {
  /// <summary>
  /// JSON-based serializer using Unity's JsonUtility.
  /// </summary>
  public class JsonSaveSerializer : ISaveSerializer {
    private readonly bool _prettyPrint;

    public JsonSaveSerializer(bool prettyPrint = false) {
      _prettyPrint = prettyPrint;
    }

    public string Serialize<T>(T data) {
      return JsonUtility.ToJson(data, _prettyPrint);
    }

    public T Deserialize<T>(string data) {
      return JsonUtility.FromJson<T>(data);
    }
  }
}
