using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;

using EchoesOfTheVoid.Core.Combat.Data;
using EchoesOfTheVoid.Core.Combat.Gambits;
using EchoesOfTheVoid.Core.Combat.Gambits.Blocks.Implementations;
using EchoesOfTheVoid.Core.Inventory.Data;

namespace EchoesOfTheVoid.Core.Combat.ScriptableObjects {
  [CreateAssetMenu(fileName = "New Combatant Template", menuName = "Combat/Combatant Template")]
  public class CombatantTemplateScriptableObject : ScriptableObject {

    [Header("AI Behavior")]
    public bool isPlayerControlled = false;

    [Header("Basic Info")]
    public string combatantId;
    public string displayName;
    public Sprite portrait;
    public GameObject combatPrefab;

    [Header("Base Stats")]
    public CombatStats baseStats;

    [Header("Starting Skills")]
    public List<SkillScriptableObject> startingSkills = new();

    [HideIf(nameof(isPlayerControlled))]
    [Header("Starting Items")]
    public List<ItemStackData> startingItems = new();
    [HideIf(nameof(isPlayerControlled))]
    [Header("Starting Equipment")]
    public List<EquippedItemData> startingEquipment = new();


    [HideIf(nameof(isPlayerControlled))]
    [Header("Gambits")]
    [InlineEditor]
    public GambitProfile gambitProfile;

    private void OnValidate() {
      if (isPlayerControlled || gambitProfile == null) {
        return;
      }

      startingSkills ??= new List<SkillScriptableObject>();

      startingItems ??= new List<ItemStackData>();

      startingEquipment ??= new List<EquippedItemData>();
      if (gambitProfile.rules == null || gambitProfile.rules.Count == 0) {
        return;
      }

      var ownedSkillIds = new HashSet<string>(startingSkills.Where(skill => skill != null).Select(skill => skill.SkillId));
      var ownedItemIds = new HashSet<string>(startingItems.Where(stack => stack != null && stack.Item != null).Select(stack => stack.Item.ItemId));

      var missingSkills = new HashSet<string>();
      var missingItems = new HashSet<string>();

      foreach (GambitRuleDefinition rule in gambitProfile.rules) {
        if (rule?.Action is SkillActionBlock skillBlock && skillBlock.skill != null && !ownedSkillIds.Contains(skillBlock.skill.SkillId)) {
          string skillName = string.IsNullOrEmpty(skillBlock.skill.DisplayName) ? skillBlock.skill.name : skillBlock.skill.DisplayName;
          _ = missingSkills.Add(skillName);
        }

        if (rule?.Action is ItemActionBlock itemBlock && itemBlock.item != null && !ownedItemIds.Contains(itemBlock.item.ItemId)) {
          string itemName = string.IsNullOrEmpty(itemBlock.item.DisplayName) ? itemBlock.item.name : itemBlock.item.DisplayName;
          _ = missingItems.Add(itemName);
        }
      }

      if (missingSkills.Count == 0 && missingItems.Count == 0) {
        return;
      }

      var messageParts = new List<string>();
      if (missingSkills.Count > 0) {
        messageParts.Add($"skills [{string.Join(", ", missingSkills)}]");
      }

      if (missingItems.Count > 0) {
        messageParts.Add($"items [{string.Join(", ", missingItems)}]");
      }

      string displayLabel = string.IsNullOrEmpty(displayName) ? name : displayName;
      Debug.LogWarning($"Combatant template '{displayLabel}' has gambit actions referencing missing {string.Join(" and ", messageParts)}.", this);
    }
  }
}
