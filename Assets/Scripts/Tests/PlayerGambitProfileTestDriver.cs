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
using EchoesOfTheVoid.Core.Inventory.Data;

namespace EchoesOfTheVoid.Tests {
  public class PlayerGambitProfileTestDriver : MonoBehaviour {
    [Header("References")]
    [SerializeField] private CombatSystem _combatSystem;
    [SerializeField] private List<CombatantTemplateScriptableObject> _playerTemplates = new();

    [Header("Behaviour")]
    [SerializeField] private bool _applyOnEnable = true;
    [SerializeField] private bool _enableAutoCombat = true;
    [SerializeField, Min(0f)] private float _waitTimeoutSeconds = 5f;

    private readonly Dictionary<string, GambitProfileData> _profileCache = new();
    private Coroutine _autoApplyRoutine;

    private void Awake() {
      if (_combatSystem == null) {
        _combatSystem = CombatSystem.Instance;
      }
    }

    private void OnEnable() {
      if (_applyOnEnable) {
        _autoApplyRoutine = StartCoroutine(ApplyWhenReady());
      }
    }

    private void OnDisable() {
      if (_autoApplyRoutine != null) {
        StopCoroutine(_autoApplyRoutine);
        _autoApplyRoutine = null;
      }
    }

    private void OnValidate() {
      if (_playerTemplates != null) {
        for (int i = _playerTemplates.Count - 1; i >= 0; i--) {
          if (_playerTemplates[i] == null) {
            _playerTemplates.RemoveAt(i);
          }
        }
      }

      _profileCache.Clear();
    }

    [ContextMenu("Apply Player Gambit Profiles")]
    public void ApplyProfilesFromContextMenu() {
      TryApplyProfiles(true);
    }

    private IEnumerator ApplyWhenReady() {
      float elapsed = 0f;
      while (_combatSystem == null || _combatSystem.PlayerTeam == null || _combatSystem.PlayerTeam.Count == 0) {
        if (_waitTimeoutSeconds > 0f && elapsed >= _waitTimeoutSeconds) {
          Debug.LogWarning("PlayerGambitProfileTestDriver timed out waiting for combat system player team.", this);
          _autoApplyRoutine = null;
          yield break;
        }

        elapsed += Time.deltaTime;
        yield return null;

        if (_combatSystem == null) {
          _combatSystem = CombatSystem.Instance;
        }
      }

      TryApplyProfiles();
      _autoApplyRoutine = null;
    }

    public void TryApplyProfiles(bool verboseLogging = false) {
      if (_combatSystem == null) {
        Debug.LogWarning("PlayerGambitProfileTestDriver has no CombatSystem reference.", this);
        return;
      }

      List<ICombatant> playerTeam = _combatSystem.PlayerTeam;
      if (playerTeam == null || playerTeam.Count == 0) {
        Debug.LogWarning("PlayerGambitProfileTestDriver found no player combatants to configure.", this);
        return;
      }

      EnsureProfilesBuilt(verboseLogging);

      bool appliedAny = false;
      foreach (ICombatant combatantInterface in playerTeam) {
        if (combatantInterface is not Combatant combatant) {
          continue;
        }

        CombatantTemplateScriptableObject template = ResolveTemplateForCombatant(combatant);
        if (template == null) {
          if (verboseLogging) {
            Debug.LogWarning($"No template match for combatant '{combatant.Name}'.", this);
          }
          continue;
        }

        if (!_profileCache.TryGetValue(template.combatantId, out GambitProfileData profile) || profile == null) {
          if (verboseLogging) {
            Debug.LogWarning($"No cached profile for template '{template.displayName}'.", this);
          }
          continue;
        }

        combatant.ApplyGambitProfile(profile);
        if (_enableAutoCombat) {
          _combatSystem.SetAutoCombatEnabled(combatant, true);
        }

        appliedAny = true;

        if (verboseLogging) {
          Debug.Log($"Applied gambit profile '{profile.DisplayName}' to '{combatant.Name}'.", this);
        }
      }

      if (!appliedAny) {
        Debug.LogWarning("PlayerGambitProfileTestDriver did not apply any profiles to the player team.", this);
      }
    }

    private void EnsureProfilesBuilt(bool verboseLogging) {
      _profileCache.Clear();

      if (_playerTemplates == null || _playerTemplates.Count == 0) {
        if (verboseLogging) {
          Debug.LogWarning("PlayerGambitProfileTestDriver has no player templates configured.", this);
        }
        return;
      }

      foreach (CombatantTemplateScriptableObject template in _playerTemplates) {
        if (template == null || string.IsNullOrWhiteSpace(template.combatantId)) {
          continue;
        }

        GambitProfileData profile = BuildProfileForTemplate(template);
        if (profile != null) {
          _profileCache[template.combatantId] = profile;
        } else if (verboseLogging) {
          Debug.LogWarning($"Failed to build gambit profile for template '{template.displayName}'.", this);
        }
      }
    }

    private CombatantTemplateScriptableObject ResolveTemplateForCombatant(Combatant combatant) {
      return combatant == null || _playerTemplates == null
        ? null
        : _playerTemplates.FirstOrDefault(template => template != null && string.Equals(template.displayName, combatant.Name));
    }

    private GambitProfileData BuildProfileForTemplate(CombatantTemplateScriptableObject template) {
      if (template == null) {
        return null;
      }

      List<GambitRuleDefinition> rules = template.combatantId switch {
        "aurora_knight" => BuildAuroraKnightRules(template),
        "starborne_ranger" => BuildStarborneRangerRules(template),
        _ => BuildDefaultRules(template)
      };

      if (rules == null || rules.Count == 0) {
        return null;
      }

      string profileId = $"test_{template.combatantId}";
      string displayName = string.IsNullOrWhiteSpace(template.displayName) ? template.name : template.displayName;
      return new GambitProfileData(profileId, $"{displayName} Test Profile", rules);
    }

    private List<GambitRuleDefinition> BuildAuroraKnightRules(CombatantTemplateScriptableObject template) {
      var rules = new List<GambitRuleDefinition>();

      ItemScriptableObject luminousTonic = FindItem(template, "luminous_tonic");
      if (luminousTonic != null) {
        rules.Add(CreateAllyHealItemRule("Use Tonic Under 50%", luminousTonic, 0.5f, true));
      }

      SkillScriptableObject solarStrike = FindSkill(template, "solar_strike");
      if (solarStrike != null) {
        rules.Add(CreateOffensiveSkillRule("Cast Solar Strike", solarStrike));
      }

      rules.Add(CreateBasicAttackFallback("Attack Fallback"));
      return rules;
    }

    private List<GambitRuleDefinition> BuildStarborneRangerRules(CombatantTemplateScriptableObject template) {
      var rules = new List<GambitRuleDefinition>();

      SkillScriptableObject aegisChant = FindSkill(template, "aegis_chant");
      if (aegisChant != null) {
        rules.Add(CreateAllyHealSkillRule("Aegis Chant Ally <60%", aegisChant, 0.6f));
      }

      ItemScriptableObject luminousTonic = FindItem(template, "luminous_tonic");
      if (luminousTonic != null) {
        rules.Add(CreateAllyHealItemRule("Emergency Tonic <35%", luminousTonic, 0.35f, true));
      }

      SkillScriptableObject solarStrike = FindSkill(template, "solar_strike");
      if (solarStrike != null) {
        rules.Add(CreateOffensiveSkillRule("Cast Solar Strike", solarStrike));
      }

      rules.Add(CreateBasicAttackFallback("Attack Fallback"));
      return rules;
    }

    private List<GambitRuleDefinition> BuildDefaultRules(CombatantTemplateScriptableObject template) {
      var rules = new List<GambitRuleDefinition>();

      SkillScriptableObject firstSkill = template.startingSkills?.FirstOrDefault(static skill => skill != null);
      if (firstSkill != null) {
        rules.Add(CreateOffensiveSkillRule($"Cast {firstSkill.DisplayName}", firstSkill));
      }

      ItemScriptableObject firstItem = template.startingItems?.FirstOrDefault(static stack => stack != null && stack.Item != null)?.Item;
      if (firstItem != null) {
        rules.Add(CreateAllyHealItemRule($"Use {firstItem.DisplayName}", firstItem, 0.3f, true));
      }

      if (rules.Count == 0) {
        return rules;
      }

      rules.Add(CreateBasicAttackFallback("Attack Fallback"));
      return rules;
    }

    private static GambitRuleDefinition CreateAllyHealItemRule(string ruleName, ItemScriptableObject item, float threshold, bool includeSelf) {
      return new GambitRuleDefinition {
        RuleName = ruleName,
        IsEnabled = true,
        TargetCondition = new AllyHealthBelowPercentBlock {
          Threshold = Mathf.Clamp01(threshold),
          IncludeSelf = includeSelf
        },
        Action = new ItemActionBlock {
          item = item,
          requireAvailability = true
        }
      };
    }

    private static GambitRuleDefinition CreateAllyHealSkillRule(string ruleName, SkillScriptableObject skill, float threshold) {
      return new GambitRuleDefinition {
        RuleName = ruleName,
        IsEnabled = true,
        TargetCondition = new AllyHealthBelowPercentBlock {
          Threshold = Mathf.Clamp01(threshold),
          IncludeSelf = true
        },
        Action = new SkillActionBlock {
          skill = skill,
          requireCanUse = true
        }
      };
    }

    private static GambitRuleDefinition CreateOffensiveSkillRule(string ruleName, SkillScriptableObject skill) {
      return new GambitRuleDefinition {
        RuleName = ruleName,
        IsEnabled = true,
        TargetCondition = new RandomEnemyTargetBlock(),
        Action = new SkillActionBlock {
          skill = skill,
          requireCanUse = true
        }
      };
    }

    private static GambitRuleDefinition CreateBasicAttackFallback(string ruleName) {
      return new GambitRuleDefinition {
        RuleName = ruleName,
        IsEnabled = true,
        TargetCondition = new RandomEnemyTargetBlock(),
        Action = new AttackActionBlock()
      };
    }

    private static ItemScriptableObject FindItem(CombatantTemplateScriptableObject template, string itemId) {
      if (template.startingItems == null) {
        return null;
      }

      foreach (ItemStackData stack in template.startingItems) {
        if (stack?.Item != null && (string.IsNullOrWhiteSpace(itemId) || stack.Item.ItemId == itemId)) {
          return stack.Item;
        }
      }

      return null;
    }

    private static SkillScriptableObject FindSkill(CombatantTemplateScriptableObject template, string skillId) {
      if (template.startingSkills == null) {
        return null;
      }

      foreach (SkillScriptableObject skill in template.startingSkills) {
        if (skill != null && (string.IsNullOrWhiteSpace(skillId) || skill.SkillId == skillId)) {
          return skill;
        }
      }

      return null;
    }
  }
}
