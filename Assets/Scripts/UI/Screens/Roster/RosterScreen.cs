using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

using EchoesOfTheVoid.Core.Combat;
using EchoesOfTheVoid.Core.Combat.Data;
using EchoesOfTheVoid.Core.Combat.Systems;
using EchoesOfTheVoid.Core.Inventory.Data;
using EchoesOfTheVoid.Core.Inventory.Player;
using EchoesOfTheVoid.Core.Inventory.ScriptableObjects;
using EchoesOfTheVoid.Core.Roster;
using EchoesOfTheVoid.Core.Roster.Data;
using EchoesOfTheVoid.Core.Roster.Progression.Contracts;
using EchoesOfTheVoid.UI.Modals;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace EchoesOfTheVoid.UI.Roster {
  public class RosterScreen : UIScreen {
    [SerializeField] private PlayerRosterService _rosterService;
    [SerializeField] private CombatEncounterBootstrapper _encounterBootstrapper;
    [SerializeField] private EchoEquipmentModal _equipmentModal;
    [SerializeField] private EchoGambitModal _gambitModal;
    [SerializeField] private EchoSkillTreeModal _skillTreeModal;
    [SerializeField] private EchoSkillNodeDetailModal _skillNodeModal;
    [SerializeField] private PlayerInventory _playerInventory;

    private Label _summaryLabel;
    private ListView _ownedListView;
    private Label _ownedEmptyLabel;
    private Button _assignButton;
    private Button _removeButton;
    private Button _equipmentButton;
    private Button _skillTreeButton;
    private Button _gambitButton;
    private Button _confirmButton;
    private Button _closeButton;
    private Label _detailNameLabel;
    private Label _detailSlotLabel;
    private VisualElement _detailProgressRoot;
    private Label _detailLevelLabel;
    private ProgressBar _detailExperienceBar;
    private VisualElement _detailStatsRoot;

    private readonly List<OwnedEchoViewModel> _ownedEchoItems = new();
    private readonly List<PartySlotView> _partySlotViews = new();

    private string _selectedEchoId = string.Empty;
    private int _selectedPartySlotIndex = -1;

    private bool _rosterEventsSubscribed;

    public event Action OnPartyConfirmed;
    public event Action<PlayerEchoData> OnEquipmentRequested;
    public event Action<PlayerEchoData> OnGambitEditRequested;

#if UNITY_EDITOR
    private void OnValidate() {
      if (string.IsNullOrEmpty(_screenId)) {
        _screenId = "RosterScreen";
      }

      if (_screenTemplate == null) {
        _screenTemplate = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI Toolkit/Screens/Roster/Roster.uxml");
      }
    }
#endif

    protected override void SetupUI() {
      InitializeServices();

      _summaryLabel = FindLabel("roster-summary");
      _ownedListView = FindElement<ListView>("owned-list");
      _ownedEmptyLabel = FindLabel("owned-empty");
      _assignButton = FindButton("assign-button");
      _removeButton = FindButton("remove-button");
      _equipmentButton = FindButton("equipment-button");
      _skillTreeButton = FindButton("skill-tree-button");
      _gambitButton = FindButton("gambit-button");
      _confirmButton = FindButton("confirm-button");
      _closeButton = FindButton("close-button");
      _detailNameLabel = FindLabel("detail-name");
      _detailSlotLabel = FindLabel("detail-slot");
      _detailProgressRoot = FindElement<VisualElement>("detail-progress");
      _detailLevelLabel = FindLabel("detail-level");
      _detailExperienceBar = FindElement<ProgressBar>("detail-experience-bar");
      _detailStatsRoot = FindElement<VisualElement>("detail-stats");

      if (_detailProgressRoot != null) {
        _detailProgressRoot.style.display = DisplayStyle.None;
      }

      if (_detailExperienceBar != null) {
        _detailExperienceBar.style.display = DisplayStyle.None;
      }

      ConfigureOwnedList();
      ConfigurePartyGrid();

      if (_equipmentModal != null) {
        _equipmentModal.OnEquipmentApplied += HandleEquipmentApplied;
        _equipmentModal.ConfigureServices(_rosterService, ResolvePlayerInventory());
      }

      if (_gambitModal != null) {
        _gambitModal.ConfigureServices(_rosterService, ResolvePlayerInventory());
        _gambitModal.OnGambitApplied += HandleGambitApplied;
      }

      if (_skillTreeModal != null) {
        _skillTreeModal.OnNodeSelected += HandleSkillTreeNodeSelected;
      }

      if (_skillNodeModal != null) {
        _skillNodeModal.ConfigureServices(_rosterService);
        _skillNodeModal.OnNodeUnlockSucceeded += HandleSkillNodeUnlocked;
      }

      UpdateActionButtons();
      RefreshAll();
    }

    protected override void BindEvents() {
      if (_ownedListView != null) {
        _ownedListView.selectionChanged += OnOwnedSelectionChanged;
      }

      if (_assignButton != null) {
        _assignButton.clicked += OnAssignClicked;
      }

      if (_removeButton != null) {
        _removeButton.clicked += OnRemoveClicked;
      }

      if (_equipmentButton != null) {
        _equipmentButton.clicked += OnEquipmentClicked;
      }

      if (_skillTreeButton != null) {
        _skillTreeButton.clicked += OnSkillTreeClicked;
      }

      if (_gambitButton != null) {
        _gambitButton.clicked += OnGambitClicked;
      }

      if (_confirmButton != null) {
        _confirmButton.clicked += OnConfirmParty;
      }

      if (_closeButton != null) {
        _closeButton.clicked += () => NavigationManager.Instance.NavigateBack();
      }

      SubscribeRosterEvents();
    }

    protected override void OnShow() {
      base.OnShow();
      InitializeServices();
      SubscribeRosterEvents();

      if (_equipmentModal != null) {
        _equipmentModal.ConfigureServices(_rosterService, ResolvePlayerInventory());
      }

      if (_gambitModal != null) {
        _gambitModal.ConfigureServices(_rosterService, ResolvePlayerInventory());
      }

      if (_skillNodeModal != null) {
        _skillNodeModal.ConfigureServices(_rosterService);
      }

      RefreshAll();
    }

    protected override void OnHide() {
      base.OnHide();
    }

    private void OnDestroy() {
      if (_rosterEventsSubscribed && _rosterService != null) {
        _rosterService.OnRosterChanged -= HandleRosterChanged;
        _rosterService.OnPartySlotChanged -= HandlePartySlotChanged;
        _rosterService.OnEchoUpdated -= HandleEchoUpdated;
        _rosterEventsSubscribed = false;
      }

      if (_equipmentModal != null) {
        _equipmentModal.OnEquipmentApplied -= HandleEquipmentApplied;
      }

      if (_gambitModal != null) {
        _gambitModal.OnGambitApplied -= HandleGambitApplied;
      }

      if (_skillTreeModal != null) {
        _skillTreeModal.OnNodeSelected -= HandleSkillTreeNodeSelected;
      }

      if (_skillNodeModal != null) {
        _skillNodeModal.OnNodeUnlockSucceeded -= HandleSkillNodeUnlocked;
      }
    }

    private void InitializeServices() {
      if (_rosterService == null) {
        _rosterService = FindFirstObjectByType<PlayerRosterService>();
      }

      ResolvePlayerInventory();

      if (_encounterBootstrapper == null) {
        _encounterBootstrapper = FindFirstObjectByType<CombatEncounterBootstrapper>();
      }
    }

    private PlayerInventory ResolvePlayerInventory() {
      if (_playerInventory == null) {
        _playerInventory = FindFirstObjectByType<PlayerInventory>();
      }

      return _playerInventory;
    }

    private void ConfigureOwnedList() {
      if (_ownedListView == null) {
        return;
      }

      _ownedListView.itemsSource = _ownedEchoItems;
      _ownedListView.selectionType = SelectionType.Single;

      _ownedListView.makeItem = () => {
        var root = new VisualElement();
        root.AddToClassList("roster-owned-item");

        var nameLabel = new Label { name = "roster-owned-item__name" };
        nameLabel.AddToClassList("roster-owned-item__name");
        root.Add(nameLabel);

        var statusLabel = new Label { name = "roster-owned-item__status" };
        statusLabel.AddToClassList("roster-owned-item__status");
        root.Add(statusLabel);

        return root;
      };

      _ownedListView.bindItem = (element, index) => {
        if (index < 0 || index >= _ownedEchoItems.Count) {
          return;
        }

        OwnedEchoViewModel viewModel = _ownedEchoItems[index];
        Label nameLabel = element.Q<Label>("roster-owned-item__name");
        if (nameLabel != null) {
          nameLabel.text = viewModel.DisplayName;
        }

        Label statusLabel = element.Q<Label>("roster-owned-item__status");
        if (statusLabel != null) {
          statusLabel.text = viewModel.IsInParty && viewModel.PartySlotIndex >= 0 ? $"Party Slot {viewModel.PartySlotIndex + 1}" : "Reserve";

          statusLabel.EnableInClassList("roster-owned-item__status--party", viewModel.IsInParty);
        }
      };
    }

    private void ConfigurePartyGrid() {
      _partySlotViews.Clear();
      for (int i = 0; i < 9; i++) {
        VisualElement slotElement = FindElement<VisualElement>($"party-slot-{i}");
        if (slotElement == null) {
          continue;
        }

        var label = new Label { name = "roster-party-slot__label" };
        label.AddToClassList("roster-party-slot__label");
        slotElement.Add(label);

        int captured = i;
        slotElement.RegisterCallback<ClickEvent>(_ => OnPartySlotClicked(captured));

        _partySlotViews.Add(new PartySlotView(slotElement, label, i));
      }
    }

    private void SubscribeRosterEvents() {
      if (_rosterService == null || _rosterEventsSubscribed) {
        return;
      }

      _rosterService.OnRosterChanged += HandleRosterChanged;
      _rosterService.OnPartySlotChanged += HandlePartySlotChanged;
      _rosterService.OnEchoUpdated += HandleEchoUpdated;
      _rosterEventsSubscribed = true;
    }

    private void HandleRosterChanged() {
      RefreshAll();
    }

    private void HandlePartySlotChanged(int slotIndex, string previous, string current) {
      RefreshPartyGrid();
      RefreshOwnedList();
      UpdateActionButtons();
    }

    private void HandleEchoUpdated(PlayerEchoData echo) {
      if (echo == null) {
        return;
      }

      int index = _ownedEchoItems.FindIndex(vm => vm.InstanceId == echo.InstanceId);
      if (index >= 0 && _ownedListView != null) {
        _ownedListView.RefreshItem(index);
      }

      if (echo.InstanceId == _selectedEchoId) {
        if (_skillTreeModal != null && _skillTreeModal.IsVisible) {
          _skillTreeModal.RefreshState(echo);
        }

        if (_skillNodeModal != null && _skillNodeModal.IsVisible && _skillTreeModal != null) {
          string currentNodeId = _skillNodeModal.CurrentNodeId;
          if (!string.IsNullOrEmpty(currentNodeId) && _skillTreeModal.TryGetNode(currentNodeId, out EchoSkillTreeModal.NodeViewModel updatedViewModel)) {
            _skillNodeModal.RefreshWithViewModel(echo, updatedViewModel);
          }
        }

        RefreshDetailPanel();
      }
    }

    private void HandleEquipmentApplied(PlayerEchoData echo) {
      RefreshAll();
    }

    private void HandleGambitApplied(PlayerEchoData echo) {
      RefreshAll();
    }

    private void RefreshAll() {
      RefreshOwnedList();
      RefreshPartyGrid();
      RefreshDetailPanel();
      UpdateActionButtons();
    }

    private void RefreshOwnedList() {
      _ownedEchoItems.Clear();

      if (_rosterService != null) {
        foreach (PlayerEchoData echo in _rosterService.OwnedEchoes) {
          if (echo == null) {
            continue;
          }

          int slotIndex = _rosterService.IsInParty(echo.InstanceId)
            ? GetPartySlotForEcho(echo.InstanceId)
            : -1;

          _ownedEchoItems.Add(new OwnedEchoViewModel(echo, slotIndex));
        }
      }

      _ownedListView?.RefreshItems();
      UpdateSummary();
      UpdateOwnedEmptyState();
      ReselectCurrentEcho();
    }

    private void UpdateSummary() {
      if (_summaryLabel == null || _rosterService == null) {
        return;
      }

      int owned = _rosterService.OwnedEchoes?.Count ?? 0;
      _summaryLabel.text = $"{owned} / {_rosterService.MaxOwnedEchoes}";
    }

    private void UpdateOwnedEmptyState() {
      if (_ownedEmptyLabel == null || _ownedListView == null) {
        return;
      }

      bool hasItems = _ownedEchoItems.Count > 0;
      _ownedEmptyLabel.style.display = hasItems ? DisplayStyle.None : DisplayStyle.Flex;
      _ownedListView.style.display = hasItems ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private void RefreshPartyGrid() {
      if (_rosterService == null) {
        return;
      }

      IReadOnlyList<PlayerRosterService.PartySlotInfo> slots = _rosterService.PartySlots;
      foreach (PartySlotView slot in _partySlotViews) {
        PlayerRosterService.PartySlotInfo info = slot.SlotIndex < slots.Count ? slots[slot.SlotIndex] : default;
        PlayerEchoData echo = !string.IsNullOrEmpty(info.EchoInstanceId) && _rosterService.TryGetEcho(info.EchoInstanceId, out PlayerEchoData data)
          ? data
          : null;

        string labelText;
        if (info.IsLocked) {
          labelText = "Locked";
        } else {
          labelText = echo != null ? echo.DisplayName : $"Slot {slot.SlotIndex + 1}";
        }

        slot.Label.text = labelText;
        slot.Root.EnableInClassList("roster-party-slot--locked", info.IsLocked);
        slot.Root.EnableInClassList("roster-party-slot--occupied", echo != null);
        slot.Root.EnableInClassList("roster-party-slot--selected", slot.SlotIndex == _selectedPartySlotIndex);
      }
    }

    private void RefreshDetailPanel() {
      if (_detailNameLabel == null || _detailSlotLabel == null) {
        return;
      }

      if (string.IsNullOrEmpty(_selectedEchoId) || _rosterService == null || !_rosterService.TryGetEcho(_selectedEchoId, out PlayerEchoData echo)) {
        _detailNameLabel.text = "Select an echo";
        _detailSlotLabel.text = string.Empty;
        UpdateDetailProgress(null);
        _detailStatsRoot?.Clear();
        return;
      }

      _detailNameLabel.text = echo.DisplayName;
      _detailSlotLabel.text = _selectedPartySlotIndex >= 0
        ? $"Assigned Slot: {_selectedPartySlotIndex + 1}"
        : "Currently Benched";
      UpdateDetailProgress(echo);

      PopulateStatBlock(echo);
    }

    private void PopulateStatBlock(PlayerEchoData echo) {
      if (_detailStatsRoot == null || echo?.Template == null) {
        return;
      }

      _detailStatsRoot.Clear();

      CombatStats baseStats = echo.Template.BaseStats;
      CombatStats displayStats;
      IStatProgression statProgression = echo.Template.ProgressionProfile?.StatProgression;

      if (statProgression != null) {
        displayStats = statProgression.BuildStatSnapshot(baseStats, echo.Level);
      } else if (baseStats != null) {
        displayStats = baseStats.Clone();
      } else {
        return;
      }

      Dictionary<StatType, StatTotals> equipmentTotals = BuildEquipmentTotals(echo);

      AddStatLabel("Health", ApplyEquipmentModifiers(displayStats.Health, equipmentTotals, StatType.Health).ToString());
      AddStatLabel("Mana", ApplyEquipmentModifiers(displayStats.Mana, equipmentTotals, StatType.Mana).ToString());
      AddStatLabel("Attack", ApplyEquipmentModifiers(displayStats.Attack, equipmentTotals, StatType.Attack).ToString());
      AddStatLabel("Defense", ApplyEquipmentModifiers(displayStats.Defense, equipmentTotals, StatType.Defense).ToString());
      AddStatLabel("Speed", ApplyEquipmentModifiers(displayStats.Speed, equipmentTotals, StatType.Speed).ToString());
      AddStatLabel("Luck", ApplyEquipmentModifiers(displayStats.Luck, equipmentTotals, StatType.Luck).ToString());
    }

    private static Dictionary<StatType, StatTotals> BuildEquipmentTotals(PlayerEchoData echo) {
      var totals = new Dictionary<StatType, StatTotals>();
      if (echo?.EquipmentLoadout == null) {
        return totals;
      }

      foreach (EquippedItemData entry in echo.EquipmentLoadout) {
        EquipmentItemScriptableObject item = entry.Item;
        if (item?.StatModifiers == null) {
          continue;
        }

        foreach (EquipmentStatModifier modifier in item.StatModifiers) {
          if (!totals.TryGetValue(modifier.Stat, out StatTotals aggregate)) {
            aggregate = default;
          }

          aggregate.Additive += modifier.FlatBonus;
          aggregate.Percent += modifier.PercentBonus;
          totals[modifier.Stat] = aggregate;
        }
      }

      return totals;
    }

    private static int ApplyEquipmentModifiers(int baseValue, Dictionary<StatType, StatTotals> totals, StatType statType) {
      if (!totals.TryGetValue(statType, out StatTotals aggregate)) {
        return baseValue;
      }

      int adjusted = (int)(baseValue * (1f + aggregate.Percent));
      return adjusted + aggregate.Additive;
    }

    private struct StatTotals {
      public int Additive;
      public float Percent;
    }

    private void AddStatLabel(string statName, string value) {
      if (_detailStatsRoot == null) {
        return;
      }

      var row = new VisualElement();
      row.AddToClassList("roster-detail-stat-row");

      var nameLabel = new Label(statName);
      nameLabel.AddToClassList("roster-detail-stat-name");
      row.Add(nameLabel);

      var valueLabel = new Label(value);
      valueLabel.AddToClassList("roster-detail-stat-value");
      row.Add(valueLabel);

      _detailStatsRoot.Add(row);
    }

    private void ClearSelectionState() {
      _selectedEchoId = string.Empty;
      _selectedPartySlotIndex = -1;
      if (_ownedListView != null) {
        _ownedListView.selectedIndex = -1;
      }

      _skillTreeModal?.Hide();
      _skillNodeModal?.Hide();
    }

    private void UpdateDetailProgress(PlayerEchoData echo) {
      if (_detailProgressRoot == null || _detailLevelLabel == null || _detailExperienceBar == null) {
        return;
      }

      if (echo == null) {
        _detailProgressRoot.style.display = DisplayStyle.None;
        _detailExperienceBar.style.display = DisplayStyle.None;
        _detailExperienceBar.title = string.Empty;
        return;
      }

      _detailProgressRoot.style.display = DisplayStyle.Flex;
      _detailLevelLabel.text = $"Level {echo.Level}";

      ILevelProgression progression = echo.Template?.ProgressionProfile?.LevelProgression;
      if (progression == null) {
        _detailExperienceBar.style.display = DisplayStyle.None;
        _detailExperienceBar.title = string.Empty;
        return;
      }

      _detailExperienceBar.style.display = DisplayStyle.Flex;

      if (progression.IsMaxLevel(echo.Level)) {
        _detailExperienceBar.lowValue = 0f;
        _detailExperienceBar.highValue = 1f;
        _detailExperienceBar.value = 1f;
        _detailExperienceBar.title = "Max Level";
        return;
      }

      int required = progression.GetExperienceRequiredForLevel(echo.Level);
      if (required <= 0) {
        required = 1;
      }

      int current = Mathf.Clamp(echo.CurrentExperience, 0, required);
      _detailExperienceBar.lowValue = 0f;
      _detailExperienceBar.highValue = required;
      _detailExperienceBar.value = current;
      _detailExperienceBar.title = $"{current} / {required} XP";
    }

    private void OnOwnedSelectionChanged(IEnumerable<object> _) {
      _selectedPartySlotIndex = -1;
      if (_ownedListView == null || _ownedListView.selectedIndex < 0 || _ownedListView.selectedIndex >= _ownedEchoItems.Count) {
        _selectedEchoId = string.Empty;
      } else {
        OwnedEchoViewModel viewModel = _ownedEchoItems[_ownedListView.selectedIndex];
        _selectedEchoId = viewModel.InstanceId;
        _selectedPartySlotIndex = viewModel.PartySlotIndex;
      }

      if (_skillTreeModal != null && _skillTreeModal.IsVisible) {
        if (TryGetSelectedEcho(out PlayerEchoData selectedEcho)) {
          IEchoSkillTreeDefinition skillTree = ResolveSkillTree(selectedEcho);
          if (skillTree != null) {
            _skillTreeModal.ShowForEcho(selectedEcho, skillTree);
          } else {
            _skillTreeModal.Hide();
          }
        } else {
          _skillTreeModal.Hide();
        }

        _skillNodeModal?.Hide();
      }

      RefreshDetailPanel();
      HighlightSelectedSlot();
      UpdateActionButtons();
    }

    private void OnPartySlotClicked(int slotIndex) {
      if (_rosterService == null) {
        return;
      }

      IReadOnlyList<PlayerRosterService.PartySlotInfo> slots = _rosterService.PartySlots;
      PlayerRosterService.PartySlotInfo info = slotIndex < slots.Count ? slots[slotIndex] : default;

      if (!string.IsNullOrEmpty(_selectedEchoId)) {
        if (_rosterService.TryAssignToSlot(_selectedEchoId, slotIndex, out string errorMessage)) {
          ClearSelectionState();
          RefreshPartyGrid();
          RefreshOwnedList();
          RefreshDetailPanel();
          HighlightSelectedSlot();
          UpdateActionButtons();
        } else if (!string.IsNullOrEmpty(errorMessage)) {
          Debug.LogWarning(errorMessage, this);
        }

        return;
      }

      if (!string.IsNullOrEmpty(info.EchoInstanceId)) {
        int index = _ownedEchoItems.FindIndex(vm => vm.InstanceId == info.EchoInstanceId);
        if (_ownedListView != null && index >= 0) {
          _ownedListView.selectedIndex = index;
        }
      } else {
        _selectedPartySlotIndex = slotIndex;
        HighlightSelectedSlot();
        UpdateActionButtons();
      }
    }

    private void OnAssignClicked() {
      if (_rosterService == null || string.IsNullOrEmpty(_selectedEchoId)) {
        return;
      }

      int targetSlot = _selectedPartySlotIndex;
      if (targetSlot < 0) {
        targetSlot = FindFirstAvailableSlot();
      }

      if (targetSlot < 0) {
        Debug.LogWarning("No available slot to assign echo.", this);
        return;
      }

      if (_rosterService.TryAssignToSlot(_selectedEchoId, targetSlot, out string errorMessage)) {
        ClearSelectionState();
        RefreshPartyGrid();
        RefreshOwnedList();
        RefreshDetailPanel();
        HighlightSelectedSlot();
        UpdateActionButtons();
      } else if (!string.IsNullOrEmpty(errorMessage)) {
        Debug.LogWarning(errorMessage, this);
      }
    }

    private void OnRemoveClicked() {
      if (_rosterService == null || string.IsNullOrEmpty(_selectedEchoId)) {
        return;
      }

      if (_rosterService.RemoveFromParty(_selectedEchoId)) {
        _selectedPartySlotIndex = -1;
        RefreshPartyGrid();
        RefreshOwnedList();
        UpdateActionButtons();
      }
    }

    private void OnEquipmentClicked() {
      if (string.IsNullOrEmpty(_selectedEchoId) || _rosterService == null || !_rosterService.TryGetEcho(_selectedEchoId, out PlayerEchoData echo)) {
        return;
      }

      if (_equipmentModal != null) {
        _equipmentModal.ConfigureServices(_rosterService, ResolvePlayerInventory());
        _equipmentModal.ShowForEcho(echo);
      }

      OnEquipmentRequested?.Invoke(echo);
    }

    private void OnSkillTreeClicked() {
      if (!TryGetSelectedEcho(out PlayerEchoData echo)) {
        return;
      }

      IEchoSkillTreeDefinition skillTree = ResolveSkillTree(echo);
      if (skillTree == null) {
        Debug.LogWarning($"Skill tree not configured for '{echo.DisplayName}'.", this);
        return;
      }

      if (_skillNodeModal != null && _skillNodeModal.IsVisible) {
        _skillNodeModal.Hide();
      }

      _skillTreeModal?.ShowForEcho(echo, skillTree);
    }

    private void OnGambitClicked() {
      if (string.IsNullOrEmpty(_selectedEchoId) || _rosterService == null || !_rosterService.TryGetEcho(_selectedEchoId, out PlayerEchoData echo)) {
        return;
      }

      if (_gambitModal != null) {
        _gambitModal.ConfigureServices(_rosterService, ResolvePlayerInventory());
        _gambitModal.ShowForEcho(echo);
      }

      OnGambitEditRequested?.Invoke(echo);
    }

    private void HandleSkillTreeNodeSelected(EchoSkillTreeModal.NodeViewModel viewModel) {
      if (!TryGetSelectedEcho(out PlayerEchoData echo)) {
        return;
      }

      IEchoSkillTreeDefinition skillTree = ResolveSkillTree(echo);
      if (skillTree == null || _skillNodeModal == null) {
        return;
      }

      _skillNodeModal.PresentNode(echo, skillTree, viewModel);
    }

    private void HandleSkillNodeUnlocked(string nodeId) {
      if (!TryGetSelectedEcho(out PlayerEchoData echo)) {
        return;
      }

      IEchoSkillTreeDefinition skillTree = ResolveSkillTree(echo);
      if (skillTree == null) {
        return;
      }

      _skillTreeModal?.RefreshState(echo);

      if (_skillTreeModal != null && _skillNodeModal != null && !string.IsNullOrEmpty(nodeId) && _skillTreeModal.TryGetNode(nodeId, out EchoSkillTreeModal.NodeViewModel updatedViewModel)) {
        _skillNodeModal.RefreshWithViewModel(echo, updatedViewModel);
      }

      UpdateActionButtons();
    }

    private void OnConfirmParty() {
      OnPartyConfirmed?.Invoke();

      if (_encounterBootstrapper == null) {
        _encounterBootstrapper = FindFirstObjectByType<CombatEncounterBootstrapper>();
      }

      _encounterBootstrapper?.BeginEncounter();
    }

    private void HighlightSelectedSlot() {
      foreach (PartySlotView slot in _partySlotViews) {
        slot.Root.EnableInClassList("roster-party-slot--selected", slot.SlotIndex == _selectedPartySlotIndex);
      }
    }

    private int GetPartySlotForEcho(string echoId) {
      IReadOnlyList<PlayerRosterService.PartySlotInfo> slots = _rosterService.PartySlots;
      for (int i = 0; i < slots.Count; i++) {
        if (string.Equals(slots[i].EchoInstanceId, echoId, StringComparison.Ordinal)) {
          return i;
        }
      }

      return -1;
    }

    private bool TryGetSelectedEcho(out PlayerEchoData echo) {
      echo = null;
      if (string.IsNullOrEmpty(_selectedEchoId) || _rosterService == null) {
        return false;
      }

      return _rosterService.TryGetEcho(_selectedEchoId, out echo);
    }

    private static IEchoSkillTreeDefinition ResolveSkillTree(PlayerEchoData echo) {
      return echo?.Template?.ProgressionProfile?.SkillTree;
    }

    private int FindFirstAvailableSlot() {
      IReadOnlyList<PlayerRosterService.PartySlotInfo> slots = _rosterService.PartySlots;
      for (int i = 0; i < slots.Count; i++) {
        if (i >= _rosterService.MaxPartySize) {
          break;
        }

        PlayerRosterService.PartySlotInfo info = slots[i];
        if (!info.IsLocked && string.IsNullOrEmpty(info.EchoInstanceId)) {
          return i;
        }
      }

      return -1;
    }

    private void ReselectCurrentEcho() {
      if (_ownedListView == null || string.IsNullOrEmpty(_selectedEchoId)) {
        return;
      }

      int index = _ownedEchoItems.FindIndex(vm => vm.InstanceId == _selectedEchoId);
      if (index >= 0) {
        _ownedListView.selectedIndex = index;
      } else {
        _ownedListView.selectedIndex = -1;
        _selectedEchoId = string.Empty;
      }
    }

    private void UpdateActionButtons() {
      bool hasSelection = !string.IsNullOrEmpty(_selectedEchoId);
      PlayerEchoData selectedEcho = null;
      bool hasEcho = hasSelection && TryGetSelectedEcho(out selectedEcho);
      bool isInParty = hasEcho && _selectedPartySlotIndex >= 0;
      bool canAssign = hasEcho && !isInParty;
      bool canOpenSkillTree = hasEcho && ResolveSkillTree(selectedEcho) != null;

      _assignButton?.SetEnabled(canAssign);
      _removeButton?.SetEnabled(isInParty);
      _equipmentButton?.SetEnabled(hasEcho);
      _gambitButton?.SetEnabled(hasEcho);
      _skillTreeButton?.SetEnabled(canOpenSkillTree);
    }

    private readonly struct OwnedEchoViewModel {
      public OwnedEchoViewModel(PlayerEchoData echo, int partySlotIndex) {
        InstanceId = echo.InstanceId;
        DisplayName = echo.DisplayName;
        Level = echo.Level;
        IsInParty = partySlotIndex >= 0;
        PartySlotIndex = partySlotIndex;
      }

      public string InstanceId { get; }
      public string DisplayName { get; }
      public int Level { get; }
      public bool IsInParty { get; }
      public int PartySlotIndex { get; }
    }

    private readonly struct PartySlotView {
      public PartySlotView(VisualElement root, Label label, int slotIndex) {
        Root = root;
        Label = label;
        SlotIndex = slotIndex;
      }

      public VisualElement Root { get; }
      public Label Label { get; }
      public int SlotIndex { get; }
    }
  }
}



