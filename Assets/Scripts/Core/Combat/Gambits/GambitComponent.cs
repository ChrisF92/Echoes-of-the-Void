using System.Collections.Generic;
using EchoesOfTheVoid.Core.Combat.Actions;
using EchoesOfTheVoid.Core.Combat.Components;
using EchoesOfTheVoid.Core.Combat.Entities;

namespace EchoesOfTheVoid.Core.Combat.Gambits {
  public class GambitComponent : CombatComponent {
    private Combatant _owner;

    public IGambitRuleSource CurrentProfile { get; private set; }

    public override void Initialize(ICombatant owner) {
      _owner = owner as Combatant;
    }

    public override void Update(float deltaTime) {
    }

    public void SetProfile(GambitProfile profile) {
      SetProfileSource(profile);
    }

    public void SetProfile(GambitProfileData profile) {
      SetProfileSource(profile);
    }

    public void SetProfileSource(IGambitRuleSource profile) {
      CurrentProfile = profile;
    }

    public bool TryBuildAction(GambitRuntimeContext context, out CombatAction action, out GambitEvaluationLog log) {
      action = null;
      log = new GambitEvaluationLog(_owner, CurrentProfile);

      if (CurrentProfile == null) {
        log.Records.Add(new GambitRuleEvaluationRecord(null) {
          FailureReason = "No gambit profile assigned"
        });
        return false;
      }

      IReadOnlyList<GambitRuleDefinition> rules = CurrentProfile.Rules;
      if (rules == null || rules.Count == 0) {
        log.Records.Add(new GambitRuleEvaluationRecord(null) {
          FailureReason = "Profile contains no rules"
        });
        return false;
      }

      foreach (GambitRuleDefinition rule in rules) {
        var record = new GambitRuleEvaluationRecord(rule);
        log.Records.Add(record);

        if (rule == null) {
          record.FailureReason = "Rule reference missing";
          continue;
        }

        if (!rule.IsEnabled) {
          record.FailureReason = "Rule disabled";
          continue;
        }

        if (!rule.HasValidBlocks) {
          record.FailureReason = "Rule incomplete";
          continue;
        }

        if (!rule.TargetCondition.TrySelectTarget(context, out ICombatant target, out string conditionFailure)) {
          record.FailureReason = conditionFailure;
          continue;
        }

        record.ConditionMatched = true;
        record.Target = target;

        if (!rule.Action.TryBuildAction(context, target, out action, out string actionFailure)) {
          record.FailureReason = actionFailure;
          continue;
        }

        record.ActionBuilt = true;
        log.SetResult(action, target);
        return true;
      }

      return false;
    }
  }
}
