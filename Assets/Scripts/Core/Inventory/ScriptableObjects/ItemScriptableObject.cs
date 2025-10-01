using System.Collections.Generic;
using EchoesOfTheVoid.Core.Inventory.Data;
using UnityEngine;

namespace EchoesOfTheVoid.Core.Inventory.ScriptableObjects {
  [CreateAssetMenu(fileName = "New Item", menuName = "Inventory/Item")]
  public class ItemScriptableObject : ScriptableObject {
    [Header("Basic Info")]
    public string ItemId;
    public string DisplayName;
    [TextArea(2, 4)] public string Description;
    public Sprite Icon;

    [Header("Usage")]
    public ItemType ItemType;
    public bool ConsumableInCombat = true;
    public bool UsableOutsideCombat = true;
    public int MaxStackSize = 99;

    [Header("Effects")]
    public List<ItemEffectData> Effects = new();

    [Header("Audio & Visual")]
    public AudioClip UseSound;
    public GameObject UseEffect;
  }
}
