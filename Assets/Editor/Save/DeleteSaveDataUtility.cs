using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace EchoesOfTheVoid.Editor.Save {
  public static class DeleteSaveDataUtility {
    private const string _defaultSearchPattern = "*.dat";

    [MenuItem("Echoes/Delete Save Data", priority = 100)]
    public static void DeleteSaveData() {
      string saveRoot = Application.persistentDataPath;
      if (string.IsNullOrEmpty(saveRoot)) {
        EditorUtility.DisplayDialog("Delete Save Data", "Unable to resolve persistent data path.", "OK");
        return;
      }

      string[] candidates;
      try {
        candidates = Directory.Exists(saveRoot)
          ? Directory.GetFiles(saveRoot, _defaultSearchPattern, SearchOption.TopDirectoryOnly)
          : Array.Empty<string>();
      } catch (Exception ex) {
        Debug.LogError($"[DeleteSaveDataUtility] Failed to scan save directory: {ex.Message}");
        EditorUtility.DisplayDialog("Delete Save Data", "Failed to scan save directory. Check the console for details.", "OK");
        return;
      }

      if (candidates.Length == 0) {
        EditorUtility.DisplayDialog("Delete Save Data", "No save files were found to delete.", "OK");
        return;
      }

      int deletedCount = 0;
      foreach (string file in candidates) {
        try {
          File.Delete(file);
          deletedCount++;
        } catch (Exception ex) {
          Debug.LogError($"[DeleteSaveDataUtility] Failed to delete '{file}': {ex.Message}");
        }
      }

      string message = deletedCount > 0
        ? $"Deleted {deletedCount} save file{(deletedCount == 1 ? string.Empty : "s")} from:\n{saveRoot}"
        : "No save files were deleted. Check the console for details.";

      EditorUtility.DisplayDialog("Delete Save Data", message, "OK");
    }
  }
}
