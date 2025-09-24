using System;
using System.Collections.Generic;
using System.Linq;
using EchoesOfTheVoid.Core.Combat;

using EchoesOfTheVoid.Core.Combat.Entities;
using EchoesOfTheVoid.Core.Combat.ScriptableObjects;
using EchoesOfTheVoid.Core.Combat.Wrappers;
using EchoesOfTheVoid.Core.Combat.Results;

namespace EchoesOfTheVoid.Core.Combat.Components
{
public class SkillComponent : CombatComponent
  {
    private readonly Dictionary<string, CombatSkill> _skills = new();
    private readonly Dictionary<string, int> _cooldowns = new();
    private ICombatant _owner;

    public override void Initialize(ICombatant owner)
    {
      _owner = owner;
    }

    public override void Update(float deltaTime)
    {
    }

    public void OnTurnEnd()
    {
      var keys = _cooldowns.Keys.ToList();
      foreach (var skillId in keys)
      {
        _cooldowns[skillId] = Math.Max(0, _cooldowns[skillId] - 1);
      }
    }

    public void LearnSkill(SkillScriptableObject skillData)
    {
      var skill = new CombatSkill(skillData);
      _skills[skillData.skillId] = skill;
      _cooldowns[skillData.skillId] = 0;
    }

    public bool CanUseSkill(string skillId)
    {
      if (!_skills.TryGetValue(skillId, out var skill))
      {
        return false;
      }

      return skill.CanUse(_owner) &&
             _cooldowns[skillId] <= 0 &&
             _owner.GetStat(StatType.Mana) >= skill.Data.manaCost;
    }

    public SkillResult UseSkill(string skillId, ICombatant target = null)
    {
      if (!CanUseSkill(skillId))
      {
        return SkillResult.Failed("Cannot use skill");
      }

      var skill = _skills[skillId];
      var result = skill.Execute(_owner, target);

      if (result.IsSuccess)
      {
        var rawCooldown = skill.Data.cooldownTurns;
        _cooldowns[skillId] = rawCooldown > 0 ? rawCooldown + 1 : 0;
        _owner.ConsumeMana(skill.Data.manaCost);
      }

      return result;
    }

    public IEnumerable<CombatSkill> GetAvailableSkills()
    {
      return _skills.Values.Where(s => CanUseSkill(s.Data.skillId));
    }

    public int GetSkillCooldown(string skillId)
    {
      return _cooldowns.TryGetValue(skillId, out var cooldown) ? cooldown : 0;
    }
  }
}
