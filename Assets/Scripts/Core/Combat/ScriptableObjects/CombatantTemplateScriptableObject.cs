using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;

using EchoesOfTheVoid.Core.Combat.Data;
using EchoesOfTheVoid.Core.Combat.Gambits;
using EchoesOfTheVoid.Core.Combat.Gambits.Blocks.Implementations;
using EchoesOfTheVoid.Core.Inventory.Data;

namespace EchoesOfTheVoid.Core.Combat.ScriptableObjects
{
  [CreateAssetMenu(fileName = "New Combatant Template", menuName = "Combat/Combatant Template")]
  public class CombatantTemplateScriptableObject : ScriptableObject
  {
    [Header("Basic Info")]
    public string combatantId;
    public string displayName;
    public Sprite portrait;
    public GameObject combatPrefab;

    [Header("Base Stats")]
    public CombatStats baseStats;

    [Header("Starting Skills")]
    public List<SkillScriptableObject> startingSkills = new();

    [Header("Starting Items")]
    public List<ItemStackData> startingItems = new();

    [Header("AI Behavior")]
    public bool isPlayerControlled = false;

    [HideIf(nameof(isPlayerControlled))]
    [InlineEditor]
    public GambitProfile gambitProfile;

    private void OnValidate()
    {
      if (isPlayerControlled || gambitProfile == null)
      {
        return;
      }

      if (startingSkills == null)
      {
        startingSkills = new List<SkillScriptableObject>();
      }

      if (startingItems == null)
      {
        startingItems = new List<ItemStackData>();
      }

      if (gambitProfile.rules == null || gambitProfile.rules.Count == 0)
      {
        return;
      }

      var ownedSkillIds = new HashSet<string>(startingSkills.Where(skill => skill != null).Select(skill => skill.skillId));
      var ownedItemIds = new HashSet<string>(startingItems.Where(stack => stack != null && stack.item != null).Select(stack => stack.item.itemId));

      var missingSkills = new HashSet<string>();
      var missingItems = new HashSet<string>();

      foreach (var rule in gambitProfile.rules)
      {
        if (rule?.action is SkillActionBlock skillBlock && skillBlock.skill != null && !ownedSkillIds.Contains(skillBlock.skill.skillId))
        {
          var skillName = string.IsNullOrEmpty(skillBlock.skill.displayName) ? skillBlock.skill.name : skillBlock.skill.displayName;
          missingSkills.Add(skillName);
        }

        if (rule?.action is ItemActionBlock itemBlock && itemBlock.item != null && !ownedItemIds.Contains(itemBlock.item.itemId))
        {
          var itemName = string.IsNullOrEmpty(itemBlock.item.displayName) ? itemBlock.item.name : itemBlock.item.displayName;
          missingItems.Add(itemName);
        }
      }

      if (missingSkills.Count == 0 && missingItems.Count == 0)
      {
        return;
      }

      var messageParts = new List<string>();
      if (missingSkills.Count > 0)
      {
        messageParts.Add($"skills [{string.Join(", ", missingSkills)}]");
      }

      if (missingItems.Count > 0)
      {
        messageParts.Add($"items [{string.Join(", ", missingItems)}]");
      }

      var displayLabel = string.IsNullOrEmpty(displayName) ? name : displayName;
      Debug.LogWarning($"Combatant template '{displayLabel}' has gambit actions referencing missing {string.Join(" and ", messageParts)}.", this);
    }
  }
}
