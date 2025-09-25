using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using EchoesOfTheVoid.Core.Combat.Entities;
using EchoesOfTheVoid.Core.Combat.Gambits;
using EchoesOfTheVoid.Core.Combat.Gambits.Blocks.Implementations;
using EchoesOfTheVoid.Core.Combat.ScriptableObjects;
using EchoesOfTheVoid.Core.Combat.Systems;
using EchoesOfTheVoid.Core.Inventory.ScriptableObjects;

namespace EchoesOfTheVoid.Tests
{
  public class PlayerGambitProfileTestDriver : MonoBehaviour
  {
    [Header("References")]
    [SerializeField] private CombatSystem combatSystem;
    [SerializeField] private List<CombatantTemplateScriptableObject> playerTemplates = new List<CombatantTemplateScriptableObject>();

    [Header("Behaviour")]
    [SerializeField] private bool applyOnEnable = true;
    [SerializeField] private bool enableAutoCombat = true;
    [SerializeField, Min(0f)] private float waitTimeoutSeconds = 5f;

    private readonly Dictionary<string, GambitProfileData> profileCache = new Dictionary<string, GambitProfileData>();
    private Coroutine autoApplyRoutine;

    private void Awake()
    {
      if (combatSystem == null)
      {
        combatSystem = CombatSystem.Instance;
      }
    }

    private void OnEnable()
    {
      if (applyOnEnable)
      {
        autoApplyRoutine = StartCoroutine(ApplyWhenReady());
      }
    }

    private void OnDisable()
    {
      if (autoApplyRoutine != null)
      {
        StopCoroutine(autoApplyRoutine);
        autoApplyRoutine = null;
      }
    }

    private void OnValidate()
    {
      if (playerTemplates != null)
      {
        for (var i = playerTemplates.Count - 1; i >= 0; i--)
        {
          if (playerTemplates[i] == null)
          {
            playerTemplates.RemoveAt(i);
          }
        }
      }

      profileCache.Clear();
    }

    [ContextMenu("Apply Player Gambit Profiles")]
    public void ApplyProfilesFromContextMenu()
    {
      TryApplyProfiles(true);
    }

    private IEnumerator ApplyWhenReady()
    {
      var elapsed = 0f;
      while (combatSystem == null || combatSystem.PlayerTeam == null || combatSystem.PlayerTeam.Count == 0)
      {
        if (waitTimeoutSeconds > 0f && elapsed >= waitTimeoutSeconds)
        {
          Debug.LogWarning("PlayerGambitProfileTestDriver timed out waiting for combat system player team.", this);
          autoApplyRoutine = null;
          yield break;
        }

        elapsed += Time.deltaTime;
        yield return null;

        if (combatSystem == null)
        {
          combatSystem = CombatSystem.Instance;
        }
      }

      TryApplyProfiles();
      autoApplyRoutine = null;
    }

    public void TryApplyProfiles(bool verboseLogging = false)
    {
      if (combatSystem == null)
      {
        Debug.LogWarning("PlayerGambitProfileTestDriver has no CombatSystem reference.", this);
        return;
      }

      var playerTeam = combatSystem.PlayerTeam;
      if (playerTeam == null || playerTeam.Count == 0)
      {
        Debug.LogWarning("PlayerGambitProfileTestDriver found no player combatants to configure.", this);
        return;
      }

      EnsureProfilesBuilt(verboseLogging);

      var appliedAny = false;
      foreach (var combatantInterface in playerTeam)
      {
        if (combatantInterface is not Combatant combatant)
        {
          continue;
        }

        var template = ResolveTemplateForCombatant(combatant);
        if (template == null)
        {
          if (verboseLogging)
          {
            Debug.LogWarning($"No template match for combatant '{combatant.Name}'.", this);
          }
          continue;
        }

        if (!profileCache.TryGetValue(template.combatantId, out var profile) || profile == null)
        {
          if (verboseLogging)
          {
            Debug.LogWarning($"No cached profile for template '{template.displayName}'.", this);
          }
          continue;
        }

        combatant.ApplyGambitProfile(profile);
        if (enableAutoCombat)
        {
          combatSystem.SetAutoCombatEnabled(combatant, true);
        }

        appliedAny = true;

        if (verboseLogging)
        {
          Debug.Log($"Applied gambit profile '{profile.DisplayName}' to '{combatant.Name}'.", this);
        }
      }

      if (!appliedAny)
      {
        Debug.LogWarning("PlayerGambitProfileTestDriver did not apply any profiles to the player team.", this);
      }
    }

    private void EnsureProfilesBuilt(bool verboseLogging)
    {
      profileCache.Clear();

      if (playerTemplates == null || playerTemplates.Count == 0)
      {
        if (verboseLogging)
        {
          Debug.LogWarning("PlayerGambitProfileTestDriver has no player templates configured.", this);
        }
        return;
      }

      foreach (var template in playerTemplates)
      {
        if (template == null || string.IsNullOrWhiteSpace(template.combatantId))
        {
          continue;
        }

        var profile = BuildProfileForTemplate(template);
        if (profile != null)
        {
          profileCache[template.combatantId] = profile;
        }
        else if (verboseLogging)
        {
          Debug.LogWarning($"Failed to build gambit profile for template '{template.displayName}'.", this);
        }
      }
    }

    private CombatantTemplateScriptableObject ResolveTemplateForCombatant(Combatant combatant)
    {
      if (combatant == null || playerTemplates == null)
      {
        return null;
      }

      return playerTemplates.FirstOrDefault(template => template != null && string.Equals(template.displayName, combatant.Name));
    }

    private GambitProfileData BuildProfileForTemplate(CombatantTemplateScriptableObject template)
    {
      if (template == null)
      {
        return null;
      }

      var rules = template.combatantId switch
      {
        "aurora_knight" => BuildAuroraKnightRules(template),
        "starborne_ranger" => BuildStarborneRangerRules(template),
        _ => BuildDefaultRules(template)
      };

      if (rules == null || rules.Count == 0)
      {
        return null;
      }

      var profileId = $"test_{template.combatantId}";
      var displayName = string.IsNullOrWhiteSpace(template.displayName) ? template.name : template.displayName;
      return new GambitProfileData(profileId, $"{displayName} Test Profile", rules);
    }

    private List<GambitRuleDefinition> BuildAuroraKnightRules(CombatantTemplateScriptableObject template)
    {
      var rules = new List<GambitRuleDefinition>();

      var luminousTonic = FindItem(template, "luminous_tonic");
      if (luminousTonic != null)
      {
        rules.Add(CreateAllyHealItemRule("Use Tonic Under 50%", luminousTonic, 0.5f, true));
      }

      var solarStrike = FindSkill(template, "solar_strike");
      if (solarStrike != null)
      {
        rules.Add(CreateOffensiveSkillRule("Cast Solar Strike", solarStrike));
      }

      rules.Add(CreateBasicAttackFallback("Attack Fallback"));
      return rules;
    }

    private List<GambitRuleDefinition> BuildStarborneRangerRules(CombatantTemplateScriptableObject template)
    {
      var rules = new List<GambitRuleDefinition>();

      var aegisChant = FindSkill(template, "aegis_chant");
      if (aegisChant != null)
      {
        rules.Add(CreateAllyHealSkillRule("Aegis Chant Ally <60%", aegisChant, 0.6f));
      }

      var luminousTonic = FindItem(template, "luminous_tonic");
      if (luminousTonic != null)
      {
        rules.Add(CreateAllyHealItemRule("Emergency Tonic <35%", luminousTonic, 0.35f, true));
      }

      var solarStrike = FindSkill(template, "solar_strike");
      if (solarStrike != null)
      {
        rules.Add(CreateOffensiveSkillRule("Cast Solar Strike", solarStrike));
      }

      rules.Add(CreateBasicAttackFallback("Attack Fallback"));
      return rules;
    }

    private List<GambitRuleDefinition> BuildDefaultRules(CombatantTemplateScriptableObject template)
    {
      var rules = new List<GambitRuleDefinition>();

      var firstSkill = template.startingSkills?.FirstOrDefault(skill => skill != null);
      if (firstSkill != null)
      {
        rules.Add(CreateOffensiveSkillRule($"Cast {firstSkill.displayName}", firstSkill));
      }

      var firstItem = template.startingItems?.FirstOrDefault(stack => stack != null && stack.item != null)?.item;
      if (firstItem != null)
      {
        rules.Add(CreateAllyHealItemRule($"Use {firstItem.displayName}", firstItem, 0.3f, true));
      }

      if (rules.Count == 0)
      {
        return rules;
      }

      rules.Add(CreateBasicAttackFallback("Attack Fallback"));
      return rules;
    }

    private static GambitRuleDefinition CreateAllyHealItemRule(string ruleName, ItemScriptableObject item, float threshold, bool includeSelf)
    {
      return new GambitRuleDefinition
      {
        ruleName = ruleName,
        isEnabled = true,
        targetCondition = new AllyHealthBelowPercentBlock
        {
          threshold = Mathf.Clamp01(threshold),
          includeSelf = includeSelf
        },
        action = new ItemActionBlock
        {
          item = item,
          requireAvailability = true
        }
      };
    }

    private static GambitRuleDefinition CreateAllyHealSkillRule(string ruleName, SkillScriptableObject skill, float threshold)
    {
      return new GambitRuleDefinition
      {
        ruleName = ruleName,
        isEnabled = true,
        targetCondition = new AllyHealthBelowPercentBlock
        {
          threshold = Mathf.Clamp01(threshold),
          includeSelf = true
        },
        action = new SkillActionBlock
        {
          skill = skill,
          requireCanUse = true
        }
      };
    }

    private static GambitRuleDefinition CreateOffensiveSkillRule(string ruleName, SkillScriptableObject skill)
    {
      return new GambitRuleDefinition
      {
        ruleName = ruleName,
        isEnabled = true,
        targetCondition = new RandomEnemyTargetBlock(),
        action = new SkillActionBlock
        {
          skill = skill,
          requireCanUse = true
        }
      };
    }

    private static GambitRuleDefinition CreateBasicAttackFallback(string ruleName)
    {
      return new GambitRuleDefinition
      {
        ruleName = ruleName,
        isEnabled = true,
        targetCondition = new RandomEnemyTargetBlock(),
        action = new AttackActionBlock()
      };
    }

    private static ItemScriptableObject FindItem(CombatantTemplateScriptableObject template, string itemId)
    {
      if (template.startingItems == null)
      {
        return null;
      }

      foreach (var stack in template.startingItems)
      {
        if (stack?.item != null && (string.IsNullOrWhiteSpace(itemId) || stack.item.itemId == itemId))
        {
          return stack.item;
        }
      }

      return null;
    }

    private static SkillScriptableObject FindSkill(CombatantTemplateScriptableObject template, string skillId)
    {
      if (template.startingSkills == null)
      {
        return null;
      }

      foreach (var skill in template.startingSkills)
      {
        if (skill != null && (string.IsNullOrWhiteSpace(skillId) || skill.skillId == skillId))
        {
          return skill;
        }
      }

      return null;
    }
  }
}
