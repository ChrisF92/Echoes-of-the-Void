using System.Collections.Generic;
using System.Linq;
using EchoesOfTheVoid.Core.Combat.Data;
using EchoesOfTheVoid.Core.Combat.Gambits;
using EchoesOfTheVoid.Core.Combat.Gambits.Blocks.Implementations;
using EchoesOfTheVoid.Core.Inventory.Data;
using Sirenix.OdinInspector;
using UnityEngine;

namespace EchoesOfTheVoid.Core.Combat.ScriptableObjects {
  [CreateAssetMenu(fileName = "New Combatant Template", menuName = "Combat/Combatant Template")]
  public class CombatantSO : ScriptableObject {

    [Header("AI Behavior")]
    public bool IsPlayerControlled = false;

    [Header("Basic Info")]
    public string CombatantId;
    public string DisplayName;
    public Sprite Portrait;
    public GameObject CombatPrefab;

    [Header("Base Stats")]
    public CombatStats BaseStats;

    [Header("Starting Skills")]
    public List<SkillSO> StartingSkills = new();

    [HideIf(nameof(IsPlayerControlled))]
    [Header("Starting Items")]
    public List<ItemStackData> StartingItems = new();
    [HideIf(nameof(IsPlayerControlled))]
    [Header("Starting Equipment")]
    public List<EquippedItemData> StartingEquipment = new();


    [HideIf(nameof(IsPlayerControlled))]
    [Header("Gambits")]
    [InlineEditor]
    public GambitProfile GambitProfile;

    private void OnValidate() {
      if (IsPlayerControlled || GambitProfile == null) {
        return;
      }

      StartingSkills ??= new List<SkillSO>();

      StartingItems ??= new List<ItemStackData>();

      StartingEquipment ??= new List<EquippedItemData>();
      if (GambitProfile.rules == null || GambitProfile.rules.Count == 0) {
        return;
      }

      var ownedSkillIds = new HashSet<string>(StartingSkills.Where(static skill => skill != null).Select(static skill => skill.SkillId));
      var ownedItemIds = new HashSet<string>(StartingItems.Where(static stack => stack != null && stack.Item != null).Select(static stack => stack.Item.ItemId));

      var missingSkills = new HashSet<string>();
      var missingItems = new HashSet<string>();

      foreach (GambitRuleDefinition rule in GambitProfile.rules) {
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

      string displayLabel = string.IsNullOrEmpty(DisplayName) ? name : DisplayName;
      Debug.LogWarning($"Combatant template '{displayLabel}' has gambit actions referencing missing {string.Join(" and ", messageParts)}.", this);
    }
  }
}
