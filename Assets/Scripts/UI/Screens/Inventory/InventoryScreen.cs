using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

using EchoesOfTheVoid.Core.Combat.Entities;
using EchoesOfTheVoid.Core.Inventory;
using EchoesOfTheVoid.Core.Inventory.Player;
using EchoesOfTheVoid.Core.Inventory.ScriptableObjects;
using EchoesOfTheVoid.Core.Inventory.Systems;
using EchoesOfTheVoid.Core.Combat;
using EchoesOfTheVoid.Core.Inventory.Results;
using EchoesOfTheVoid.Core.Inventory.Data;

namespace EchoesOfTheVoid.UI {
  public class InventoryScreen : UIScreen {
    [SerializeField] private PlayerInventory _playerInventory;
    [SerializeField] private float _itemRowHeight = 44f;

    private readonly Dictionary<ItemType, InventoryTab> _tabs = new();
    private PlayerInventory _subscribedInventory;
    private PlayerEquipment _playerEquipment;
    private PlayerEquipment _activeEquipment;
    private PlayerEquipment _subscribedEquipment;
    private InventoryTab _activeTab;
    private string _lastSelectedInventoryKey = string.Empty;
    private string _activeTeamMemberId = string.Empty;
    private string _activeTeamMemberName = string.Empty;

    private readonly List<LoadoutSlotViewModel> _loadoutItems = new();
    private readonly List<TeamMemberEntry> _teamMembers = new();

    private Label _summaryLabel;
    private Label _detailTitle;
    private Label _detailQuantity;
    private Label _detailDescription;
    private Button _closeButton;
    private ListView _teamListView;
    private ListView _loadoutListView;
    private Label _teamEmptyLabel;
    private Label _loadoutEmptyLabel;
    private Label _loadoutTitle;
    private Button _equipButton;
    private Button _unequipButton;
    private Label _comparisonLabel;

    private InventoryItemViewModel? _selectedInventoryItem;
    private LoadoutSlotViewModel? _selectedLoadoutSlot;
    private EquipmentSlotType? _selectedLoadoutSlotType;
    private int _selectedLoadoutIndex = -1;

    private Action<IEnumerable<object>> _teamSelectionHandler;
    private Action<IEnumerable<object>> _loadoutSelectionHandler;

    private bool _eventsBound;
    private bool _suppressSelectionEvents;

    private const string _activeTabClass = "inventory-tab-button--active";
    private const string _positiveDeltaColor = "#6DFE9A";
    private const string _negativeDeltaColor = "#FF6D6D";

    private static readonly EquipmentSlotType[] DefaultLoadoutOrder =
    {
    EquipmentSlotType.Head,
    EquipmentSlotType.Chest,
    EquipmentSlotType.Legs,
    EquipmentSlotType.MainHand,
    EquipmentSlotType.OffHand,
    EquipmentSlotType.Accessory,
    EquipmentSlotType.Relic
  };

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

      _teamListView = FindElement<ListView>("team-list");
      _teamEmptyLabel = FindElement<Label>("team-empty");
      _loadoutTitle = FindElement<Label>("loadout-title");
      _loadoutListView = FindElement<ListView>("loadout-list");
      _loadoutEmptyLabel = FindElement<Label>("loadout-empty");
      _equipButton = FindElement<Button>("equip-button");
      _unequipButton = FindElement<Button>("unequip-button");
      _comparisonLabel = FindElement<Label>("detail-compare");
      if (_comparisonLabel != null) {
        _comparisonLabel.enableRichText = true;
      }

      ConfigureTeamList();
      ConfigureLoadoutList();

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

      if (_equipButton != null) {
        _equipButton.clicked += HandleEquipButtonClicked;
      }

      if (_unequipButton != null) {
        _unequipButton.clicked += HandleUnequipButtonClicked;
      }

      UpdateTeamPanelState();
      UpdateLoadoutPanelState();
      UpdateActionButtons();
      UpdateComparisonLabel();
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
      RefreshTeamMembers();
      RefreshInventory();
    }

    protected override void OnHide() {
      base.OnHide();

      foreach (InventoryTab tab in _tabs.Values) {
        tab.ListView?.ClearSelection();
      }

      ClearLoadoutSelection();
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

      if (_equipButton != null) {
        _equipButton.clicked -= HandleEquipButtonClicked;
      }

      if (_unequipButton != null) {
        _unequipButton.clicked -= HandleUnequipButtonClicked;
      }

      UnsubscribeFromInventory();
      UnsubscribeFromEquipment();

      if (_teamListView != null && _teamSelectionHandler != null) {
        _teamListView.selectionChanged -= _teamSelectionHandler;
        _teamSelectionHandler = null;
      }

      if (_loadoutListView != null && _loadoutSelectionHandler != null) {
        _loadoutListView.selectionChanged -= _loadoutSelectionHandler;
        _loadoutSelectionHandler = null;
      }
    }

    private void CreateTab(ItemType type, string buttonName, string rootName, string listName, string emptyLabelName) {
      Button button = FindElement<Button>(buttonName);
      VisualElement root = FindElement<VisualElement>(rootName);
      ListView listView = root?.Q<ListView>(listName);
      Label emptyLabel = root?.Q<Label>(emptyLabelName);

      if (root == null) {
        Debug.LogWarning($"InventoryScreen could not locate root element '{rootName}' for tab {type}.");
      }

      var tab = new InventoryTab(type, button, root, listView, emptyLabel);
      _tabs[type] = tab;
    }

    private void ConfigureListView(InventoryTab tab) {
      ListView listView = tab.ListView;
      if (listView == null) {
        return;
      }

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
        Debug.LogWarning("InventoryScreen missing tab for {type}.");
        return;
      }

      if (_activeTab == tab) {
        UpdateEquipmentUIVisibility(tab);
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
      UpdateEquipmentUIVisibility(tab);
      ApplySelectionForTab(tab, selectFirstIfNone: true);
    }

    private void UpdateEquipmentUIVisibility(InventoryTab activeTab) {
      bool show = activeTab != null && activeTab.Type == ItemType.Equipment;
      SetElementDisplay(_equipButton, show);
      SetElementDisplay(_unequipButton, show);
      SetElementDisplay(_comparisonLabel, show);

      if (!show && _comparisonLabel != null) {
        _comparisonLabel.text = string.Empty;
      }
    }

    private static void SetElementDisplay(VisualElement element, bool visible) {
      if (element == null) {
        return;
      }

      element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
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

          if (_selectedLoadoutSlot.HasValue) {
            ShowEquipmentSlotDetails(_selectedLoadoutSlot.Value);
          } else {
            ClearDetails();
            UpdateActionButtons();
            UpdateComparisonLabel();
          }
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

          if (_selectedLoadoutSlot.HasValue) {
            ShowEquipmentSlotDetails(_selectedLoadoutSlot.Value);
          } else {
            ClearDetails();
            UpdateActionButtons();
            UpdateComparisonLabel();
          }
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

          if (_selectedLoadoutSlot.HasValue) {
            ShowEquipmentSlotDetails(_selectedLoadoutSlot.Value);
          } else {
            ClearDetails();
            UpdateActionButtons();
            UpdateComparisonLabel();
          }
        }

        return;
      }

      InventoryItemViewModel viewModel = tab.Items[index];
      _selectedInventoryItem = viewModel;
      _lastSelectedInventoryKey = viewModel.SelectionKey;

      ShowInventoryItemDetails(viewModel);

      if (viewModel.Item is EquipmentItemScriptableObject equipmentItem) {
        _selectedLoadoutSlotType = equipmentItem.Slot;
        SelectLoadoutSlot(equipmentItem.Slot, showDetails: false);
      }

      UpdateActionButtons();
      UpdateComparisonLabel();
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
        if (builder.Length > 0) {
          _ = builder.AppendLine();
        }

        _ = builder.AppendLine("Stats:");
        _ = builder.AppendLine(BuildStatSummary(equipmentItem));
      }

      string text = builder.Length > 0 ? builder.ToString().TrimEnd() : "No description available.";
      _detailDescription.text = text;
    }

    private void ShowEquipmentSlotDetails(LoadoutSlotViewModel viewModel) {
      if (_detailTitle != null) {
        _detailTitle.text = viewModel.IsEmpty ? viewModel.SlotLabel : viewModel.ItemLabel;
      }

      if (_detailQuantity != null) {
        string slotDetail = viewModel.IsEmpty
          ? $"Slot: {viewModel.SlotLabel} (empty)"
          : $"Slot: {viewModel.SlotLabel}";
        _detailQuantity.text = slotDetail;
      }

      if (_detailDescription != null) {
        if (viewModel.IsEmpty) {
          _detailDescription.text = "No item equipped.";
        } else {
          var builder = new StringBuilder();

          if (!string.IsNullOrWhiteSpace(viewModel.Description)) {
            _ = builder.AppendLine(viewModel.Description.Trim());
          }

          if (builder.Length > 0) {
            _ = builder.AppendLine();
          }

          _ = builder.AppendLine("Stats:");
          _ = builder.AppendLine(BuildStatSummary(viewModel.Item));

          _detailDescription.text = builder.ToString().TrimEnd();
        }
      }

      UpdateActionButtons();
      UpdateComparisonLabel();
    }

    private void SelectLoadoutSlot(EquipmentSlotType slotType, bool showDetails = true) {
      if (_loadoutListView == null || _loadoutItems.Count == 0) {
        return;
      }

      int index = -1;
      for (int i = 0; i < _loadoutItems.Count; i++) {
        if (_loadoutItems[i].SlotType == slotType) {
          index = i;
          break;
        }
      }

      if (index < 0) {
        return;
      }

      _suppressSelectionEvents = true;
      _loadoutListView.selectedIndex = index;
      _suppressSelectionEvents = false;

      LoadoutSlotViewModel viewModel = _loadoutItems[index];
      _selectedLoadoutSlot = viewModel;
      _selectedLoadoutSlotType = viewModel.SlotType;
      _selectedLoadoutIndex = index;

      if (showDetails) {
        ShowEquipmentSlotDetails(viewModel);
      } else {
        UpdateActionButtons();
        UpdateComparisonLabel();
      }
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

      ResolvePlayerEquipment();
    }

    private void ResolvePlayerEquipment() {
      PlayerEquipment resolved = null;

      if (_playerInventory != null) {
        resolved = _playerInventory.Equipment
          ?? _playerInventory.GetComponent<PlayerEquipment>()
          ?? _playerInventory.GetComponentInParent<PlayerEquipment>();
      }

      if (resolved == null) {
        resolved = FindFirstObjectByType<PlayerEquipment>();
      }

      if (resolved == _playerEquipment) {
        return;
      }

      _playerEquipment = resolved;
    }

    private void SubscribeToEquipment() {
      if (_activeEquipment == null) {
        _subscribedEquipment = null;
        return;
      }

      _subscribedEquipment = _activeEquipment;
      _subscribedEquipment.OnEquipped += HandleEquipmentEquipped;
      _subscribedEquipment.OnUnequipped += HandleEquipmentUnequipped;
      _subscribedEquipment.OnLoadoutChanged += HandleEquipmentLoadoutChanged;
    }

    private void UnsubscribeFromEquipment() {
      if (_subscribedEquipment == null) {
        return;
      }

      _subscribedEquipment.OnEquipped -= HandleEquipmentEquipped;
      _subscribedEquipment.OnUnequipped -= HandleEquipmentUnequipped;
      _subscribedEquipment.OnLoadoutChanged -= HandleEquipmentLoadoutChanged;
      _subscribedEquipment = null;
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

    private void HandleEquipmentEquipped(EquipmentSlotType slotType, EquipmentItemScriptableObject item) {
      _selectedLoadoutSlotType = slotType;
      RefreshInventory();
    }

    private void HandleEquipmentUnequipped(EquipmentSlotType slotType, EquipmentItemScriptableObject item) {
      _selectedLoadoutSlotType = slotType;
      RefreshInventory();
    }

    private void HandleEquipmentLoadoutChanged() {
      RefreshLoadout();
    }

    private void HandleInventoryUpdated(int index, InventorySlot slot) {
      RefreshInventory();
    }

    private void HandleInventoryItemChanged(ItemScriptableObject item, int amount) {
      RefreshInventory();
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

          InventoryItemViewModel viewModel = new InventoryItemViewModel(
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

      RefreshLoadout();
      ApplySelectionForTab(_activeTab, selectFirstIfNone: false);
      UpdateActionButtons();
      UpdateComparisonLabel();
    }

    private void ConfigureTeamList() {
      if (_teamListView == null) {
        return;
      }

      _teamListView.itemsSource = _teamMembers;
      _teamListView.selectionType = SelectionType.Single;

      if (_itemRowHeight > 0f) {
        _teamListView.fixedItemHeight = _itemRowHeight;
      }

      _teamListView.makeItem = () => {
        var root = new VisualElement();
        root.AddToClassList("inventory-list-item");
        root.AddToClassList("team-list-item");

        var nameLabel = new Label { name = "team-list-item__name" };
        nameLabel.AddToClassList("team-list-item__name");
        root.Add(nameLabel);

        return root;
      };

      _teamListView.bindItem = (element, index) => {
        if (index < 0 || index >= _teamMembers.Count) {
          return;
        }

        Label label = element.Q<Label>("team-list-item__name");
        if (label != null) {
          label.text = _teamMembers[index].DisplayName;
        }
      };

      _teamSelectionHandler = OnTeamSelectionChanged;
      _teamListView.selectionChanged += _teamSelectionHandler;
    }

    private void ConfigureLoadoutList() {
      if (_loadoutListView == null) {
        return;
      }

      _loadoutListView.itemsSource = _loadoutItems;
      _loadoutListView.selectionType = SelectionType.Single;

      if (_itemRowHeight > 0f) {
        _loadoutListView.fixedItemHeight = _itemRowHeight;
      }

      _loadoutListView.makeItem = () => {
        var root = new VisualElement();
        root.AddToClassList("inventory-list-item");
        root.AddToClassList("loadout-list-item");

        var slotLabel = new Label { name = "loadout-list-item__slot" };
        slotLabel.AddToClassList("loadout-list-item__slot");
        root.Add(slotLabel);

        var itemLabel = new Label { name = "loadout-list-item__item" };
        itemLabel.AddToClassList("loadout-list-item__item");
        root.Add(itemLabel);

        return root;
      };

      _loadoutListView.bindItem = (element, index) => {
        if (index < 0 || index >= _loadoutItems.Count) {
          return;
        }

        LoadoutSlotViewModel viewModel = _loadoutItems[index];
        Label slotLabel = element.Q<Label>("loadout-list-item__slot");
        if (slotLabel != null) {
          slotLabel.text = viewModel.SlotLabel;
        }

        Label itemLabel = element.Q<Label>("loadout-list-item__item");
        if (itemLabel != null) {
          itemLabel.text = viewModel.ItemLabel;
          itemLabel.EnableInClassList("loadout-list-item__item--empty", viewModel.IsEmpty);
        }
      };

      _loadoutSelectionHandler = HandleLoadoutSelectionChanged;
      _loadoutListView.selectionChanged += _loadoutSelectionHandler;
    }

    private void RefreshTeamMembers() {
      string previousId = _activeTeamMemberId;
      _teamMembers.Clear();

      if (_playerEquipment != null) {
        string displayName = ResolveEquipmentOwnerName(_playerEquipment);
        _teamMembers.Add(new TeamMemberEntry(_playerEquipment, displayName));
      }

      _teamListView?.RefreshItems();
      UpdateTeamPanelState();

      if (_teamMembers.Count == 0) {
        _activeTeamMemberId = string.Empty;
        _activeTeamMemberName = string.Empty;
        SetActiveEquipment(null);
        return;
      }

      int index = 0;
      if (!string.IsNullOrEmpty(previousId)) {
        for (int i = 0; i < _teamMembers.Count; i++) {
          if (_teamMembers[i].Id == previousId) {
            index = i;
            break;
          }
        }
      }

      if (index < 0 || index >= _teamMembers.Count) {
        index = 0;
      }

      TeamMemberEntry selected = _teamMembers[index];
      _activeTeamMemberId = selected.Id;
      _activeTeamMemberName = selected.DisplayName;

      if (_teamListView != null) {
        _suppressSelectionEvents = true;
        _teamListView.selectedIndex = index;
        _suppressSelectionEvents = false;
      }

      SetActiveEquipment(selected.Equipment);
    }



    private string ResolveEquipmentOwnerName(PlayerEquipment equipment) {
      if (equipment == null) {
        return string.Empty;
      }

      Combatant combatant = equipment.GetComponent<Combatant>() ?? equipment.GetComponentInParent<Combatant>();
      if (combatant != null && !string.IsNullOrWhiteSpace(combatant.Name)) {
        return combatant.Name;
      }

      string owner = equipment.gameObject != null ? equipment.gameObject.name : string.Empty;
      return owner ?? string.Empty;
    }


    private void UpdateTeamPanelState() {
      if (_teamListView == null || _teamEmptyLabel == null) {
        return;
      }

      bool hasMembers = _teamMembers.Count > 0;
      _teamListView.style.display = hasMembers ? DisplayStyle.Flex : DisplayStyle.None;
      _teamEmptyLabel.style.display = hasMembers ? DisplayStyle.None : DisplayStyle.Flex;
    }

    private void SetActiveEquipment(PlayerEquipment equipment) {
      if (_activeEquipment == equipment) {
        UpdateLoadoutTitle(_activeTeamMemberName);
        RefreshLoadout();
        return;
      }

      UnsubscribeFromEquipment();
      _activeEquipment = equipment;
      _selectedLoadoutSlot = null;
      _selectedLoadoutSlotType = null;
      _selectedLoadoutIndex = -1;
      SubscribeToEquipment();
      UpdateLoadoutTitle(_activeTeamMemberName);
      RefreshLoadout();
      UpdateActionButtons();
      UpdateComparisonLabel();
    }

    private void UpdateLoadoutTitle(string ownerName) {
      if (_loadoutTitle == null) {
        return;
      }

      _loadoutTitle.text = string.IsNullOrWhiteSpace(ownerName)
        ? "Loadout"
        : $"Loadout � {ownerName}";
    }

    private void RefreshLoadout() {
      EquipmentSlotType? previousSlot = _selectedLoadoutSlotType;
      _loadoutItems.Clear();

      if (_activeEquipment != null) {
        EquipmentSet equipmentSet = _activeEquipment.Equipment;
        if (equipmentSet != null) {
          var seen = new HashSet<EquipmentSlotType>();

          foreach (EquipmentSlotType slotType in DefaultLoadoutOrder) {
            if (!equipmentSet.TryGetSlot(slotType, out EquipmentSlot slot)) {
              continue;
            }

            AddLoadoutSlot(slot);
            _ = seen.Add(slotType);
          }

          foreach (EquipmentSlot slot in equipmentSet.Slots) {
            if (!seen.Add(slot.SlotType)) {
              continue;
            }

            AddLoadoutSlot(slot);
          }
        }
      }

      _loadoutListView?.RefreshItems();
      UpdateLoadoutPanelState();

      if (previousSlot.HasValue) {
        SelectLoadoutSlot(previousSlot.Value);
      }
    }

    private void UpdateLoadoutPanelState() {
      if (_loadoutListView == null || _loadoutEmptyLabel == null) {
        return;
      }

      bool hasSlots = _loadoutItems.Count > 0;
      _loadoutListView.style.display = hasSlots ? DisplayStyle.Flex : DisplayStyle.None;
      _loadoutEmptyLabel.style.display = hasSlots ? DisplayStyle.None : DisplayStyle.Flex;
    }

    private void AddLoadoutSlot(EquipmentSlot slot) {
      if (slot == null) {
        return;
      }

      EquipmentItemScriptableObject item = slot.Item;
      string slotLabel = FormatSlotName(slot.SlotType);
      string itemLabel = item != null
        ? (string.IsNullOrEmpty(item.DisplayName) ? item.name : item.DisplayName)
        : "Empty";

      _loadoutItems.Add(new LoadoutSlotViewModel(slot.SlotType, slotLabel, item, itemLabel));
    }
    private static string FormatSlotName(EquipmentSlotType slotType) {
      string name = slotType.ToString();
      if (string.IsNullOrEmpty(name)) {
        return string.Empty;
      }

      var builder = new StringBuilder(name.Length + 4);
      _ = builder.Append(char.ToUpperInvariant(name[0]));

      for (int i = 1; i < name.Length; i++) {
        char current = name[i];
        char previous = name[i - 1];
        bool insertSpace = char.IsUpper(current) && !char.IsUpper(previous);

        if (insertSpace) {
          _ = builder.Append(' ');
        }

        _ = builder.Append(current);
      }

      return builder.ToString();
    }

    private void OnTeamSelectionChanged(IEnumerable<object> _) {
      if (_suppressSelectionEvents) {
        return;
      }

      if (_teamListView == null) {
        return;
      }

      int index = _teamListView.selectedIndex;
      if (index < 0 || index >= _teamMembers.Count) {
        _activeTeamMemberId = string.Empty;
        _activeTeamMemberName = string.Empty;
        SetActiveEquipment(null);
        return;
      }

      TeamMemberEntry entry = _teamMembers[index];
      _activeTeamMemberId = entry.Id;
      _activeTeamMemberName = entry.DisplayName;
      SetActiveEquipment(entry.Equipment);
    }

    private void HandleLoadoutSelectionChanged(IEnumerable<object> _) {
      if (_suppressSelectionEvents) {
        return;
      }

      if (_loadoutListView == null) {
        return;
      }

      int index = _loadoutListView.selectedIndex;
      if (index < 0 || index >= _loadoutItems.Count) {
        ClearLoadoutSelection();
        return;
      }

      LoadoutSlotViewModel viewModel = _loadoutItems[index];
      _selectedLoadoutSlot = viewModel;
      _selectedLoadoutSlotType = viewModel.SlotType;
      _selectedLoadoutIndex = index;
      ShowEquipmentSlotDetails(viewModel);
    }

    private void HandleEquipButtonClicked() {
      if (_activeEquipment == null || !_selectedInventoryItem.HasValue) {
        return;
      }

      InventoryItemViewModel viewModel = _selectedInventoryItem.Value;
      if (viewModel.Item is not EquipmentItemScriptableObject equipmentItem) {
        return;
      }

      EquipmentSlotType targetSlot = _selectedLoadoutSlotType ?? equipmentItem.Slot;
      EquipmentSet equipmentSet = _activeEquipment.Equipment;
      if (equipmentSet == null || !equipmentSet.HasSlot(targetSlot)) {
        Debug.LogWarning($"Cannot equip {equipmentItem.name}: slot {targetSlot} unavailable.", _activeEquipment);
        return;
      }

      ItemResult result = _activeEquipment.TryEquip(equipmentItem, targetSlot);
      if (!result.IsSuccess) {
        Debug.LogWarning(result.Message, _activeEquipment);
        return;
      }

      _selectedInventoryItem = null;
      _lastSelectedInventoryKey = string.Empty;
      ClearActiveInventorySelection();
      _selectedLoadoutSlotType = targetSlot;
      RefreshInventory();
      SelectLoadoutSlot(targetSlot);
    }

    private void HandleUnequipButtonClicked() {
      if (_activeEquipment == null) {
        return;
      }

      EquipmentSlotType? slotType = _selectedLoadoutSlotType
        ?? (_selectedLoadoutSlot.HasValue ? _selectedLoadoutSlot.Value.SlotType : null);

      if (!slotType.HasValue) {
        return;
      }

      ItemResult result = _activeEquipment.TryUnequip(slotType.Value);
      if (!result.IsSuccess) {
        Debug.LogWarning(result.Message, _activeEquipment);
        return;
      }

      _selectedInventoryItem = null;
      _lastSelectedInventoryKey = string.Empty;
      ClearActiveInventorySelection();
      RefreshInventory();
      SelectLoadoutSlot(slotType.Value);
    }

    private void UpdateActionButtons() {
      if (_equipButton != null) {
        bool canEquip = false;
        if (_activeEquipment != null && _selectedInventoryItem.HasValue) {
          InventoryItemViewModel viewModel = _selectedInventoryItem.Value;
          if (viewModel.Item is EquipmentItemScriptableObject equipmentItem) {
            EquipmentSet equipmentSet = _activeEquipment.Equipment;
            if (equipmentSet != null) {
              EquipmentSlotType targetSlot = _selectedLoadoutSlotType ?? equipmentItem.Slot;
              canEquip = equipmentSet.HasSlot(targetSlot);
            }
          }
        }

        _equipButton.SetEnabled(canEquip);
      }

      if (_unequipButton != null) {
        bool canUnequip = _activeEquipment != null
          && ((_selectedLoadoutSlot.HasValue && !_selectedLoadoutSlot.Value.IsEmpty)
              || (_selectedLoadoutSlotType.HasValue && GetEquippedItem(_selectedLoadoutSlotType.Value) != null));

        _unequipButton.SetEnabled(canUnequip);
      }
    }
    private void UpdateComparisonLabel() {
      if (_comparisonLabel == null) {
        return;
      }

      var nextItems = new List<EquipmentItemScriptableObject>();
      var currentItems = new List<EquipmentItemScriptableObject>();
      EquipmentSlotType? targetSlot = _selectedLoadoutSlotType;

      if (_selectedInventoryItem.HasValue && _selectedInventoryItem.Value.Item is EquipmentItemScriptableObject nextItem) {
        AddComparisonItem(nextItems, nextItem);
        targetSlot ??= nextItem.Slot;
      }

      if (_selectedLoadoutSlot.HasValue && _selectedLoadoutSlot.Value.Item != null) {
        AddComparisonItem(currentItems, _selectedLoadoutSlot.Value.Item);
      }

      if (targetSlot.HasValue) {
        EquipmentItemScriptableObject equippedItem = GetEquippedItem(targetSlot.Value);
        AddComparisonItem(currentItems, equippedItem);
      }

      EquipmentSet equipmentSet = _activeEquipment != null ? _activeEquipment.Equipment : null;
      if (equipmentSet != null && targetSlot.HasValue) {
        EquipmentSlotType slotType = targetSlot.Value;
        bool isHandSlot = IsHandSlot(slotType);
        bool nextIsTwoHanded = nextItems.Exists(item => item != null && item.OccupiesBothHands);

        if (isHandSlot) {
          if (nextIsTwoHanded) {
            EquipmentSlotType opposite = GetOppositeHand(slotType);
            if (equipmentSet.HasSlot(opposite)) {
              AddComparisonItem(currentItems, equipmentSet.GetEquippedItem(opposite));
            }
          } else {
            EquipmentSlotType? twoHandAnchor = FindTwoHandAnchor(equipmentSet);
            if (twoHandAnchor.HasValue && twoHandAnchor.Value != slotType) {
              AddComparisonItem(currentItems, equipmentSet.GetEquippedItem(twoHandAnchor.Value));
            }
          }
        }
      }

      _comparisonLabel.text = BuildComparisonText(nextItems, currentItems);
    }

    private static void AddComparisonItem(List<EquipmentItemScriptableObject> list, EquipmentItemScriptableObject item) {
      if (item == null || list.Contains(item)) {
        return;
      }

      list.Add(item);
    }

    private static bool IsHandSlot(EquipmentSlotType slotType) {
      return slotType == EquipmentSlotType.MainHand || slotType == EquipmentSlotType.OffHand;
    }

    private static EquipmentSlotType GetOppositeHand(EquipmentSlotType slotType) {
      return slotType == EquipmentSlotType.MainHand ? EquipmentSlotType.OffHand : EquipmentSlotType.MainHand;
    }

    private static EquipmentSlotType? FindTwoHandAnchor(EquipmentSet equipmentSet) {
      if (equipmentSet == null) {
        return null;
      }

      EquipmentItemScriptableObject mainHand = equipmentSet.GetEquippedItem(EquipmentSlotType.MainHand);
      if (mainHand != null && mainHand.OccupiesBothHands) {
        return EquipmentSlotType.MainHand;
      }

      EquipmentItemScriptableObject offHand = equipmentSet.GetEquippedItem(EquipmentSlotType.OffHand);
      if (offHand != null && offHand.OccupiesBothHands) {
        return EquipmentSlotType.OffHand;
      }

      if (equipmentSet.IsSlotBlocked(EquipmentSlotType.MainHand)) {
        return EquipmentSlotType.OffHand;
      }

      if (equipmentSet.IsSlotBlocked(EquipmentSlotType.OffHand)) {
        return EquipmentSlotType.MainHand;
      }

      return null;
    }

    private string BuildComparisonText(IReadOnlyList<EquipmentItemScriptableObject> nextItems, IReadOnlyList<EquipmentItemScriptableObject> currentItems) {
      bool hasNext = nextItems != null && nextItems.Count > 0;
      bool hasCurrent = currentItems != null && currentItems.Count > 0;

      if (!hasNext && !hasCurrent) {
        return "<b>Comparison:</b> Select equipment to view stat changes.";
      }

      var lines = BuildComparisonLines(nextItems, currentItems);
      if (lines.Count == 0) {
        return "<b>Comparison:</b> No stat changes.";
      }

      StringBuilder builder = new StringBuilder();
      _ = builder.Append("<b>Comparison:</b>");

      foreach (string line in lines) {
        _ = builder.AppendLine();
        _ = builder.Append(line);
      }

      return builder.ToString();
    }

    private List<string> BuildComparisonLines(IReadOnlyList<EquipmentItemScriptableObject> nextItems, IReadOnlyList<EquipmentItemScriptableObject> currentItems) {
      Dictionary<StatType, StatAggregate> nextStats = BuildStatDictionary(nextItems);
      Dictionary<StatType, StatAggregate> currentStats = BuildStatDictionary(currentItems);
      var lines = new List<string>();

      foreach (StatType statType in Enum.GetValues(typeof(StatType))) {
        _ = nextStats.TryGetValue(statType, out StatAggregate nextAggregate);
        _ = currentStats.TryGetValue(statType, out StatAggregate currentAggregate);

        int flatDelta = nextAggregate.Flat - currentAggregate.Flat;
        float percentDelta = nextAggregate.Percent - currentAggregate.Percent;

        if (flatDelta == 0 && Mathf.Abs(percentDelta) <= 0.0001f) {
          continue;
        }

        var parts = new List<string>();
        if (flatDelta != 0) {
          parts.Add(FormatColoredFlatDelta(flatDelta));
        }

        if (Mathf.Abs(percentDelta) > 0.0001f) {
          parts.Add(FormatColoredPercentDelta(percentDelta));
        }

        string formattedParts = string.Join(", ", parts);
        lines.Add($"{statType}: {formattedParts}");
      }

      return lines;
    }

    private string BuildStatSummary(EquipmentItemScriptableObject item) {
      Dictionary<StatType, StatAggregate> stats = BuildStatDictionary(item);
      if (stats.Count == 0) {
        return "  No modifiers.";
      }

      var builder = new StringBuilder();
      foreach (StatType statType in Enum.GetValues(typeof(StatType))) {
        if (!stats.TryGetValue(statType, out StatAggregate aggregate)) {
          continue;
        }

        _ = builder.Append("  ");
        _ = builder.Append(statType);
        _ = builder.Append(':');
        _ = builder.Append(' ');
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

    private static string FormatColoredFlatDelta(int value) {
      string color = value >= 0 ? _positiveDeltaColor : _negativeDeltaColor;
      return $"<color={color}>{FormatSignedInt(value)}</color>";
    }

    private static string FormatColoredPercentDelta(float value) {
      string color = value >= 0f ? _positiveDeltaColor : _negativeDeltaColor;
      return $"<color={color}>{FormatPercent(value)}</color>";
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



    private EquipmentItemScriptableObject GetEquippedItem(EquipmentSlotType slotType) {
      return _activeEquipment != null && _activeEquipment.Equipment != null
        ? _activeEquipment.Equipment.GetEquippedItem(slotType)
        : null;
    }

    private void ClearLoadoutSelection() {
      if (_loadoutListView != null) {
        _suppressSelectionEvents = true;
        _loadoutListView.ClearSelection();
        _suppressSelectionEvents = false;
      }

      _selectedLoadoutSlot = null;
      _selectedLoadoutSlotType = null;
      _selectedLoadoutIndex = -1;
      UpdateActionButtons();
      UpdateComparisonLabel();
    }

    private void ClearTabSelections() {
      foreach (InventoryTab tab in _tabs.Values) {
        tab.ListView?.ClearSelection();
      }

      _selectedInventoryItem = null;
      _lastSelectedInventoryKey = string.Empty;
      UpdateActionButtons();
      UpdateComparisonLabel();
    }


    private void ClearActiveInventorySelection() {
      if (_activeTab?.ListView == null) {
        return;
      }

      _suppressSelectionEvents = true;
      _activeTab.ListView.selectedIndex = -1;
      _activeTab.ListView.ClearSelection();
      _suppressSelectionEvents = false;
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

  private readonly struct LoadoutSlotViewModel {
    public LoadoutSlotViewModel(EquipmentSlotType slotType, string slotLabel, EquipmentItemScriptableObject item, string itemLabel) {
      SlotType = slotType;
      Item = item;
      SlotLabel = string.IsNullOrWhiteSpace(slotLabel) ? slotType.ToString() : slotLabel;
      ItemLabel = string.IsNullOrWhiteSpace(itemLabel)
        ? item != null
          ? (!string.IsNullOrWhiteSpace(item.DisplayName) ? item.DisplayName : item.name)
          : "Empty"
        : itemLabel;
    }

    public EquipmentSlotType SlotType { get; }
    public string SlotLabel { get; }
    public EquipmentItemScriptableObject Item { get; }
    public string ItemLabel { get; }
    public string Description => Item != null ? Item.Description : string.Empty;
    public bool IsEmpty => Item == null;
  }

  private readonly struct TeamMemberEntry {
    public TeamMemberEntry(PlayerEquipment equipment, string displayName) {
      Equipment = equipment;
      DisplayName = !string.IsNullOrWhiteSpace(displayName)
        ? displayName
        : equipment != null ? equipment.gameObject.name : "Unknown";
      Id = equipment != null ? equipment.GetInstanceID().ToString() : string.Empty;
    }

    public string Id { get; }
    public string DisplayName { get; }
    public PlayerEquipment Equipment { get; }
  }

  private struct StatAggregate {
      public int Flat;
      public float Percent;
    }
  }
}


