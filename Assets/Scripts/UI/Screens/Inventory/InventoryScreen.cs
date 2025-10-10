using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

using EchoesOfTheVoid.Core.Combat;
using EchoesOfTheVoid.Core.Inventory;
using EchoesOfTheVoid.Core.Inventory.Player;
using EchoesOfTheVoid.Core.Inventory.ScriptableObjects;
using EchoesOfTheVoid.Core.Inventory.Systems;
using System;
using EchoesOfTheVoid.Core.Inventory.Data;

namespace EchoesOfTheVoid.UI {
  public class InventoryScreen : UIScreen {
    [SerializeField] private PlayerInventory _playerInventory;
    [SerializeField] private float _itemRowHeight = 44f;

    private readonly Dictionary<ItemType, InventoryTab> _tabs = new();
    private PlayerInventory _subscribedInventory;
    private InventoryTab _activeTab;
    private string _lastSelectedInventoryKey = string.Empty;

    private Label _summaryLabel;
    private Label _detailTitle;
    private Label _detailQuantity;
    private Label _detailDescription;
    private Button _closeButton;

    private InventoryItemViewModel? _selectedInventoryItem;

    private bool _eventsBound;
    private bool _suppressSelectionEvents;

    private const string _activeTabClass = "inventory-tab-button--active";

    private sealed class InventoryTab {
      public InventoryTab(ItemType type, Button button, VisualElement root, ListView listView, Label emptyLabel) {
        Type = type;
        Button = button;
        Root = root;
        ListView = listView;
        EmptyLabel = emptyLabel;
        Items = new List<InventoryItemViewModel>();
      }

      public ItemType Type { get; }
      public Button Button { get; }
      public VisualElement Root { get; }
      public ListView ListView { get; }
      public Label EmptyLabel { get; }
      public List<InventoryItemViewModel> Items { get; }

      public Action ButtonHandler { get; set; }
      public Action<IEnumerable<object>> SelectionHandler { get; set; }
    }

    protected override void SetupUI() {
      _summaryLabel = FindElement<Label>("inventory-summary");
      _detailTitle = FindElement<Label>("detail-title");
      _detailQuantity = FindElement<Label>("detail-quantity");
      _detailDescription = FindElement<Label>("detail-description");
      _closeButton = FindElement<Button>("close-button");

      CreateTab(ItemType.Consumable, "tab-button-consumables", "tab-consumables", "tab-list-consumables", "tab-empty-consumables");
      CreateTab(ItemType.Equipment, "tab-button-equipment", "tab-equipment", "tab-list-equipment", "tab-empty-equipment");
      CreateTab(ItemType.KeyItem, "tab-button-keyitems", "tab-keyitems", "tab-list-keyitems", "tab-empty-keyitems");

      foreach (InventoryTab tab in _tabs.Values) {
        if (tab.Root != null) {
          tab.Root.style.display = DisplayStyle.None;
        }

        ConfigureListView(tab);
        UpdateTabEmptyState(tab);
      }

      ActivateTab(ItemType.Consumable);
      ClearDetails();
    }

    protected override void BindEvents() {
      if (_eventsBound) {
        return;
      }

      _eventsBound = true;

      if (_closeButton != null) {
        _closeButton.clicked += HandleCloseClicked;
      }

      foreach (InventoryTab tab in _tabs.Values) {
        if (tab.Button != null) {
          InventoryTab capturedTab = tab;
          Action handler = () => ActivateTab(capturedTab.Type);
          tab.ButtonHandler = handler;
          tab.Button.clicked += handler;
        }

        if (tab.ListView != null) {
          InventoryTab capturedTab = tab;
          Action<IEnumerable<object>> handler = _ => OnTabSelectionChanged(capturedTab);
          tab.SelectionHandler = handler;
          tab.ListView.selectionChanged += handler;
        }
      }
    }

    protected override void OnShow() {
      base.OnShow();
      ResolveInventoryReference();
      RefreshInventory();
    }

    protected override void OnHide() {
      base.OnHide();

      foreach (InventoryTab tab in _tabs.Values) {
        tab.ListView?.ClearSelection();
      }

      ClearDetails();
    }

    private void OnDestroy() {
      if (_closeButton != null) {
        _closeButton.clicked -= HandleCloseClicked;
      }

      foreach (InventoryTab tab in _tabs.Values) {
        if (tab.Button != null && tab.ButtonHandler != null) {
          tab.Button.clicked -= tab.ButtonHandler;
          tab.ButtonHandler = null;
        }

        if (tab.ListView != null && tab.SelectionHandler != null) {
          tab.ListView.selectionChanged -= tab.SelectionHandler;
          tab.SelectionHandler = null;
        }
      }

      UnsubscribeFromInventory();
    }

    private void CreateTab(ItemType type, string buttonName, string rootName, string listName, string emptyLabelName) {
      Button button = FindElement<Button>(buttonName);
      VisualElement root = FindElement<VisualElement>(rootName);
      ListView listView = FindElement<ListView>(listName);
      Label emptyLabel = FindElement<Label>(emptyLabelName);

      var tab = new InventoryTab(type, button, root, listView, emptyLabel);
      _tabs[type] = tab;
    }

    private void ConfigureListView(InventoryTab tab) {
      if (tab?.ListView == null) {
        return;
      }

      ListView listView = tab.ListView;
      listView.itemsSource = tab.Items;
      listView.selectionType = SelectionType.Single;

      if (_itemRowHeight > 0f) {
        listView.fixedItemHeight = _itemRowHeight;
      }

      listView.makeItem = CreateListItem;
      listView.bindItem = (element, index) => {
        if (index < 0 || index >= tab.Items.Count) {
          return;
        }

        InventoryItemViewModel viewModel = tab.Items[index];
        Label nameLabel = element.Q<Label>("item-name");
        if (nameLabel != null) {
          nameLabel.text = viewModel.DisplayName;
        }

        Label quantityLabel = element.Q<Label>("item-quantity");
        if (quantityLabel != null) {
          quantityLabel.text = viewModel.Quantity.ToString();
        }
      };
    }

    private VisualElement CreateListItem() {
      var root = new VisualElement();
      root.AddToClassList("inventory-list-item");

      var nameLabel = new Label { name = "item-name" };
      nameLabel.AddToClassList("inventory-list-item__name");
      root.Add(nameLabel);

      var quantityLabel = new Label { name = "item-quantity" };
      quantityLabel.AddToClassList("inventory-list-item__quantity");
      root.Add(quantityLabel);

      return root;
    }

    private void ActivateTab(ItemType type) {
      if (!_tabs.TryGetValue(type, out InventoryTab tab)) {
        Debug.LogWarning($"InventoryScreen missing tab for {type}.");
        return;
      }

      if (_activeTab == tab) {
        ApplySelectionForTab(tab, selectFirstIfNone: false);
        return;
      }

      foreach (InventoryTab entry in _tabs.Values) {
        bool isActive = entry == tab;

        if (entry.Root != null) {
          entry.Root.style.display = isActive ? DisplayStyle.Flex : DisplayStyle.None;
        }

        if (entry.Button != null) {
          entry.Button.RemoveFromClassList(_activeTabClass);
          if (isActive) {
            entry.Button.AddToClassList(_activeTabClass);
          }
        }
      }

      _activeTab = tab;
      ApplySelectionForTab(tab, selectFirstIfNone: true);
    }

    private void ApplySelectionForTab(InventoryTab tab, bool selectFirstIfNone) {
      if (tab == null || tab.ListView == null) {
        return;
      }

      if (tab.Items.Count == 0) {
        _suppressSelectionEvents = true;
        tab.ListView.ClearSelection();
        _suppressSelectionEvents = false;

        if (tab == _activeTab) {
          _selectedInventoryItem = null;
          _lastSelectedInventoryKey = string.Empty;
          ClearDetails();
        }

        return;
      }

      int index = tab.ListView.selectedIndex;

      if (index < 0 || index >= tab.Items.Count) {
        if (!string.IsNullOrEmpty(_lastSelectedInventoryKey)) {
          index = tab.Items.FindIndex(item => item.SelectionKey == _lastSelectedInventoryKey);
        }

        if (index < 0 && selectFirstIfNone && tab.Items.Count > 0) {
          index = 0;
        }
      }

      if (index < 0 || index >= tab.Items.Count) {
        _suppressSelectionEvents = true;
        tab.ListView.ClearSelection();
        _suppressSelectionEvents = false;

        if (tab == _activeTab) {
          _selectedInventoryItem = null;
          _lastSelectedInventoryKey = string.Empty;
          ClearDetails();
        }

        return;
      }

      if (tab.ListView.selectedIndex != index) {
        _suppressSelectionEvents = true;
        tab.ListView.selectedIndex = index;
        _suppressSelectionEvents = false;
      }

      OnTabSelectionChanged(tab);
    }

    private void OnTabSelectionChanged(InventoryTab tab) {
      if (_suppressSelectionEvents || tab?.ListView == null) {
        return;
      }

      int index = tab.ListView.selectedIndex;
      if (index < 0 || index >= tab.Items.Count) {
        if (tab == _activeTab) {
          _selectedInventoryItem = null;
          _lastSelectedInventoryKey = string.Empty;
          ClearDetails();
        }

        return;
      }

      InventoryItemViewModel viewModel = tab.Items[index];
      _selectedInventoryItem = viewModel;
      _lastSelectedInventoryKey = viewModel.SelectionKey;

      ShowInventoryItemDetails(viewModel);
    }

    private void RefreshInventory() {
      foreach (InventoryTab tab in _tabs.Values) {
        tab.Items.Clear();
      }

      ItemInventory inventory = _playerInventory != null ? _playerInventory.Inventory : null;

      if (inventory != null) {
        IReadOnlyList<InventorySlot> slots = inventory.Slots;
        for (int slotIndex = 0; slotIndex < slots.Count; slotIndex++) {
          InventorySlot slot = slots[slotIndex];
          if (slot == null || slot.IsEmpty) {
            continue;
          }

          ItemScriptableObject item = slot.Item;
          if (item == null) {
            continue;
          }

          var viewModel = new InventoryItemViewModel(
            item.ItemId,
            string.IsNullOrEmpty(item.DisplayName) ? item.name : item.DisplayName,
            slot.Quantity,
            item.Description,
            item.ItemType,
            item,
            slotIndex);

          if (_tabs.TryGetValue(item.ItemType, out InventoryTab itemTab)) {
            itemTab.Items.Add(viewModel);
          }
        }

        UpdateSummary(inventory);
      } else {
        UpdateSummary(null);
      }

      foreach (InventoryTab tab in _tabs.Values) {
        tab.ListView?.RefreshItems();

        UpdateTabEmptyState(tab);

        if (tab.ListView != null && tab.Items.Count == 0) {
          tab.ListView.ClearSelection();
        }
      }

      ApplySelectionForTab(_activeTab, selectFirstIfNone: false);
    }

    private void UpdateSummary(ItemInventory inventory) {
      if (_summaryLabel == null) {
        return;
      }

      if (inventory == null) {
        _summaryLabel.text = "0 / 0";
        return;
      }

      int usedSlots = 0;
      IReadOnlyList<InventorySlot> slots = inventory.Slots;
      if (slots != null) {
        for (int i = 0; i < slots.Count; i++) {
          if (!slots[i].IsEmpty) {
            usedSlots++;
          }
        }
      }

      _summaryLabel.text = $"{usedSlots} / {inventory.Capacity}";
    }

    private void UpdateTabEmptyState(InventoryTab tab) {
      if (tab == null || tab.ListView == null || tab.EmptyLabel == null) {
        return;
      }

      bool hasItems = tab.Items.Count > 0;
      tab.ListView.style.display = hasItems ? DisplayStyle.Flex : DisplayStyle.None;
      tab.EmptyLabel.style.display = hasItems ? DisplayStyle.None : DisplayStyle.Flex;
    }

    private void ShowInventoryItemDetails(InventoryItemViewModel viewModel) {
      if (_detailTitle != null) {
        _detailTitle.text = viewModel.DisplayName;
      }

      if (_detailQuantity != null) {
        _detailQuantity.text = $"Quantity: {Mathf.Max(0, viewModel.Quantity)}";
      }

      if (_detailDescription == null) {
        return;
      }

      var builder = new StringBuilder();

      if (!string.IsNullOrWhiteSpace(viewModel.Description)) {
        _ = builder.AppendLine(viewModel.Description.Trim());
      }

      if (viewModel.Item is EquipmentItemScriptableObject equipmentItem) {
        string statSummary = BuildStatSummary(equipmentItem);
        if (!string.IsNullOrWhiteSpace(statSummary)) {
          if (builder.Length > 0) {
            _ = builder.AppendLine();
          }

          _ = builder.AppendLine("Stats:");
          _ = builder.AppendLine(statSummary);
        }
      }

      string text = builder.Length > 0 ? builder.ToString().TrimEnd() : "No description available.";
      _detailDescription.text = text;
    }

    private void ClearDetails() {
      if (_detailTitle != null) {
        _detailTitle.text = "Select an item";
      }

      if (_detailQuantity != null) {
        _detailQuantity.text = "Quantity: 0";
      }

      if (_detailDescription != null) {
        _detailDescription.text = string.Empty;
      }
    }

    private void HandleCloseClicked() {
      NavigationManager.Instance?.NavigateBack();
    }

    private void ResolveInventoryReference() {
      if (_playerInventory == null) {
        _playerInventory = FindFirstObjectByType<PlayerInventory>();
      }

      if (_playerInventory != _subscribedInventory) {
        UnsubscribeFromInventory();
        SubscribeToInventory(_playerInventory);
      }
    }

    private void SubscribeToInventory(PlayerInventory inventory) {
      if (inventory == null) {
        return;
      }

      _subscribedInventory = inventory;
      _subscribedInventory.OnSlotChanged += HandleInventoryUpdated;
      _subscribedInventory.OnItemAdded += HandleInventoryItemChanged;
      _subscribedInventory.OnItemRemoved += HandleInventoryItemChanged;
    }

    private void UnsubscribeFromInventory() {
      if (_subscribedInventory == null) {
        return;
      }

      _subscribedInventory.OnSlotChanged -= HandleInventoryUpdated;
      _subscribedInventory.OnItemAdded -= HandleInventoryItemChanged;
      _subscribedInventory.OnItemRemoved -= HandleInventoryItemChanged;
      _subscribedInventory = null;
    }

    private void HandleInventoryUpdated(int index, InventorySlot slot) {
      RefreshInventory();
    }

    private void HandleInventoryItemChanged(ItemScriptableObject item, int amount) {
      RefreshInventory();
    }

    private string BuildStatSummary(EquipmentItemScriptableObject item) {
      Dictionary<StatType, StatAggregate> stats = BuildStatDictionary(item);
      if (stats.Count == 0) {
        return "  No modifiers.";
      }

      var builder = new StringBuilder();
      foreach (StatType statType in System.Enum.GetValues(typeof(StatType))) {
        if (!stats.TryGetValue(statType, out StatAggregate aggregate)) {
          continue;
        }

        _ = builder.Append("  ");
        _ = builder.Append(statType);
        _ = builder.Append(": ");
        _ = builder.Append(FormatAggregate(aggregate));
        _ = builder.AppendLine();
      }

      return builder.ToString().TrimEnd();
    }

    private static Dictionary<StatType, StatAggregate> BuildStatDictionary(IEnumerable<EquipmentItemScriptableObject> items) {
      var result = new Dictionary<StatType, StatAggregate>();

      if (items == null) {
        return result;
      }

      foreach (EquipmentItemScriptableObject item in items) {
        if (item?.StatModifiers == null) {
          continue;
        }

        foreach (EquipmentStatModifier modifier in item.StatModifiers) {
          if (!result.TryGetValue(modifier.Stat, out StatAggregate aggregate)) {
            aggregate = default;
          }

          aggregate.Flat += modifier.FlatBonus;
          aggregate.Percent += modifier.PercentBonus;
          result[modifier.Stat] = aggregate;
        }
      }

      return result;
    }

    private static Dictionary<StatType, StatAggregate> BuildStatDictionary(EquipmentItemScriptableObject item) {
      if (item == null) {
        return new Dictionary<StatType, StatAggregate>();
      }

      return BuildStatDictionary(new[] { item });
    }

    private static string FormatAggregate(StatAggregate aggregate) {
      var parts = new List<string>();

      if (aggregate.Flat != 0) {
        parts.Add(FormatSignedInt(aggregate.Flat));
      }

      if (Mathf.Abs(aggregate.Percent) > 0.0001f) {
        parts.Add(FormatPercent(aggregate.Percent));
      }

      if (parts.Count == 0) {
        parts.Add("+0");
      }

      return string.Join(", ", parts);
    }

    private static string FormatSignedInt(int value) {
      return value >= 0 ? $"+{value}" : value.ToString();
    }

    private static string FormatPercent(float value) {
      float percent = value * 100f;
      string format = Mathf.Abs(percent - Mathf.Round(percent)) < 0.001f ? "0" : "0.##";
      string formatted = percent.ToString(format, CultureInfo.InvariantCulture);
      return (value >= 0f ? "+" : string.Empty) + formatted + "%";
    }

    private readonly struct InventoryItemViewModel {
      public InventoryItemViewModel(string id, string displayName, int quantity, string description, ItemType itemType, ItemScriptableObject item, int slotIndex) {
        Id = id ?? string.Empty;
        Item = item;
        ItemType = itemType;
        Quantity = quantity;
        Description = description ?? string.Empty;
        SlotIndex = slotIndex;
        DisplayName = !string.IsNullOrWhiteSpace(displayName)
          ? displayName
          : item != null
            ? (!string.IsNullOrWhiteSpace(item.DisplayName) ? item.DisplayName : item.name)
            : string.Empty;
        SelectionKey = string.Concat(Id, ":", SlotIndex.ToString(CultureInfo.InvariantCulture));
      }

      public string Id { get; }
      public string DisplayName { get; }
      public int Quantity { get; }
      public string Description { get; }
      public ItemType ItemType { get; }
      public ItemScriptableObject Item { get; }
      public int SlotIndex { get; }
      public string SelectionKey { get; }
    }

    private struct StatAggregate {
      public int Flat;
      public float Percent;
    }
  }
}
