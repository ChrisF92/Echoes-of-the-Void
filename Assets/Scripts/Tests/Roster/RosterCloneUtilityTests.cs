using NUnit.Framework;
using EchoesOfTheVoid.Core.Combat.Gambits;
using EchoesOfTheVoid.Core.Roster;

namespace EchoesOfTheVoid.Tests.Roster {
  public class RosterCloneUtilityTests {
    [Test]
    public void CloneGambitProfile_CreatesIndependentCopy() {
      var original = new GambitProfileData("test", "Test Profile", null);
      original.rules.Add(new GambitRuleDefinition {
        RuleName = "Rule A",
        IsEnabled = true,
        TargetCondition = new DummyCondition(),
        Action = new DummyAction()
      });

      GambitProfileData clone = RosterCloneUtility.CloneGambitProfile(original);

      Assert.AreNotSame(original, clone);
      Assert.AreEqual(original.rules.Count, clone.rules.Count);
      clone.rules[0].RuleName = "Modified";

      Assert.AreNotEqual(original.rules[0].RuleName, clone.rules[0].RuleName);
    }

    private sealed class DummyCondition : TargetConditionBlock {
      public override bool TrySelectTarget(GambitRuntimeContext context, out EchoesOfTheVoid.Core.Combat.Entities.ICombatant target, out string failureReason) {
        target = null;
        failureReason = "";
        return false;
      }
    }

    private sealed class DummyAction : GambitActionBlock {
      public override bool TryBuildAction(GambitRuntimeContext context, EchoesOfTheVoid.Core.Combat.Entities.ICombatant target, out EchoesOfTheVoid.Core.Combat.Actions.CombatAction action, out string failureReason) {
        action = null;
        failureReason = "";
        return false;
      }
    }
  }
}
