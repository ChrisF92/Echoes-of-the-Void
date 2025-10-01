using System;
using System.Collections.Generic;
using EchoesOfTheVoid.Core.Combat.Actions;
using EchoesOfTheVoid.Core.Combat.Entities;

namespace EchoesOfTheVoid.Core.Combat.Gambits {
  public class GambitEvaluationLog {
    public GambitEvaluationLog(Combatant actor, IGambitRuleSource profile) {
      Actor = actor;
      Profile = profile;
      Timestamp = DateTime.UtcNow;
    }

    public Combatant Actor { get; }
    public IGambitRuleSource Profile { get; }
    public string ProfileName => Profile?.DisplayName;
    public DateTime Timestamp { get; }
    public List<GambitRuleEvaluationRecord> Records { get; } = new();
    public CombatAction SelectedAction { get; private set; }
    public ICombatant SelectedTarget { get; private set; }

    public bool HasMatch => SelectedAction != null;

    public void SetResult(CombatAction action, ICombatant target) {
      SelectedAction = action;
      SelectedTarget = target;
    }
  }

  public class GambitRuleEvaluationRecord {
    public GambitRuleEvaluationRecord(GambitRuleDefinition rule) {
      Rule = rule;
      RuleName = rule?.RuleName;
      WasEnabled = rule?.IsEnabled ?? false;
    }

    public GambitRuleDefinition Rule { get; }
    public string RuleName { get; }
    public bool WasEnabled { get; set; }
    public bool ConditionMatched { get; set; }
    public bool ActionBuilt { get; set; }
    public ICombatant Target { get; set; }
    public string FailureReason { get; set; }
  }
}

