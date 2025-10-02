using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

using EchoesOfTheVoid.Core.Inventory.Data;
using EchoesOfTheVoid.Core.Inventory.Player;
using EchoesOfTheVoid.Core.Inventory.ScriptableObjects;
using EchoesOfTheVoid.Core.Inventory.Systems;
using EchoesOfTheVoid.Core.Roster;
using EchoesOfTheVoid.Core.Roster.Data;
using EchoesOfTheVoid.Core.Inventory;

namespace EchoesOfTheVoid.UI.Modals {
  public class EchoEquipmentModal : UIModal {
    [SerializeField] private PlayerRosterService _rosterService;
    [SerializeField] private PlayerInventory _playerInventory;

    private ListView _slotListView;
    private ListView _inventoryListView;
    private Label _slotNameLabel;
    private Label _slotItemNameLabel;
    private Label _slotDescriptionLabel;
    private VisualElement _slotStatsRoot;
    private Label _gearNameLabel;
    private Label _gearDescriptionLabel;
    private VisualElement _gearStatsRoot;
    private Label _errorLabel;
    private Button _equipButton;
    private Button _unequipButton;
    private Button _closeButton;

    private readonly List<SlotViewModel> _slotItems = new();
    private readonly List<InventoryItemViewModel> _inventoryItems = new();

    private PlayerEchoData _currentEcho;
    private EquipmentSet _workingSet;
    private readonly List<EquippedItemData> _workingSnapshot = new();

    private int _selectedSlotIndex = -1;
    private int _selectedInventoryIndex = -1;

    public event Action<PlayerEchoData> OnEquipmentApplied;

    public void ConfigureServices(PlayerRosterService rosterService, PlayerInventory playerInventory) {
      _rosterService = rosterService;
      _playerInventory = playerInventory;
    }

    public void ShowForEcho(PlayerEchoData echo) {
      if (echo == null) {
        return;
      }

      EnsureServices();
      _currentEcho = echo;

      _selectedSlotIndex = -1;
      _selectedInventoryIndex = -1;
      BuildWorkingSet();
      RefreshSlotList();
      _slotListView?.ClearSelection();
      RefreshInventoryList();
      _inventoryListView?.ClearSelection();
      UpdateSlotDetailSection();
      UpdateGearDetailSection();
      UpdateActionButtons();

      if (_errorLabel != null) {
        _errorLabel.text = string.Empty;
      }

      Show();
    }

    protected override void SetupUI() {
      _slotListView = FindElement<ListView>("slot-list");
      _inventoryListView = FindElement<ListView>("inventory-list");
      _slotNameLabel = FindLabel("slot-name");
      _slotItemNameLabel = FindLabel("slot-item-name");
      _slotDescriptionLabel = FindLabel("slot-description");
      _slotStatsRoot = FindElement<VisualElement>("slot-stats");
      _gearNameLabel = FindLabel("gear-name");
      _gearDescriptionLabel = FindLabel("gear-description");
      _gearStatsRoot = FindElement<VisualElement>("gear-stats");
      _errorLabel = FindLabel("error-label");
      _equipButton = FindButton("equip-button");
      _unequipButton = FindButton("unequip-button");
      _closeButton = FindButton("close-button");
      ConfigureSlotList();
      ConfigureInventoryList();
      UpdateActionButtons();
    }

    protected override void BindEvents() {
      _equipButton?.RegisterCallback<ClickEvent>(_ => OnEquipClicked());
      _unequipButton?.RegisterCallback<ClickEvent>(_ => OnUnequipClicked());
      _closeButton?.RegisterCallback<ClickEvent>(_ => Hide());
    }

    private void EnsureServices() {
      if (_rosterService == null) {
        _rosterService = FindFirstObjectByType<PlayerRosterService>();
      }

      if (_playerInventory == null) {
        _playerInventory = FindFirstObjectByType<PlayerInventory>();
      }
    }

    private void ConfigureSlotList() {
      if (_slotListView == null) {
        return;
      }

      _slotListView.itemsSource = _slotItems;
      _slotListView.selectionType = SelectionType.Single;
      _slotListView.virtualizationMethod = CollectionVirtualizationMethod.FixedHeight;
      _slotListView.fixedItemHeight = 64f;
      _slotListView.makeItem = () => {
        var root = new VisualElement { name = "slot-item" };
        root.AddToClassList("equipment-slot-item");

        var slotLabel = new Label { name = "slot-item__name" };
        slotLabel.AddToClassList("equipment-slot-item__name");
        root.Add(slotLabel);

        var itemLabel = new Label { name = "slot-item__item" };
        itemLabel.AddToClassList("equipment-slot-item__item");
        root.Add(itemLabel);

        return root;
      };

      _slotListView.bindItem = (element, index) => {
        if (index < 0 || index >= _slotItems.Count) {
          return;
        }

        SlotViewModel viewModel = _slotItems[index];
        Label slotNameLabel = element.Q<Label>("slot-item__name");
        if (slotNameLabel != null) {
          slotNameLabel.text = viewModel.SlotName;
        }

        Label slotItemLabel = element.Q<Label>("slot-item__item");
        if (slotItemLabel != null) {
          slotItemLabel.text = viewModel.ItemLabel;
        }

        element.EnableInClassList("equipment-slot-item--filled", viewModel.Item != null);
        element.EnableInClassList("equipment-slot-item--empty", viewModel.Item == null);
      };

      _slotListView.selectionChanged += OnSlotSelectionChanged;
    }

    private void ConfigureInventoryList() {
      if (_inventoryListView == null) {
        return;
      }

      _inventoryListView.itemsSource = _inventoryItems;
      _inventoryListView.selectionType = SelectionType.Single;
      _inventoryListView.makeItem = () => {
        var root = new VisualElement { name = "inventory-item" };
        root.AddToClassList("equipment-inventory-item");

        var nameLabel = new Label { name = "inventory-item__name" };
        nameLabel.AddToClassList("equipment-inventory-item__name");
        root.Add(nameLabel);

        var countLabel = new Label { name = "inventory-item__count" };
        countLabel.AddToClassList("equipment-inventory-item__count");
        root.Add(countLabel);

        return root;
      };

      _inventoryListView.bindItem = (element, index) => {
        if (index < 0 || index >= _inventoryItems.Count) {
          return;
        }

        InventoryItemViewModel viewModel = _inventoryItems[index];
        Label nameLabel = element.Q<Label>("inventory-item__name");
        if (nameLabel != null)
        {
          nameLabel.text = viewModel.DisplayName;
        }

        string countText = viewModel.AvailableCount > 0
          ? $"x{viewModel.AvailableCount}"
          : "In Use";
        Label countLabel = element.Q<Label>("inventory-item__count");
        if (countLabel != null)
        {
          countLabel.text = countText;
        }
      };

      _inventoryListView.selectionChanged += OnInventorySelectionChanged;
    }

    private void BuildWorkingSet() {
      _workingSet = new EquipmentSet(PlayerRosterService.EquipmentSlotLayout);
      _workingSnapshot.Clear();

      if (_currentEcho?.EquipmentLoadout != null) {
        foreach (EquippedItemData entry in _currentEcho.EquipmentLoadout) {
          if (entry?.Item == null) {
            continue;
          }

          EquipmentSlotType slotType = entry.Slot;
          if (!_workingSet.HasSlot(slotType)) {
            slotType = entry.Item.Slot;
          }

          _ = _workingSet.TryEquip(entry.Item, slotType, out _);
        }
      }

      SyncSnapshotFromSet();
    }

    private void SyncSnapshotFromSet() {
      _workingSnapshot.Clear();
      foreach (EquipmentSlot slot in _workingSet.Slots) {
        if (slot.Item == null) {
          continue;
        }

        _workingSnapshot.Add(new EquippedItemData {
          Slot = slot.SlotType,
          Item = slot.Item
        });
      }
    }

    private void RefreshSlotList() {
      _slotItems.Clear();

      IReadOnlyList<EquipmentSlotType> layout = PlayerRosterService.EquipmentSlotLayout;
      for (int i = 0; i < layout.Count; i++) {
        EquipmentSlotType slotType = layout[i];
        EquipmentItemScriptableObject item = FindEquippedItem(slotType);
        _slotItems.Add(new SlotViewModel(slotType, slotType.ToString(), item));
      }

      _slotListView?.RefreshItems();
      if (_slotListView != null && _selectedSlotIndex >= 0 && _selectedSlotIndex < _slotItems.Count) {
        _slotListView.selectedIndex = _selectedSlotIndex;
      }
    }

    private void RefreshInventoryList() {
      _inventoryItems.Clear();
      Dictionary<EquipmentItemScriptableObject, int> available = BuildAvailableItemCounts();

      foreach (KeyValuePair<EquipmentItemScriptableObject, int> entry in available.OrderBy(pair => pair.Key.DisplayName)) {
        _inventoryItems.Add(new InventoryItemViewModel(entry.Key, entry.Value));
      }

      _inventoryListView?.RefreshItems();
      if (_inventoryListView != null && _selectedInventoryIndex >= 0 && _selectedInventoryIndex < _inventoryItems.Count) {
        _inventoryListView.selectedIndex = _selectedInventoryIndex;
      }
    }

    private Dictionary<EquipmentItemScriptableObject, int> BuildAvailableItemCounts() {
      var results = new Dictionary<EquipmentItemScriptableObject, int>();

      if (_playerInventory?.Slots != null) {
        foreach (InventorySlot slot in _playerInventory.Slots) {
          if (slot?.Item is EquipmentItemScriptableObject equipment && slot.Quantity > 0) {
            results[equipment] = slot.Quantity;
          }
        }
      }

      foreach (EquippedItemData entry in _workingSnapshot) {
        if (entry?.Item == null) {
          continue;
        }

        if (!results.ContainsKey(entry.Item)) {
          results[entry.Item] = 0;
        }
      }

      return results;
    }

    private EquipmentItemScriptableObject FindEquippedItem(EquipmentSlotType slotType) {
      foreach (EquippedItemData entry in _workingSnapshot) {
        if (entry != null && entry.Slot == slotType) {
          return entry.Item;
        }
      }

      return null;
    }

    private void OnSlotSelectionChanged(IEnumerable<object> _) {
      _selectedSlotIndex = _slotListView?.selectedIndex ?? -1;
      UpdateSlotDetailSection();
      UpdateGearDetailSection();
      UpdateActionButtons();
    }

    private void OnInventorySelectionChanged(IEnumerable<object> _) {
      _selectedInventoryIndex = _inventoryListView?.selectedIndex ?? -1;

      if (_selectedInventoryIndex >= 0 && _selectedInventoryIndex < _inventoryItems.Count) {
        InventoryItemViewModel viewModel = _inventoryItems[_selectedInventoryIndex];
        EquipmentItemScriptableObject selectedItem = viewModel.Item;
        int slotIndex = FindSlotIndexForItem(selectedItem);
        if (slotIndex < 0 && selectedItem != null) {
          slotIndex = FindSlotIndex(ResolvePreferredSlot(selectedItem));
        }

        if (slotIndex >= 0) {
          _selectedSlotIndex = slotIndex;
          if (_slotListView != null) {
            _slotListView.selectedIndex = slotIndex;
          } else {
            UpdateSlotDetailSection();
          }
        }
      }

      UpdateSlotDetailSection();
      UpdateGearDetailSection();
      UpdateActionButtons();
    }

    private void UpdateSlotDetailSection() {
      if (_slotNameLabel == null || _slotItemNameLabel == null || _slotDescriptionLabel == null) {
        return;
      }

      if (_selectedSlotIndex < 0 || _selectedSlotIndex >= _slotItems.Count) {
        _slotNameLabel.text = "Selected Slot";
        _slotItemNameLabel.text = string.Empty;
        _slotDescriptionLabel.text = "Select a slot to see its equipment.";
        PopulateStatPreview(null, _slotStatsRoot);
        return;
      }

      SlotViewModel slot = _slotItems[_selectedSlotIndex];
      _slotNameLabel.text = slot.SlotName;
      _slotItemNameLabel.text = slot.ItemLabel;
      if (slot.HasItem) {
        _slotDescriptionLabel.text = !string.IsNullOrWhiteSpace(slot.Item.Description) ? slot.Item.Description : "No description available.";
      } else {
        _slotDescriptionLabel.text = "No equipment assigned.";
      }

      PopulateStatPreview(slot.Item, _slotStatsRoot);
    }

    private void UpdateGearDetailSection() {
      if (_gearNameLabel == null || _gearDescriptionLabel == null) {
        return;
      }

      if (_selectedInventoryIndex < 0 || _selectedInventoryIndex >= _inventoryItems.Count) {
        _gearNameLabel.text = string.Empty;
        _gearDescriptionLabel.text = string.Empty;
        PopulateStatPreview(null, _gearStatsRoot);
        return;
      }

      InventoryItemViewModel viewModel = _inventoryItems[_selectedInventoryIndex];
      if (viewModel.Item != null) {
        _gearNameLabel.text = viewModel.DisplayName;
        _gearDescriptionLabel.text = !string.IsNullOrWhiteSpace(viewModel.Item.Description) ? viewModel.Item.Description : "No description available.";
      } else {
        _gearNameLabel.text = viewModel.DisplayName;
        _gearDescriptionLabel.text = string.Empty;
      }

      PopulateStatPreview(viewModel.Item, _gearStatsRoot);
    }

    private void PopulateStatPreview(EquipmentItemScriptableObject item, VisualElement targetRoot) {
      if (targetRoot == null) {
        return;
      }

      targetRoot.Clear();

      if (item == null) {
        return;
      }

      if (item.StatModifiers == null || item.StatModifiers.Count == 0) {
        var placeholder = new Label("No stat modifiers.");
        placeholder.AddToClassList("equipment-detail__empty");
        targetRoot.Add(placeholder);
        return;
      }

      foreach (EquipmentStatModifier modifier in item.StatModifiers) {
        var row = new VisualElement();
        row.AddToClassList("equipment-detail__stat-row");

        row.Add(new Label(modifier.Stat.ToString()));
        var valueLabel = new Label(FormatModifier(modifier));
        valueLabel.AddToClassList("equipment-detail__stat-value");
        row.Add(valueLabel);
        targetRoot.Add(row);
      }
    }

    private bool CommitWorkingSet(EquipmentSlotType slotToSelect, EquipmentItemScriptableObject inventoryItemToHighlight) {
      if (_currentEcho == null || _rosterService == null) {
        RefreshFromRosterView(slotToSelect, inventoryItemToHighlight);
        return true;
      }

      if (!_rosterService.TryApplyEquipment(_currentEcho.InstanceId, _workingSnapshot, out string errorMessage)) {
        SetError(errorMessage);
        RefreshFromRosterView(slotToSelect, inventoryItemToHighlight);
        return false;
      }

      SetError(string.Empty);
      OnEquipmentApplied?.Invoke(_currentEcho);
      RefreshFromRosterView(slotToSelect, inventoryItemToHighlight);
      return true;
    }

    private void RefreshFromRosterView(EquipmentSlotType slotToSelect, EquipmentItemScriptableObject inventoryItemToHighlight) {
      BuildWorkingSet();
      RefreshSlotList();
      _selectedSlotIndex = FindSlotIndex(slotToSelect);
      if (_slotListView != null) {
        _slotListView.selectedIndex = _selectedSlotIndex;
      }

      RefreshInventoryList();
      _selectedInventoryIndex = FindInventoryIndex(inventoryItemToHighlight);
      if (_inventoryListView != null) {
        _inventoryListView.selectedIndex = _selectedInventoryIndex;
      }

      UpdateSlotDetailSection();
      UpdateGearDetailSection();
      UpdateActionButtons();
    }

    private int FindSlotIndexForItem(EquipmentItemScriptableObject item) {
      if (item == null) {
        return -1;
      }

      for (int i = 0; i < _slotItems.Count; i++) {
        if (_slotItems[i].Item == item) {
          return i;
        }
      }

      return -1;
    }

    private EquipmentSlotType ResolvePreferredSlot(EquipmentItemScriptableObject item) {
      if (item == null) {
        return EquipmentSlotType.MainHand;
      }

      if (item.OccupiesBothHands) {
        return EquipmentSlotType.MainHand;
      }

      return item.Slot;
    }

    private int FindSlotIndex(EquipmentSlotType slotType) {
      for (int i = 0; i < _slotItems.Count; i++) {
        if (_slotItems[i].SlotType == slotType) {
          return i;
        }
      }

      return -1;
    }

    private int FindInventoryIndex(EquipmentItemScriptableObject item) {
      if (item == null) {
        return -1;
      }

      for (int i = 0; i < _inventoryItems.Count; i++) {
        if (_inventoryItems[i].Item == item) {
          return i;
        }
      }

      return -1;
    }

    private void OnEquipClicked() {
      SetError(string.Empty);

      if (_selectedSlotIndex < 0 || _selectedSlotIndex >= _slotItems.Count) {
        SetError("Select a slot.");
        return;
      }

      if (_selectedInventoryIndex < 0 || _selectedInventoryIndex >= _inventoryItems.Count) {
        SetError("Select an item to equip.");
        return;
      }

      SlotViewModel slot = _slotItems[_selectedSlotIndex];
      InventoryItemViewModel itemView = _inventoryItems[_selectedInventoryIndex];
      if (itemView.Item == null) {
        SetError("Item data missing.");
        return;
      }

      if (!_workingSet.TryEquip(itemView.Item, slot.SlotType, out _)) {
        SetError("Cannot equip item in this slot.");
        return;
      }

      SyncSnapshotFromSet();

      _inventoryListView?.ClearSelection();
      _selectedInventoryIndex = -1;

      if (!CommitWorkingSet(slot.SlotType, null)) {
        return;
      }
    }

    private void OnUnequipClicked() {
      SetError(string.Empty);

      if (_selectedSlotIndex < 0 || _selectedSlotIndex >= _slotItems.Count) {
        SetError("Select a slot.");
        return;
      }

      SlotViewModel slot = _slotItems[_selectedSlotIndex];
      if (!_workingSet.TryUnequip(slot.SlotType, out EquipmentDisplacement displacement) || displacement.Item == null) {
        SetError("Nothing to unequip.");
        return;
      }

      SyncSnapshotFromSet();

      if (!CommitWorkingSet(slot.SlotType, displacement.Item)) {
        return;
      }
    }


    private void SetError(string message) {
      if (_errorLabel != null) {
        _errorLabel.text = message ?? string.Empty;
      }
    }


    private static string FormatModifier(EquipmentStatModifier modifier) {
      string flat = modifier.FlatBonus != 0 ? (modifier.FlatBonus > 0 ? $"+{modifier.FlatBonus}" : modifier.FlatBonus.ToString()) : string.Empty;
      float percentValue = modifier.PercentBonus * 100f;
      string percent = Math.Abs(percentValue) > 0.001f ? (percentValue > 0 ? $"+{percentValue:0.#}%" : $"{percentValue:0.#}%") : string.Empty;

      if (!string.IsNullOrEmpty(flat) && !string.IsNullOrEmpty(percent)) {
        return $"{flat} {percent}";
      }

      if (!string.IsNullOrEmpty(flat)) {
        return flat;
      }

      if (!string.IsNullOrEmpty(percent)) {
        return percent;
      }

      return "0";
    }

    private void UpdateActionButtons() {
      bool slotSelected = _selectedSlotIndex >= 0 && _selectedSlotIndex < _slotItems.Count;
      bool itemSelected = _selectedInventoryIndex >= 0 && _selectedInventoryIndex < _inventoryItems.Count;

      _equipButton?.SetEnabled(slotSelected && itemSelected);
      SlotViewModel slot = slotSelected ? _slotItems[_selectedSlotIndex] : default;
      _unequipButton?.SetEnabled(slotSelected && slot.HasItem);
    }

    private readonly struct SlotViewModel {
      public SlotViewModel(EquipmentSlotType slotType, string slotName, EquipmentItemScriptableObject item) {
        SlotType = slotType;
        SlotName = slotName;
        Item = item;
      }

      public EquipmentSlotType SlotType { get; }
      public string SlotName { get; }
      public EquipmentItemScriptableObject Item { get; }
      public string ItemLabel => Item != null ? (!string.IsNullOrWhiteSpace(Item.DisplayName) ? Item.DisplayName : Item.name) : "Nothing equipped";
      public bool HasItem => Item != null;
    }

    private readonly struct InventoryItemViewModel {
      public InventoryItemViewModel(EquipmentItemScriptableObject item, int availableCount) {
        Item = item;
        AvailableCount = availableCount;
        DisplayName = item != null
          ? (!string.IsNullOrWhiteSpace(item.DisplayName) ? item.DisplayName : item.name)
          : string.Empty;
      }

      public EquipmentItemScriptableObject Item { get; }
      public int AvailableCount { get; }
      public string DisplayName { get; }
    }
  }
}













