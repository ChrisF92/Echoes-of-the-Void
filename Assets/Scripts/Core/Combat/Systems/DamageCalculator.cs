using System;
using EchoesOfTheVoid.Core.Combat.Entities;
using UnityEngine;

namespace EchoesOfTheVoid.Core.Combat.Systems {
  /// <summary>
  /// Handles all damage calculation logic with extensible formula system.
  /// </summary>
  public class DamageCalculator {
    private readonly DamageFormulaConfig _config;

    public DamageCalculator(DamageFormulaConfig customConfig = null) {
      _config = customConfig ?? DamageFormulaConfig.Default;
    }

    /// <summary>
    /// Calculate physical attack damage.
    /// </summary>
    public int CalculatePhysicalDamage(ICombatant attacker, ICombatant target) {
      int attack = attacker.GetStat(StatType.Attack);
      int defense = target.GetStat(StatType.Defense);

      // Apply defense stance modifier
      if (target.IsDefending) {
        defense = Mathf.RoundToInt(defense * _config.DefenseStanceMultiplier);
      }

      // Base formula: Attack - Defense (with minimum damage)
      int baseDamage = Mathf.Max(_config.MinimumDamage, attack - defense);

      // Apply variance
      float variance = UnityEngine.Random.Range(
        _config.DamageVarianceMin,
        _config.DamageVarianceMax
      );
      int finalDamage = Mathf.RoundToInt(baseDamage * variance);

      // Critical hit check
      if (RollCriticalHit(attacker)) {
        finalDamage = Mathf.RoundToInt(finalDamage * _config.CriticalMultiplier);
      }

      return Mathf.Max(_config.MinimumDamage, finalDamage);
    }

    /// <summary>
    /// Calculate skill-based damage with stat scaling.
    /// </summary>
    public int CalculateSkillDamage(
      int baseValue,
      float statScaling,
      StatType scalingStat,
      ICombatant user,
      AnimationCurve curve = null) {
      int damage = baseValue;

      // Apply stat scaling
      if (statScaling > 0f) {
        int statValue = user.GetStat(scalingStat);
        damage += Mathf.RoundToInt(statValue * statScaling);
      }

      // Apply curve modifier
      if (curve != null) {
        damage = Mathf.RoundToInt(damage * curve.Evaluate(1f));
      }

      return Mathf.Max(1, damage);
    }

    /// <summary>
    /// Calculate healing amount with potential overhealing prevention.
    /// </summary>
    public int CalculateHealing(int baseHealing, ICombatant target) {
      int currentHealth = target.GetStat(StatType.Health);
      int maxHealth = target.GetMaxStat(StatType.Health);

      if (!_config.AllowOverhealing) {
        int availableHealing = maxHealth - currentHealth;
        return Mathf.Min(baseHealing, availableHealing);
      }

      return baseHealing;
    }

    /// <summary>
    /// Check if attack critically hits.
    /// </summary>
    private bool RollCriticalHit(ICombatant attacker) {
      if (!_config.EnableCriticals) {
        return false;
      }

      int luck = attacker.GetStat(StatType.Luck);
      float critChance = _config.BaseCriticalChance + (luck * _config.LuckCriticalBonus);

      return UnityEngine.Random.value < critChance;
    }

    /// <summary>
    /// Apply elemental or type-based damage modifiers (extensible).
    /// </summary>
    public float GetDamageModifier(DamageType damageType, DamageType targetResistance) {
      // Placeholder for elemental system
      // You can expand this with a weakness/resistance matrix
      return 1f;
    }
  }

  /// <summary>
  /// Configuration for damage formulas. Can be ScriptableObject for designer control.
  /// </summary>
  [Serializable]
  public class DamageFormulaConfig {
    public int MinimumDamage = 1;
    public float DamageVarianceMin = 0.9f;
    public float DamageVarianceMax = 1.1f;
    public float DefenseStanceMultiplier = 1.5f;

    public bool EnableCriticals = true;
    public float BaseCriticalChance = 0.05f;
    public float LuckCriticalBonus = 0.005f;
    public float CriticalMultiplier = 1.5f;

    public bool AllowOverhealing = false;

    public static DamageFormulaConfig Default => new() {
      MinimumDamage = 1,
      DamageVarianceMin = 0.9f,
      DamageVarianceMax = 1.1f,
      DefenseStanceMultiplier = 1.5f,
      EnableCriticals = true,
      BaseCriticalChance = 0.05f,
      LuckCriticalBonus = 0.005f,
      CriticalMultiplier = 1.5f,
      AllowOverhealing = false
    };
  }

  public enum DamageType {
    Physical,
    Magical,
    Fire,
    Ice,
    Lightning,
    Dark,
    Holy,
    True // Ignores defense
  }
}