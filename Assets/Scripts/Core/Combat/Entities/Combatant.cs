using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

using EchoesOfTheVoid.Core.Combat.Components;
using EchoesOfTheVoid.Core.Combat.Data;
using EchoesOfTheVoid.Core.Combat.ScriptableObjects;
using EchoesOfTheVoid.Core.Combat.Gambits;
using EchoesOfTheVoid.Core.Inventory.Data;

namespace EchoesOfTheVoid.Core.Combat.Entities
{
  public class Combatant : MonoBehaviour, ICombatant
  {
    [Header("Basic Info")]
    [FormerlySerializedAs("combatantName")]
    [SerializeField] private string _combatantName;
    [FormerlySerializedAs("isPlayerControlled")]
    [SerializeField] private bool _isPlayerControlled = true;

    [Header("Stats")]
    [FormerlySerializedAs("baseStats")]
    [SerializeField] private CombatStats _baseStats;
    [FormerlySerializedAs("currentStats")]
    [SerializeField] private CombatStats _currentStats;

    private readonly Dictionary<Type, CombatComponent> _components = new();
    private CombatTeam _team;
    private bool _isDefending;
    [SerializeField] private bool _isAutoCombatEnabled;

    public string Name => _combatantName;
    public bool IsAlive => _currentStats.health > 0;
    public bool IsPlayerControlled => _isPlayerControlled;
    public bool IsAutoCombatEnabled => _isAutoCombatEnabled;
    public bool IsDefending => _isDefending;
    public CombatTeam Team => _team;

    public event Action OnDefeated;
    public event Action<int> OnDamaged;
    public event Action<int> OnHealed;
    public event Action<StatType, int, int> OnStatChanged;

    private void Awake()
    {
      InitializeStats();
    }

    private void Start()
    {
      InitializeComponents();
    }

    public void InitializeFromTemplate(CombatantTemplateScriptableObject template)
    {
      _combatantName = template.displayName;
      _isPlayerControlled = template.isPlayerControlled;
      _baseStats = template.baseStats;
      _currentStats = new CombatStats
      {
        health = _baseStats.health,
        mana = _baseStats.mana,
        attack = _baseStats.attack,
        defense = _baseStats.defense,
        speed = _baseStats.speed,
        luck = _baseStats.luck
      };

      InitializeComponents();
      SetupSkillComponent(template.startingSkills);
      SetupInventoryComponent(template.startingItems);
      SetupGambitComponent(template.gambitProfile);
    }

    private void InitializeStats()
    {
      if (_baseStats == null)
      {
        _baseStats = new CombatStats();
      }

      if (_currentStats == null || _currentStats.health <= 0)
      {
        _currentStats = new CombatStats
        {
          health = _baseStats.health,
          mana = _baseStats.mana,
          attack = _baseStats.attack,
          defense = _baseStats.defense,
          speed = _baseStats.speed,
          luck = _baseStats.luck
        };
      }
    }

    private void InitializeComponents()
    {
      if (GetComponent<SkillComponent>() == null)
      {
        AddComponent(new SkillComponent());
      }

      if (GetComponent<InventoryComponent>() == null)
      {
        AddComponent(new InventoryComponent());
      }

      if (GetComponent<GambitComponent>() == null)
      {
        AddComponent(new GambitComponent());
      }
    }

    private void SetupSkillComponent(List<SkillScriptableObject> startingSkills)
    {
      var skillComponent = GetComponent<SkillComponent>();
      if (skillComponent != null)
      {
        foreach (var skill in startingSkills)
        {
          skillComponent.LearnSkill(skill);
        }
      }
    }

    private void SetupInventoryComponent(List<ItemStackData> startingItems)
    {
      var inventoryComponent = GetComponent<InventoryComponent>();
      if (inventoryComponent != null)
      {
        foreach (var itemStack in startingItems)
        {
          inventoryComponent.AddItem(itemStack.item, itemStack.quantity);
        }
      }
    }

    private void SetupGambitComponent(GambitProfile profile)
    {
      var gambitComponent = GetComponent<GambitComponent>();
      if (gambitComponent != null)
      {
        gambitComponent.SetProfile(profile);
      }
    }

    public void ApplyGambitProfile(IGambitRuleSource profile)
    {
      var gambitComponent = GetComponent<GambitComponent>();
      gambitComponent?.SetProfileSource(profile);
    }

    public void ApplyGambitProfile(GambitProfileData profile)
    {
      ApplyGambitProfile(profile as IGambitRuleSource);
    }

    public int GetStat(StatType statType)
    {
      return statType switch
      {
        StatType.Health => _currentStats.health,
        StatType.Mana => _currentStats.mana,
        StatType.Attack => _currentStats.attack,
        StatType.Defense => _currentStats.defense,
        StatType.Speed => _currentStats.speed,
        StatType.Luck => _currentStats.luck,
        _ => 0
      };
    }

    public int GetMaxStat(StatType statType)
    {
      return statType switch
      {
        StatType.Health => _baseStats.health,
        StatType.Mana => _baseStats.mana,
        StatType.Attack => _baseStats.attack,
        StatType.Defense => _baseStats.defense,
        StatType.Speed => _baseStats.speed,
        StatType.Luck => _baseStats.luck,
        _ => 0
      };
    }

    public void SetTeam(CombatTeam team)
    {
      _team = team;
    }

    public void SetAutoCombatEnabled(bool enabled)
    {
      _isAutoCombatEnabled = enabled;
    }

    public void SetDefending(bool defending)
    {
      _isDefending = defending;
    }

    public void TakeDamage(int damage)
    {
      if (!IsAlive)
      {
        return;
      }

      var actualDamage = Math.Max(0, damage);
      var oldHealth = _currentStats.health;
      _currentStats.health = Math.Max(0, _currentStats.health - actualDamage);

      OnDamaged?.Invoke(actualDamage);
      OnStatChanged?.Invoke(StatType.Health, oldHealth, _currentStats.health);

      if (_currentStats.health == 0)
      {
        OnDefeated?.Invoke();
      }
    }

    public void Heal(int amount)
    {
      if (!IsAlive)
      {
        return;
      }

      var oldHealth = _currentStats.health;
      _currentStats.health = Math.Min(_baseStats.health, _currentStats.health + amount);
      var actualHealing = _currentStats.health - oldHealth;

      if (actualHealing > 0)
      {
        OnHealed?.Invoke(actualHealing);
        OnStatChanged?.Invoke(StatType.Health, oldHealth, _currentStats.health);
      }
    }

    public void ConsumeMana(int amount)
    {
      var oldMana = _currentStats.mana;
      _currentStats.mana = Math.Max(0, _currentStats.mana - amount);
      OnStatChanged?.Invoke(StatType.Mana, oldMana, _currentStats.mana);
    }

    public bool CanUseSkill(string skillId)
    {
      return GetComponent<SkillComponent>()?.CanUseSkill(skillId) ?? false;
    }

    public void AddComponent<T>(T component) where T : CombatComponent
    {
      _components[typeof(T)] = component;
      component.Initialize(this);
    }

    public T GetComponent<T>() where T : CombatComponent
    {
      return _components.TryGetValue(typeof(T), out var component) ? component as T : null;
    }

    public void UpdateComponents(float deltaTime)
    {
      foreach (var component in _components.Values)
      {
        component.Update(deltaTime);
      }
    }
  }
}
