using System;
using System.Collections.Generic;
using EchoesOfTheVoid.Core;
using EchoesOfTheVoid.Combat;
using EchoesOfTheVoid.Combat.Actions;
using UnityEngine;

namespace EchoesOfTheVoid.Skills
{
  /// <summary>
  /// Provides usable skill actions for the current combat context.
  /// Uses a serialized list of skill definitions (name + mana cost) and
  /// filters based on the user's current mana if they implement <see cref="IManaUser"/>.
  /// </summary>
  [DisallowMultipleComponent]
  public sealed class SkillManager : MonoBehaviour
  {
    [Serializable]
    public sealed class SkillDefinition
    {
      public string Name = "Skill";
      public int ManaCost = 5;
    }

    [SerializeField] private List<SkillDefinition> _skills = new List<SkillDefinition>
    {
      new SkillDefinition { Name = "Fireball", ManaCost = 5 },
      new SkillDefinition { Name = "Ice Shard", ManaCost = 3 },
      new SkillDefinition { Name = "Heal", ManaCost = 4 },
    };

    /// <summary>
    /// Returns skill actions that are currently usable by <paramref name="user"/>.
    /// Excludes skills that exceed current mana for <see cref="IManaUser"/>.
    /// </summary>
    public List<ICombatAction> GetUsableSkills(ICombatant user, TargetingSystem targeting)
    {
      var results = new List<ICombatAction>();
      if (user == null)
      {
        return results;
      }

      int currentMana = int.MaxValue;
      if (user is IManaUser manaUser)
      {
        currentMana = Mathf.Max(0, manaUser.Mana);
      }

      foreach (SkillDefinition def in _skills)
      {
        var action = new UseSkillAction(def.Name, new NoopSkillEffect(def.ManaCost));

        // Filter by mana if applicable.
        if (currentMana < action.ManaCost)
        {
          continue;
        }

        // Optionally filter skills that have no valid targets.
        if (targeting != null)
        {
          var candidates = targeting.GetValidTargets(action, user);
          if (candidates == null || candidates.Count == 0)
          {
            continue;
          }
        }

        results.Add(action);
      }
      return results;
    }

    private sealed class NoopSkillEffect : ISkillEffect
    {
      public int ManaCost { get; }
      public NoopSkillEffect(int manaCost)
      {
        ManaCost = Mathf.Max(0, manaCost);
      }
      public void Execute(ICombatant user, ICombatant target) { }
    }
  }
}

