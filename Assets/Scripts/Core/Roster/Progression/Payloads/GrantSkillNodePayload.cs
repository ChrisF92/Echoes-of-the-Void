using EchoesOfTheVoid.Core.Combat.Components;
using EchoesOfTheVoid.Core.Combat.Entities;
using EchoesOfTheVoid.Core.Combat.ScriptableObjects;
using EchoesOfTheVoid.Core.Roster.Data;
using UnityEngine;

namespace EchoesOfTheVoid.Core.Roster.Progression.Payloads {
  [CreateAssetMenu(fileName = "GrantSkillNodePayload", menuName = "Roster/Progression/Payloads/Grant Skill")]
  public class GrantSkillNodePayload : EchoSkillNodePayload {
    [SerializeField] private SkillSO _skill;

    public SkillSO Skill => _skill;

    public override void Apply(PlayerEchoData echo, Combatant combatant) {
      if (_skill == null || combatant == null) {
        return;
      }

      SkillComponent component = combatant.GetComponent<SkillComponent>();
      component?.LearnSkill(_skill);
    }
  }
}
