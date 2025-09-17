using System;
using EchoesOfTheVoid.Core;
using UnityEngine;

namespace EchoesOfTheVoid.Combat
{
  /// <summary>
  /// An AI-controlled combatant.
  /// </summary>
  [DisallowMultipleComponent]
  [AddComponentMenu("Combat/Enemy Character")]
  public sealed class EnemyCharacter : MonoBehaviour, ICombatant, IDamageable, IManaUser, IDefendable
  {
    [SerializeField] private string _displayName = "Enemy";
    [SerializeField] private int _maxHealth = 80;
    [SerializeField] private int _maxMana = 30;
    [SerializeField] private int _startingHealth = 80;
    [SerializeField] private int _startingMana = 10;

    public string Name => _displayName;
    public int Health => _health;
    public int MaxHealth => _maxHealth;
    public int Mana => _mana;
    public bool IsAlive => _health > 0;

    private int _health;
    private int _mana;

    // Simple defend stance state.
    private bool _isDefending;
    private int _defendTurns;
    private float _defendReduction;

    private void Awake()
    {
      _maxHealth = Mathf.Max(1, _maxHealth);
      _maxMana = Mathf.Max(0, _maxMana);
      _health = Mathf.Clamp(_startingHealth <= 0 ? _maxHealth : _startingHealth, 0, _maxHealth);
      _mana = Mathf.Clamp(_startingMana, 0, _maxMana);
    }

    public void BeginTurn()
    {
      // Placeholder for AI behavior trigger.
      Debug.Log($"{Name} turn begins.");
    }

    public void EndTurn()
    {
      // Placeholder for AI cleanup.
      Debug.Log($"{Name} turn ends.");
    }

    public void PerformAction(ICombatAction action, ICombatant target)
    {
      if (action == null)
      {
        throw new ArgumentNullException(nameof(action));
      }

      if (target == null)
      {
        throw new ArgumentNullException(nameof(target));
      }

      if (!IsAlive)
      {
        Debug.LogWarning($"{Name} cannot act while defeated.");
        return;
      }

      try
      {
        action.Execute(this, target);
      }
      catch (Exception e)
      {
        Debug.LogException(e);
      }
    }

    // Basic helpers for actions and tests.
    public void ApplyDamage(int amount)
    {
      if (amount <= 0)
      {
        return;
      }
      _health = Mathf.Max(0, _health - amount);
    }

    public void RestoreHealth(int amount)
    {
      if (amount <= 0)
      {
        return;
      }
      _health = Mathf.Clamp(_health + amount, 0, _maxHealth);
    }

    public bool TryConsumeMana(int amount)
    {
      if (amount <= 0)
      {
        return true;
      }
      if (_mana < amount)
      {
        return false;
      }
      _mana -= amount;
      return true;
    }

    public void RestoreMana(int amount)
    {
      if (amount <= 0)
      {
        return;
      }
      _mana = Mathf.Clamp(_mana + amount, 0, _maxMana);
    }

    public void ApplyDefense(int turns, float damageReduction)
    {
      _defendTurns = Mathf.Max(1, turns);
      _defendReduction = Mathf.Clamp01(damageReduction);
      _isDefending = true;
    }

    public int MitigateDamage(int incomingDamage)
    {
      if (!_isDefending || incomingDamage <= 0)
      {
        return Mathf.Max(0, incomingDamage);
      }

      int mitigated = Mathf.RoundToInt(incomingDamage * (1f - _defendReduction));
      _defendTurns--;
      if (_defendTurns <= 0)
      {
        _isDefending = false;
        _defendReduction = 0f;
      }
      return Mathf.Max(0, mitigated);
    }
  }
}
