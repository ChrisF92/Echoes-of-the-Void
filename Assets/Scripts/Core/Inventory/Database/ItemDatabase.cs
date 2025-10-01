using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using EchoesOfTheVoid.Core.Inventory.ScriptableObjects;

namespace EchoesOfTheVoid.Core.Inventory.Database {
  public class ItemDatabase : MonoBehaviour {
    public static ItemDatabase Instance { get; private set; }

    [SerializeField] private List<ItemScriptableObject> _allItems = new();

    private void Awake() {
      if (Instance == null) {
        Instance = this;
        DontDestroyOnLoad(gameObject);
      } else {
        Destroy(gameObject);
      }
    }

    public IEnumerable<ItemScriptableObject> GetItems() => _allItems;

    public ItemScriptableObject GetItem(string itemId) {
      return _allItems.FirstOrDefault(i => i.ItemId == itemId);
    }
  }
}
