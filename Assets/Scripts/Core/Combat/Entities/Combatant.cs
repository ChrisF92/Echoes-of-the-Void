using System;
using System.Collections.Generic;
using UnityEngine;

using EchoesOfTheVoid.Core.Combat.Components;
using EchoesOfTheVoid.Core.Combat.Data;
using EchoesOfTheVoid.Core.Combat.ScriptableObjects;
using EchoesOfTheVoid.Core.Combat.Gambits;
using EchoesOfTheVoid.Core.Inventory.Data;
using EchoesOfTheVoid.Core.Inventory.Player;

namespace EchoesOfTheVoid.Core.Combat.Entities {
  public class Combatant : MonoBehaviour, ICombatant {
    [Header("Stats")]
    [SerializeField] private CombatStats _baseStats;
    [SerializeField] private CombatStats _currentStats;

    private readonly Dictionary<Type, CombatComponent> _components = new();
    private EquipmentComponent _equipmentComponent;
    private readonly Dictionary<StatType, StatModifierValue> _skillTreeModifiers = new();

    [field: Header("Basic Info")]
    [field: SerializeField]
    public string Name { get; private set; }
    public bool IsAlive => _currentStats.Health > 0;
    [field: SerializeField]
    public bool IsPlayerControlled { get; private set; } = true;
    [field: SerializeField]
    public bool IsAutoCombatEnabled { get; private set; }
    public bool IsDefending { get; private set; }
    public CombatTeam Team { get; private set; }

    public event Action OnDefeated;
    public event Action<int> OnDamaged;
    public event Action<int> OnHealed;
    public event Action<StatType, int, int> OnStatChanged;

    private void Awake() {
      InitializeStats();
    }

    private void Start() {
      InitializeComponents();
    }

    public void InitializeFromTemplate(CombatantSO template) {
      Name = template.DisplayName;
      IsPlayerControlled = template.IsPlayerControlled;
      OverrideBaseStats(template.BaseStats, true);

      InitializeComponents();
      SetupSkillComponent(template.StartingSkills);
      SetupInventoryComponent(template.StartingItems);
      SetupEquipmentComponent(template.StartingEquipment);
      SetupGambitComponent(template.GambitProfile);
    }

    private void InitializeStats() {
      if (_baseStats == null) {
        _baseStats = new CombatStats();
      }

      if (_currentStats == null) {
        _currentStats = CloneStats(_baseStats);
        return;
      }

      if (_currentStats.Health <= 0) {
        CopyStats(_baseStats, _currentStats);
      } else {
        ClampCurrentToBase(_currentStats, _baseStats);
      }
    }

    public void OverrideBaseStats(CombatStats baseStats, bool resetCurrentStats = true) {
      _baseStats = CloneStats(baseStats);

      if (_currentStats == null || resetCurrentStats) {
        _currentStats = CloneStats(_baseStats);
      } else {
        ClampCurrentToBase(_currentStats, _baseStats);
      }
    }

    private void InitializeComponents() {
      if (GetComponent<SkillComponent>() == null) {
        AddComponent(new SkillComponent());
      }

      if (GetComponent<InventoryComponent>() == null) {
        AddComponent(new InventoryComponent());
      }

      if (GetComponent<GambitComponent>() == null) {
        AddComponent(new GambitComponent());
      }

      if (GetComponent<EquipmentComponent>() == null) {
        AddComponent(new EquipmentComponent());
      }

      EquipmentComponent latestEquipmentComponent = GetComponent<EquipmentComponent>();
      if (_equipmentComponent != latestEquipmentComponent) {
        if (_equipmentComponent != null) {
          _equipmentComponent.OnModifiersChanged -= HandleEquipmentModifiersChanged;
        }

        _equipmentComponent = latestEquipmentComponent;
        if (_equipmentComponent != null) {
          _equipmentComponent.OnModifiersChanged += HandleEquipmentModifiersChanged;
          HandleEquipmentModifiersChanged();
        }
      }
    }

    private void SetupSkillComponent(List<SkillSO> startingSkills) {
      SkillComponent skillComponent = GetComponent<SkillComponent>();
      if (skillComponent != null) {
        foreach (SkillSO skill in startingSkills) {
          skillComponent.LearnSkill(skill);
        }
      }
    }

    private void SetupInventoryComponent(List<ItemStackData> startingItems) {
      InventoryComponent inventoryComponent = GetComponent<InventoryComponent>();
      if (inventoryComponent != null) {
        foreach (ItemStackData itemStack in startingItems) {
          _ = inventoryComponent.AddItem(itemStack.Item, itemStack.Quantity);
        }
      }
    }

    private void SetupEquipmentComponent(List<EquippedItemData> startingEquipment) {
      if (startingEquipment == null || startingEquipment.Count == 0) {
        return;
      }

      ApplyEquipmentLoadout(startingEquipment, suppressNotifications: true);
    }

    public void ApplyEquipmentLoadout(IEnumerable<EquippedItemData> loadout, bool suppressNotifications = true) {
      EquipmentComponent equipmentComponent = _equipmentComponent ?? GetComponent<EquipmentComponent>();
      equipmentComponent?.LoadFromSnapshot(loadout, suppressNotifications);
    }

    public void ApplyEquipmentFrom(PlayerEquipment playerEquipment, bool suppressNotifications = true) {
      EquipmentComponent equipmentComponent = _equipmentComponent ?? GetComponent<EquipmentComponent>();
      equipmentComponent?.LoadFromPlayerEquipment(playerEquipment, suppressNotifications);
    }


    private void SetupGambitComponent(GambitProfile profile) {
      GambitComponent gambitComponent = GetComponent<GambitComponent>();
      gambitComponent?.SetProfile(profile);
    }

    private void HandleEquipmentModifiersChanged() {
      if (_equipmentComponent == null) {
        return;
      }

      int maxHealth = GetMaxStat(StatType.Health);
      if (_currentStats.Health > maxHealth) {
        int oldHealth = _currentStats.Health;
        _currentStats.Health = maxHealth;
        OnStatChanged?.Invoke(StatType.Health, oldHealth, _currentStats.Health);
      }

      int maxMana = GetMaxStat(StatType.Mana);
      if (_currentStats.Mana > maxMana) {
        int oldMana = _currentStats.Mana;
        _currentStats.Mana = maxMana;
        OnStatChanged?.Invoke(StatType.Mana, oldMana, _currentStats.Mana);
      }
    }


    public void ApplyGambitProfile(IGambitRuleSource profile) {
      GambitComponent gambitComponent = GetComponent<GambitComponent>();
      gambitComponent?.SetProfileSource(profile);
    }

    public void ApplyGambitProfile(GambitProfileData profile) {
      ApplyGambitProfile(profile as IGambitRuleSource);
    }

    public int GetStat(StatType statType) {
      int value = statType switch {
        StatType.Health => _currentStats.Health,
        StatType.Mana => _currentStats.Mana,
        StatType.Attack => _currentStats.Attack,
        StatType.Defense => _currentStats.Defense,
        StatType.Speed => _currentStats.Speed,
        StatType.Luck => _currentStats.Luck,
        _ => 0
      };

      if (statType == StatType.Health) {
        int maxHealth = GetMaxStat(StatType.Health);
        if (_currentStats.Health > maxHealth) {
          int oldHealth = _currentStats.Health;
          _currentStats.Health = maxHealth;
          OnStatChanged?.Invoke(StatType.Health, oldHealth, _currentStats.Health);
          value = _currentStats.Health;
        }

        return value;
      }

      if (statType == StatType.Mana) {
        int maxMana = GetMaxStat(StatType.Mana);
        if (_currentStats.Mana > maxMana) {
          int oldMana = _currentStats.Mana;
          _currentStats.Mana = maxMana;
          OnStatChanged?.Invoke(StatType.Mana, oldMana, _currentStats.Mana);
          value = _currentStats.Mana;
        }

        return value;
      }

      EquipmentComponent equipment = _equipmentComponent ?? GetComponent<EquipmentComponent>();
      if (equipment != null) {
        EquipmentComponent.StatModifier modifier = equipment.GetModifier(statType);
        value = ApplyModifier(value, modifier.Additive, modifier.Percent);
      }

      if (_skillTreeModifiers.TryGetValue(statType, out StatModifierValue skillTreeModifier)) {
        value = ApplyModifier(value, skillTreeModifier.Additive, skillTreeModifier.Percent);
      }

      return Mathf.Max(0, value);
    }

    public int GetMaxStat(StatType statType) {
      int value = statType switch {
        StatType.Health => _baseStats.Health,
        StatType.Mana => _baseStats.Mana,
        StatType.Attack => _baseStats.Attack,
        StatType.Defense => _baseStats.Defense,
        StatType.Speed => _baseStats.Speed,
        StatType.Luck => _baseStats.Luck,
        _ => 0
      };

      EquipmentComponent equipment = _equipmentComponent ?? GetComponent<EquipmentComponent>();
      if (equipment != null) {
        EquipmentComponent.StatModifier modifier = equipment.GetModifier(statType);
        value = ApplyModifier(value, modifier.Additive, modifier.Percent);
      }

      if (_skillTreeModifiers.TryGetValue(statType, out StatModifierValue skillTreeModifier)) {
        value = ApplyModifier(value, skillTreeModifier.Additive, skillTreeModifier.Percent);
      }

      return Mathf.Max(0, value);
    }

    public void SetTeam(CombatTeam team) {
      Team = team;
    }

    public void SetAutoCombatEnabled(bool enabled) {
      IsAutoCombatEnabled = enabled;
    }

    public void SetDefending(bool defending) {
      IsDefending = defending;
    }

    public void TakeDamage(int damage) {
      if (!IsAlive) {
        return;
      }

      int incomingDamage = Math.Max(0, damage);
      int oldHealth = _currentStats.Health;
      int newHealth = Math.Max(0, _currentStats.Health - incomingDamage);
      int actualDamage = oldHealth - newHealth;

      _currentStats.Health = newHealth;

      if (actualDamage > 0) {
        OnDamaged?.Invoke(actualDamage);
        OnStatChanged?.Invoke(StatType.Health, oldHealth, _currentStats.Health);
      }

      if (_currentStats.Health == 0 && oldHealth > 0) {
        OnDefeated?.Invoke();
      }
    }

    public void Heal(int amount) {
      if (!IsAlive) {
        return;
      }

      int incomingHealing = Math.Max(0, amount);
      int oldHealth = _currentStats.Health;
      int maxHealth = GetMaxStat(StatType.Health);
      int newHealth = Math.Min(maxHealth, _currentStats.Health + incomingHealing);
      int actualHealing = newHealth - oldHealth;

      _currentStats.Health = newHealth;

      if (actualHealing > 0) {
        OnHealed?.Invoke(actualHealing);
        OnStatChanged?.Invoke(StatType.Health, oldHealth, _currentStats.Health);
      }
    }

    public void ConsumeMana(int amount) {
      int oldMana = _currentStats.Mana;
      _currentStats.Mana = Math.Max(0, _currentStats.Mana - amount);
      OnStatChanged?.Invoke(StatType.Mana, oldMana, _currentStats.Mana);
    }

    public bool CanUseSkill(string skillId) {
      return GetComponent<SkillComponent>()?.CanUseSkill(skillId) ?? false;
    }

    public void AddComponent<T>(T component) where T : CombatComponent {
      _components[typeof(T)] = component;
      component.Initialize(this);
    }

    public new T GetComponent<T>() where T : CombatComponent {
      return _components.TryGetValue(typeof(T), out CombatComponent component) ? component as T : null;
    }

    public void UpdateComponents(float deltaTime) {
      foreach (CombatComponent component in _components.Values) {
        component.Update(deltaTime);
      }
    }

    private static CombatStats CloneStats(CombatStats source) {
      return source != null ? source.Clone() : new CombatStats();
    }

    private static void CopyStats(CombatStats source, CombatStats destination) {
      if (destination == null) {
        return;
      }

      CombatStats baseline = CloneStats(source);
      destination.Health = baseline.Health;
      destination.Mana = baseline.Mana;
      destination.Attack = baseline.Attack;
      destination.Defense = baseline.Defense;
      destination.Speed = baseline.Speed;
      destination.Luck = baseline.Luck;
    }

    private static void ClampCurrentToBase(CombatStats current, CombatStats baseline) {
      if (current == null || baseline == null) {
        return;
      }

      current.Health = Mathf.Clamp(current.Health, 0, baseline.Health);
      current.Mana = Mathf.Clamp(current.Mana, 0, baseline.Mana);
      current.Attack = Mathf.Clamp(current.Attack, 0, baseline.Attack);
      current.Defense = Mathf.Clamp(current.Defense, 0, baseline.Defense);
      current.Speed = Mathf.Clamp(current.Speed, 0, baseline.Speed);
      current.Luck = Mathf.Clamp(current.Luck, 0, baseline.Luck);
    }

    public void ClearSkillTreeModifiers() {
      _skillTreeModifiers.Clear();
    }

    public void AddSkillTreeModifier(StatType statType, int additive, float percent) {
      if (!_skillTreeModifiers.TryGetValue(statType, out StatModifierValue totals)) {
        totals = default;
      }

      totals.Additive += additive;
      totals.Percent += percent;
      _skillTreeModifiers[statType] = totals;
    }

    public struct StatModifierValue {
      public int Additive;
      public float Percent;
    }

    private static int ApplyModifier(int baseValue, int additive, float percent) {
      float scaled = baseValue * (1f + percent);
      int adjusted = Mathf.RoundToInt(scaled);
      return Mathf.Max(0, adjusted + additive);
    }
  }
}








