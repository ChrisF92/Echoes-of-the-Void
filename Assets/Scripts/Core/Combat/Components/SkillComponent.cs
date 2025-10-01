using System;
using System.Collections.Generic;
using System.Linq;

using EchoesOfTheVoid.Core.Combat.Entities;
using EchoesOfTheVoid.Core.Combat.ScriptableObjects;
using EchoesOfTheVoid.Core.Combat.Wrappers;
using EchoesOfTheVoid.Core.Combat.Results;

namespace EchoesOfTheVoid.Core.Combat.Components {
  public class SkillComponent : CombatComponent {
    private readonly Dictionary<string, CombatSkill> _skills = new();
    private readonly Dictionary<string, int> _cooldowns = new();
    private ICombatant _owner;

    public override void Initialize(ICombatant owner) {
      _owner = owner;
    }

    public override void Update(float deltaTime) {
    }

    public void OnTurnEnd() {
      var keys = _cooldowns.Keys.ToList();
      foreach (string skillId in keys) {
        _cooldowns[skillId] = Math.Max(0, _cooldowns[skillId] - 1);
      }
    }

    public void LearnSkill(SkillScriptableObject skillData) {
      var skill = new CombatSkill(skillData);
      _skills[skillData.SkillId] = skill;
      _cooldowns[skillData.SkillId] = 0;
    }

    public bool CanUseSkill(string skillId) {
      return _skills.TryGetValue(skillId, out CombatSkill skill) && skill.CanUse(_owner) &&
             _cooldowns[skillId] <= 0 &&
             _owner.GetStat(StatType.Mana) >= skill.Data.ManaCost;
    }

    public SkillResult UseSkill(string skillId, ICombatant target = null) {
      if (!CanUseSkill(skillId)) {
        return SkillResult.Failed("Cannot use skill");
      }

      CombatSkill skill = _skills[skillId];
      SkillResult result = skill.Execute(_owner, target);

      if (result.IsSuccess) {
        int rawCooldown = skill.Data.CooldownTurns;
        _cooldowns[skillId] = rawCooldown > 0 ? rawCooldown + 1 : 0;
        _owner.ConsumeMana(skill.Data.ManaCost);
      }

      return result;
    }

    public IEnumerable<CombatSkill> GetAvailableSkills() {
      return _skills.Values.Where(s => CanUseSkill(s.Data.SkillId));
    }

    public int GetSkillCooldown(string skillId) {
      return _cooldowns.TryGetValue(skillId, out int cooldown) ? cooldown : 0;
    }
  }
}
