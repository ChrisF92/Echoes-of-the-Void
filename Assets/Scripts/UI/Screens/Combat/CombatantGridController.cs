using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

using EchoesOfTheVoid.Core.Combat;
using EchoesOfTheVoid.Core.Combat.Entities;
using EchoesOfTheVoid.Core.Combat.ScriptableObjects;

namespace EchoesOfTheVoid.UI.Combat {
  /// <summary>
  /// Handles the presentation and state of combatant slots on the combat screen.
  /// </summary>
  public sealed class CombatantGridController {
    private readonly Dictionary<VisualElement, SlotVisualCache> _slotCache = new();
    private readonly Dictionary<Combatant, CombatantEventSubscription> _combatantEventSubscriptions = new();
    private readonly Dictionary<Combatant, CombatantUIData> _combatantUIData = new();
    private readonly Dictionary<Combatant, VisualElement> _combatantSlots = new();
    private readonly Dictionary<Combatant, Vector2Int> _combatantGridPositions = new();
    private readonly Dictionary<Combatant, CombatantSO> _combatantTemplates = new();

    private readonly List<VisualElement> _playerSlots = new(9);
    private readonly List<VisualElement> _enemySlots = new(9);
    private readonly List<Combatant> _playerTeam = new();
    private readonly List<Combatant> _enemyTeam = new();

    public IReadOnlyList<Combatant> PlayerTeam => _playerTeam;
    public IReadOnlyList<Combatant> EnemyTeam => _enemyTeam;

    public event Action<Combatant> OnCombatantClicked;
    public event Action<Combatant, bool> OnAutoToggleRequested;

    public void Initialize(VisualElement playerGrid, VisualElement enemyGrid) {
      _slotCache.Clear();
      ConfigureGrid(playerGrid, _playerSlots, true);
      ConfigureGrid(enemyGrid, _enemySlots, false);
      ClearSlotVisuals();
    }

    public void Clear() {
      UnsubscribeAllCombatantEvents();
      _playerTeam.Clear();
      _enemyTeam.Clear();
      _combatantSlots.Clear();
      _combatantGridPositions.Clear();
      _combatantUIData.Clear();
      _combatantTemplates.Clear();
      ClearSlotVisuals();
    }

    public void SetPlayerTeam(IReadOnlyList<Combatant> combatants) {
      SetTeamInternal(combatants, _playerSlots, _playerTeam);
    }

    public void SetEnemyTeam(IReadOnlyList<Combatant> combatants) {
      SetTeamInternal(combatants, _enemySlots, _enemyTeam);
    }

    public void AddCombatant(Combatant combatant, Vector2Int gridPosition) {
      if (combatant == null) {
        return;
      }

      List<VisualElement> slots = combatant.IsPlayerControlled ? _playerSlots : _enemySlots;
      VisualElement slot = GetSlotAt(slots, gridPosition);
      if (slot == null) {
        return;
      }

      AddCombatantInternal(combatant, slot, gridPosition);

      List<Combatant> team = combatant.IsPlayerControlled ? _playerTeam : _enemyTeam;
      if (!team.Contains(combatant)) {
        team.Add(combatant);
      }
    }

    public void RemoveCombatant(Combatant combatant) {
      if (combatant == null) {
        return;
      }

      if (_combatantSlots.TryGetValue(combatant, out VisualElement slot)) {
        ClearSlot(slot);
      }

      _ = _combatantSlots.Remove(combatant);
      _ = _combatantGridPositions.Remove(combatant);
      _ = _combatantUIData.Remove(combatant);
      _ = _playerTeam.Remove(combatant);
      _ = _enemyTeam.Remove(combatant);

      UnsubscribeCombatantEvents(combatant);
    }

    public void UpdateCombatant(Combatant combatant) {
      if (combatant == null) {
        return;
      }

      if (!_combatantGridPositions.TryGetValue(combatant, out Vector2Int gridPos)) {
        gridPos = new Vector2Int(-1, -1);
      }

      Sprite portrait = ResolvePortrait(combatant);
      if (!_combatantUIData.TryGetValue(combatant, out CombatantUIData data)) {
        data = new CombatantUIData(combatant, gridPos);
        _combatantUIData[combatant] = data;
      } else {
        data.UpdateFromCombatant(combatant, gridPos, portrait);
      }

      ApplyToSlot(combatant, data);
    }

    public void RegisterCombatantTemplate(Combatant combatant, CombatantSO template) {
      if (combatant == null || template == null) {
        return;
      }

      _combatantTemplates[combatant] = template;
      UpdateCombatant(combatant);
    }

    public Vector2Int GetGridPosition(Combatant combatant) {
      return combatant != null && _combatantGridPositions.TryGetValue(combatant, out Vector2Int position)
        ? position
        : new Vector2Int(-1, -1);
    }

    public void HighlightTargets(
      IReadOnlyCollection<Combatant> validTargets,
      IReadOnlyCollection<Combatant> selectedTargets) {
      HashSet<Combatant> validSet = validTargets != null
        ? new HashSet<Combatant>(validTargets.Where(static c => c != null && c.IsAlive))
        : new HashSet<Combatant>();
      HashSet<Combatant> selectedSet = selectedTargets != null
        ? new HashSet<Combatant>(selectedTargets.Where(static c => c != null))
        : new HashSet<Combatant>();

      foreach (SlotVisualCache cache in _slotCache.Values) {
        if (cache.Root == null) {
          continue;
        }

        cache.Root.RemoveFromClassList("valid-target");
        cache.Root.RemoveFromClassList("invalid-target");
        cache.Root.RemoveFromClassList("selected-target");

        if (cache.Combatant == null || !cache.Combatant.IsAlive) {
          continue;
        }

        if (validSet.Count > 0) {
          if (validSet.Contains(cache.Combatant)) {
            cache.Root.AddToClassList("valid-target");
          } else {
            cache.Root.AddToClassList("invalid-target");
          }
        }

        if (selectedSet.Contains(cache.Combatant)) {
          cache.Root.AddToClassList("selected-target");
        }
      }
    }

    public void SelectTarget(Combatant combatant) {
      foreach (SlotVisualCache cache in _slotCache.Values) {
        cache.Root?.RemoveFromClassList("selected-target");
      }

      if (combatant == null || !_combatantSlots.TryGetValue(combatant, out VisualElement slot)) {
        return;
      }

      if (_slotCache.TryGetValue(slot, out SlotVisualCache selectedCache) && selectedCache.Root != null) {
        selectedCache.Root.AddToClassList("selected-target");
      }
    }

    public void ClearTargetHighlights() {
      foreach (SlotVisualCache cache in _slotCache.Values) {
        if (cache.Root == null) {
          continue;
        }

        cache.Root.RemoveFromClassList("valid-target");
        cache.Root.RemoveFromClassList("invalid-target");
        cache.Root.RemoveFromClassList("selected-target");
      }
    }

    public void SetCurrentTurn(Combatant combatant) {
      foreach (SlotVisualCache cache in _slotCache.Values) {
        if (cache.Root == null) {
          continue;
        }

        if (cache.Combatant == combatant) {
          cache.Root.AddToClassList("current-turn");
        } else {
          cache.Root.RemoveFromClassList("current-turn");
        }
      }
    }

    public void Dispose() {
      UnsubscribeAllCombatantEvents();
    }

    private void ConfigureGrid(VisualElement gridRoot, List<VisualElement> slotList, bool isPlayerGrid) {
      slotList.Clear();
      if (gridRoot == null) {
        return;
      }

      string prefix = isPlayerGrid ? "player-slot-" : "enemy-slot-";
      for (int index = 0; index < 9; index++) {
        VisualElement slot = gridRoot.Q<VisualElement>($"{prefix}{index}");
        if (slot == null) {
          continue;
        }

        slotList.Add(slot);
        CacheSlot(slot, new Vector2Int(index % 3, index / 3), isPlayerGrid);
      }
    }

    private void CacheSlot(VisualElement slot, Vector2Int gridPosition, bool isPlayerGrid) {
      if (slot == null || _slotCache.ContainsKey(slot)) {
        return;
      }

      var cache = new SlotVisualCache {
        Root = slot,
        GridPosition = gridPosition,
        IsPlayerSlot = isPlayerGrid,
        NameLabel = slot.Q<Label>(className: "combatant-name"),
        HealthBar = slot.Q<ProgressBar>(className: "health-bar"),
        Portrait = slot.Q<VisualElement>(className: "portrait")
      };

      if (isPlayerGrid) {
        cache.AutoToggle = slot.Q<Button>("auto-toggle");
        if (cache.AutoToggle != null) {
          cache.AutoToggle.SetEnabled(false);
          cache.AutoToggle.text = "Manual";
          cache.AutoToggle.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
          cache.AutoToggle.RegisterCallback<PointerUpEvent>(evt => evt.StopPropagation());
          cache.AutoToggle.RegisterCallback<ClickEvent>(evt => {
            evt.StopPropagation();
            Combatant combatant = cache.Combatant;
            if (combatant == null || !combatant.IsAlive) {
              return;
            }

            bool nextState = !combatant.IsAutoCombatEnabled;
            OnAutoToggleRequested?.Invoke(combatant, nextState);
          });
        }
      }

      _slotCache[slot] = cache;

      slot.RegisterCallback<ClickEvent>(_ => {
        Combatant combatant = cache.Combatant;
        OnCombatantClicked?.Invoke(combatant);
      });
    }

    private void ClearSlotVisuals() {
      foreach (SlotVisualCache cache in _slotCache.Values) {
        ClearSlot(cache.Root);
      }
    }

    private void SetTeamInternal(IReadOnlyList<Combatant> combatants, List<VisualElement> slots, List<Combatant> team) {
      foreach (VisualElement slot in slots) {
        ClearSlot(slot);
      }

      team.Clear();

      if (combatants == null) {
        return;
      }

      for (int i = 0; i < combatants.Count && i < slots.Count; i++) {
        Combatant combatant = combatants[i];
        VisualElement slot = slots[i];
        Vector2Int gridPos = _slotCache.TryGetValue(slot, out SlotVisualCache cache) ? cache.GridPosition : new Vector2Int(i % 3, i / 3);
        AddCombatantInternal(combatant, slot, gridPos);
        if (combatant != null) {
          team.Add(combatant);
        }
      }
    }

    private void AddCombatantInternal(Combatant combatant, VisualElement slot, Vector2Int gridPosition) {
      if (combatant == null || slot == null) {
        return;
      }

      _combatantSlots[combatant] = slot;
      _combatantGridPositions[combatant] = gridPosition;

      if (!_combatantUIData.TryGetValue(combatant, out CombatantUIData data)) {
        data = new CombatantUIData(combatant, gridPosition);
        _combatantUIData[combatant] = data;
      } else {
        Sprite portrait = ResolvePortrait(combatant);
        data.UpdateFromCombatant(combatant, gridPosition, portrait);
      }

      PopulateSlot(slot, combatant, data);
      SubscribeCombatantEvents(combatant);
    }

    private void PopulateSlot(VisualElement slot, Combatant combatant, CombatantUIData data) {
      if (slot == null || !_slotCache.TryGetValue(slot, out SlotVisualCache cache)) {
        return;
      }

      cache.Combatant = combatant;
      slot.RemoveFromClassList("is-empty");
      slot.AddToClassList("combatant-alive");
      slot.RemoveFromClassList("combatant-dead");

      ApplyDataToCache(cache, data);
    }

    private void ApplyToSlot(Combatant combatant, CombatantUIData data) {
      if (combatant == null || data == null) {
        return;
      }

      if (!_combatantSlots.TryGetValue(combatant, out VisualElement slot)) {
        return;
      }

      if (!_slotCache.TryGetValue(slot, out SlotVisualCache cache)) {
        return;
      }

      cache.Combatant = combatant;
      ApplyDataToCache(cache, data);
    }

    private void ApplyDataToCache(SlotVisualCache cache, CombatantUIData data) {
      if (cache.NameLabel != null) {
        cache.NameLabel.bindingPath = nameof(CombatantUIData.Name);
        cache.NameLabel.dataSource = data;
        cache.NameLabel.text = data.Name;
      }

      if (cache.HealthBar != null) {
        cache.HealthBar.bindingPath = nameof(CombatantUIData.HPPercentage);
        cache.HealthBar.dataSource = data;
        cache.HealthBar.lowValue = 0f;
        cache.HealthBar.highValue = 1f;
        cache.HealthBar.value = data.HPPercentage;
        cache.HealthBar.title = $"{data.CurrentHP}/{data.MaxHP}";
      }

      if (cache.Portrait != null) {
        if (data.Portrait != null) {
          cache.Portrait.style.backgroundImage = new StyleBackground(data.Portrait);
          cache.Portrait.RemoveFromClassList("portrait-empty");
        } else {
          cache.Portrait.style.backgroundImage = StyleKeyword.Null;
          cache.Portrait.AddToClassList("portrait-empty");
        }
      }

      if (cache.Root != null) {
        if (data.IsAlive) {
          cache.Root.AddToClassList("combatant-alive");
          cache.Root.RemoveFromClassList("combatant-dead");
        } else {
          cache.Root.AddToClassList("combatant-dead");
          cache.Root.RemoveFromClassList("combatant-alive");
        }

        if (data.IsDefending) {
          cache.Root.AddToClassList("combatant-defending");
        } else {
          cache.Root.RemoveFromClassList("combatant-defending");
        }
      }

      UpdateAutoToggleVisual(cache, data);
    }

    private void ClearSlot(VisualElement slot) {
      if (slot == null || !_slotCache.TryGetValue(slot, out SlotVisualCache cache)) {
        return;
      }

      cache.Combatant = null;

      slot.AddToClassList("is-empty");
      slot.RemoveFromClassList("combatant-alive");
      slot.RemoveFromClassList("combatant-dead");
      slot.RemoveFromClassList("valid-target");
      slot.RemoveFromClassList("invalid-target");
      slot.RemoveFromClassList("selected-target");
      slot.RemoveFromClassList("current-turn");
      slot.RemoveFromClassList("combatant-defending");

      if (cache.NameLabel != null) {
        cache.NameLabel.text = "-";
      }

      if (cache.AutoToggle != null) {
        cache.AutoToggle.text = "Manual";
        cache.AutoToggle.RemoveFromClassList("is-active");
        cache.AutoToggle.SetEnabled(false);
        cache.AutoToggle.tooltip = string.Empty;
        cache.AutoToggle.style.display = DisplayStyle.None;
      }

      if (cache.HealthBar != null) {
        cache.HealthBar.value = 0f;
        cache.HealthBar.title = string.Empty;
      }

      if (cache.Portrait != null) {
        cache.Portrait.style.backgroundImage = StyleKeyword.Null;
        cache.Portrait.AddToClassList("portrait-empty");
      }
    }

    private VisualElement GetSlotAt(List<VisualElement> slots, Vector2Int gridPosition) {
      int index = (gridPosition.y * 3) + gridPosition.x;
      return index >= 0 && index < slots.Count ? slots[index] : null;
    }

    private Sprite ResolvePortrait(Combatant combatant) {
      if (combatant == null) {
        return null;
      }

      if (_combatantTemplates.TryGetValue(combatant, out CombatantSO template) && template != null) {
        return template.Portrait;
      }

      return null;
    }

    private void SubscribeCombatantEvents(Combatant combatant) {
      if (combatant == null) {
        return;
      }

      UnsubscribeCombatantEvents(combatant);

      var subscription = new CombatantEventSubscription {
        DamagedHandler = damage => UpdateCombatant(combatant),
        HealedHandler = heal => UpdateCombatant(combatant),
        DefeatedHandler = () => UpdateCombatant(combatant),
        StatChangedHandler = (stat, _, _) => {
          if (stat == StatType.Health) {
            UpdateCombatant(combatant);
          }
        }
      };

      combatant.OnDamaged += subscription.DamagedHandler;
      combatant.OnHealed += subscription.HealedHandler;
      combatant.OnDefeated += subscription.DefeatedHandler;
      combatant.OnStatChanged += subscription.StatChangedHandler;

      _combatantEventSubscriptions[combatant] = subscription;
    }

    private void UnsubscribeCombatantEvents(Combatant combatant) {
      if (combatant == null) {
        return;
      }

      if (!_combatantEventSubscriptions.TryGetValue(combatant, out CombatantEventSubscription subscription)) {
        return;
      }

      combatant.OnDamaged -= subscription.DamagedHandler;
      combatant.OnHealed -= subscription.HealedHandler;
      combatant.OnDefeated -= subscription.DefeatedHandler;
      combatant.OnStatChanged -= subscription.StatChangedHandler;

      _combatantEventSubscriptions.Remove(combatant);
    }

    private void UnsubscribeAllCombatantEvents() {
      foreach (Combatant combatant in _combatantEventSubscriptions.Keys.ToList()) {
        UnsubscribeCombatantEvents(combatant);
      }

      _combatantEventSubscriptions.Clear();
    }

    private void UpdateAutoToggleVisual(SlotVisualCache cache, CombatantUIData data) {
      if (cache == null || !cache.IsPlayerSlot || cache.AutoToggle == null) {
        return;
      }

      Combatant combatant = cache.Combatant ?? data?.SourceCombatant;
      bool hasCombatant = combatant != null;
      bool isAlive = combatant != null && combatant.IsAlive;
      bool isAutoEnabled = data != null ? data.IsAutoEnabled : (combatant?.IsAutoCombatEnabled ?? false);

      cache.AutoToggle.style.display = hasCombatant ? DisplayStyle.Flex : DisplayStyle.None;
      cache.AutoToggle.text = isAutoEnabled ? "Auto" : "Manual";

      if (isAutoEnabled) {
        cache.AutoToggle.AddToClassList("is-active");
      } else {
        cache.AutoToggle.RemoveFromClassList("is-active");
      }

      cache.AutoToggle.SetEnabled(isAlive);
      cache.AutoToggle.tooltip = !hasCombatant
        ? string.Empty
        : isAlive
          ? isAutoEnabled
            ? "Gambits enabled for this combatant."
            : "Click to enable gambits for this combatant."
          : "Combatant unavailable.";
    }

    private sealed class SlotVisualCache {
      public VisualElement Root;
      public Vector2Int GridPosition;
      public bool IsPlayerSlot;
      public Label NameLabel;
      public ProgressBar HealthBar;
      public VisualElement Portrait;
      public Combatant Combatant;
      public Button AutoToggle;
    }

    private sealed class CombatantEventSubscription {
      public Action<int> DamagedHandler;
      public Action<int> HealedHandler;
      public Action DefeatedHandler;
      public Action<StatType, int, int> StatChangedHandler;
    }
  }
}
