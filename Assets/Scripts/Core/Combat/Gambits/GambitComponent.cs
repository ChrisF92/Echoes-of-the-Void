using EchoesOfTheVoid.Core.Combat.Actions;
using EchoesOfTheVoid.Core.Combat.Components;
using EchoesOfTheVoid.Core.Combat.Entities;

namespace EchoesOfTheVoid.Core.Combat.Gambits
{
  public class GambitComponent : CombatComponent
  {
    private Combatant _owner;
    private IGambitRuleSource _profile;
    public IGambitRuleSource CurrentProfile => _profile;

    public override void Initialize(ICombatant owner)
    {
      _owner = owner as Combatant;
    }

    public override void Update(float deltaTime)
    {
    }

    public void SetProfile(GambitProfile profile)
    {
      SetProfileSource(profile);
    }

    public void SetProfile(GambitProfileData profile)
    {
      SetProfileSource(profile);
    }

    public void SetProfileSource(IGambitRuleSource profile)
    {
      _profile = profile;
    }

    public bool TryBuildAction(GambitRuntimeContext context, out CombatAction action, out GambitEvaluationLog log)
    {
      action = null;
      log = new GambitEvaluationLog(_owner, _profile);

      if (_profile == null)
      {
        log.Records.Add(new GambitRuleEvaluationRecord(null)
        {
          FailureReason = "No gambit profile assigned"
        });
        return false;
      }

      var rules = _profile.Rules;
      if (rules == null || rules.Count == 0)
      {
        log.Records.Add(new GambitRuleEvaluationRecord(null)
        {
          FailureReason = "Profile contains no rules"
        });
        return false;
      }

      foreach (var rule in rules)
      {
        var record = new GambitRuleEvaluationRecord(rule);
        log.Records.Add(record);

        if (rule == null)
        {
          record.FailureReason = "Rule reference missing";
          continue;
        }

        if (!rule.isEnabled)
        {
          record.FailureReason = "Rule disabled";
          continue;
        }

        if (!rule.HasValidBlocks)
        {
          record.FailureReason = "Rule incomplete";
          continue;
        }

        if (!rule.targetCondition.TrySelectTarget(context, out var target, out var conditionFailure))
        {
          record.FailureReason = conditionFailure;
          continue;
        }

        record.ConditionMatched = true;
        record.Target = target;

        if (!rule.action.TryBuildAction(context, target, out action, out var actionFailure))
        {
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
