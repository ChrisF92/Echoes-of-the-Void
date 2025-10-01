using System;
using EchoesOfTheVoid.Core.Combat.Actions;
using EchoesOfTheVoid.Core.Combat.Components;
using EchoesOfTheVoid.Core.Combat.Entities;
using EchoesOfTheVoid.Core.Combat.ScriptableObjects;

namespace EchoesOfTheVoid.Core.Combat.Gambits.Blocks.Implementations {
  [Serializable]
  public class SkillActionBlock : GambitActionBlock {
    public SkillScriptableObject skill;
    public bool requireCanUse = true;

    public override string Summary => skill != null ? $"Use Skill: {skill.DisplayName}" : "Use Skill";

    public override bool TryBuildAction(GambitRuntimeContext context, ICombatant target, out CombatAction action, out string failureReason) {
      action = null;
      if (skill == null) {
        failureReason = "Skill not set";
        return false;
      }

      SkillComponent skillComponent = context?.Actor?.GetComponent<SkillComponent>();
      if (skillComponent == null) {
        failureReason = "No skill component";
        return false;
      }

      if (requireCanUse && !skillComponent.CanUseSkill(skill.SkillId)) {
        failureReason = "Skill not usable";
        return false;
      }

      action = new CombatAction {
        ActionType = CombatActionType.Skill,
        SkillId = skill.SkillId,
        Target = target
      };

      failureReason = null;
      return true;
    }
  }
}
