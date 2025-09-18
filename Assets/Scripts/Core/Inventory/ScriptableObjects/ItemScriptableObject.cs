using System.Collections.Generic;
using UnityEngine;
using EchoesOfTheVoid.Core.Inventory;
using EchoesOfTheVoid.Core.Inventory.Data;

namespace EchoesOfTheVoid.Core.Inventory.ScriptableObjects
{
  [CreateAssetMenu(fileName = "New Item", menuName = "Inventory/Item")]
  public class ItemScriptableObject : ScriptableObject
  {
    [Header("Basic Info")]
    public string itemId;
    public string displayName;
    [TextArea(2, 4)] public string description;
    public Sprite icon;

    [Header("Usage")]
    public ItemType itemType;
    public bool consumableInCombat = true;
    public bool usableOutsideCombat = true;
    public int maxStackSize = 99;

    [Header("Effects")]
    public List<ItemEffectData> effects = new();

    [Header("Audio & Visual")]
    public AudioClip useSound;
    public GameObject useEffect;
  }
}
