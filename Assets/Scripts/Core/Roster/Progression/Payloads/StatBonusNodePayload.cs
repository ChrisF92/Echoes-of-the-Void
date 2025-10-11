using System;
using System.Collections.Generic;
using EchoesOfTheVoid.Core.Combat;
using EchoesOfTheVoid.Core.Combat.Entities;
using EchoesOfTheVoid.Core.Roster.Data;
using UnityEngine;

namespace EchoesOfTheVoid.Core.Roster.Progression.Payloads {
  [CreateAssetMenu(fileName = "StatBonusNodePayload", menuName = "Roster/Progression/Payloads/Stat Bonus")]
  public class StatBonusNodePayload : EchoSkillNodePayload {
    [SerializeField] private List<StatBonus> _bonuses = new();

    public IReadOnlyList<StatBonus> Bonuses => _bonuses;

    public override void Apply(PlayerEchoData echo, Combatant combatant) {
      if (combatant == null || _bonuses == null || _bonuses.Count == 0) {
        return;
      }

      for (int i = 0; i < _bonuses.Count; i++) {
        StatBonus bonus = _bonuses[i];
        if (!bonus.HasEffect) {
          continue;
        }

        combatant.AddSkillTreeModifier(bonus.Stat, bonus.FlatBonus, bonus.PercentBonus);
      }
    }

    [Serializable]
    public struct StatBonus {
      [SerializeField] private StatType _stat;
      [SerializeField] private int _flatBonus;
      [SerializeField] [Range(-1f, 5f)] private float _percentBonus;

      public StatType Stat => _stat;
      public int FlatBonus => _flatBonus;
      public float PercentBonus => _percentBonus;
      public bool HasEffect => _flatBonus != 0 || Math.Abs(_percentBonus) > 0.0001f;
    }
  }
}
