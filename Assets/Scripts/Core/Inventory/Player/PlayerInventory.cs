using System.Collections.Generic;
using UnityEngine;

using EchoesOfTheVoid.Core.Inventory.Data;
using EchoesOfTheVoid.Core.Inventory.Database;
using EchoesOfTheVoid.Core.Inventory.ScriptableObjects;
using EchoesOfTheVoid.Core.Inventory.Systems;

namespace EchoesOfTheVoid.Core.Inventory.Player {
  [DisallowMultipleComponent]
  [RequireComponent(typeof(PlayerEquipment))]
  public class PlayerInventory : MonoBehaviour {
    [Header("Inventory Settings")]
    [SerializeField, Min(1)] private int capacity = 30;
    [SerializeField] private List<ItemStackData> startingItems = new();
    [SerializeField] private PlayerEquipment equipment;

    public ItemInventory Inventory { get; private set; }
    public IReadOnlyList<InventorySlot> Slots => Inventory?.Slots;
    public PlayerEquipment Equipment => ResolveEquipment();

    public event System.Action<int, InventorySlot> OnSlotChanged;
    public event System.Action<ItemScriptableObject, int> OnItemAdded;
    public event System.Action<ItemScriptableObject, int> OnItemRemoved;

    private void Awake() {
      InitializeInventory();
      _ = ResolveEquipment();
      equipment?.ApplyStartingEquipment(adjustInventory: true, notify: false);
    }

    private void OnDestroy() {
      DetachSignals();
    }

    private void OnValidate() {
      if (capacity < 1) {
        capacity = 1;
      }

      if (startingItems == null) {
        return;
      }

      foreach (ItemStackData stack in startingItems) {
        if (stack == null) {
          continue;
        }

        if (stack.Quantity < 0) {
          stack.Quantity = 0;
        }
      }
    }

    public bool AddItem(ItemScriptableObject item, int quantity = 1) {
      return Inventory != null && Inventory.AddItem(item, quantity);
    }

    public bool AddItem(string itemId, int quantity = 1, ItemDatabase database = null) {
      return Inventory != null && Inventory.AddItem(itemId, quantity, database: database);
    }

    public bool RemoveItem(ItemScriptableObject item, int quantity = 1) {
      return Inventory != null && Inventory.RemoveItem(item, quantity);
    }

    public bool RemoveItem(string itemId, int quantity = 1) {
      return Inventory != null && Inventory.RemoveItem(itemId, quantity);
    }

    public bool HasItem(ItemScriptableObject item, int quantity = 1) {
      return Inventory != null && Inventory.HasItem(item, quantity);
    }

    public bool HasItem(string itemId, int quantity = 1) {
      return Inventory != null && Inventory.HasItem(itemId, quantity);
    }

    public int GetItemCount(ItemScriptableObject item) {
      return Inventory != null ? Inventory.GetItemCount(item) : 0;
    }

    public int GetItemCount(string itemId) {
      return Inventory != null ? Inventory.GetItemCount(itemId) : 0;
    }

    public void Sort(System.Comparison<InventorySlot> comparison = null, bool suppressNotifications = false) {
      Inventory?.Sort(comparison, suppressNotifications);
    }

    public void Clear(bool suppressNotifications = false) {
      Inventory?.Clear(suppressNotifications);
    }

    public IEnumerable<ItemStackData> GetSnapshot() {
      return Inventory != null ? Inventory.ToItemStacks() : System.Array.Empty<ItemStackData>();
    }

    public void ResetToStartingItems(bool notify = false) {
      if (Inventory == null) {
        InitializeInventory();
        return;
      }

      Inventory.Clear(suppressNotifications: !notify);
      Inventory.Load(startingItems, suppressNotifications: !notify);
      equipment?.ApplyStartingEquipment(adjustInventory: true, notify: notify);
    }

    public void Resize(int newCapacity, bool preserveContents = true, bool notify = false) {
      if (newCapacity <= 0) {
        Debug.LogWarning("PlayerInventory.Resize called with invalid capacity.", this);
        return;
      }

      capacity = newCapacity;
      List<ItemStackData> payload = preserveContents && Inventory != null
        ? new List<ItemStackData>(Inventory.ToItemStacks())
        : startingItems;

      DetachSignals();
      Inventory = new ItemInventory(newCapacity);
      AttachSignals();

      if (payload != null) {
        Inventory.Load(payload, suppressNotifications: !notify);
      }
    }

    private PlayerEquipment ResolveEquipment() {
      if (equipment == null) {
        equipment = GetComponent<PlayerEquipment>() ?? GetComponentInParent<PlayerEquipment>();
      }

      return equipment;
    }

    private void InitializeInventory() {
      int resolvedCapacity = capacity > 0 ? capacity : 1;
      DetachSignals();
      Inventory = new ItemInventory(resolvedCapacity);
      AttachSignals();
      Inventory.Load(startingItems, suppressNotifications: true);
    }

    private void AttachSignals() {
      if (Inventory == null) {
        return;
      }

      Inventory.OnSlotChanged += HandleSlotChanged;
      Inventory.OnItemAdded += HandleItemAdded;
      Inventory.OnItemRemoved += HandleItemRemoved;
    }

    private void DetachSignals() {
      if (Inventory == null) {
        return;
      }

      Inventory.OnSlotChanged -= HandleSlotChanged;
      Inventory.OnItemAdded -= HandleItemAdded;
      Inventory.OnItemRemoved -= HandleItemRemoved;
    }

    private void HandleSlotChanged(int slotIndex, InventorySlot slot) {
      OnSlotChanged?.Invoke(slotIndex, slot);
    }

    private void HandleItemAdded(ItemScriptableObject item, int quantity) {
      OnItemAdded?.Invoke(item, quantity);
    }

    private void HandleItemRemoved(ItemScriptableObject item, int quantity) {
      OnItemRemoved?.Invoke(item, quantity);
    }
  }
}



























