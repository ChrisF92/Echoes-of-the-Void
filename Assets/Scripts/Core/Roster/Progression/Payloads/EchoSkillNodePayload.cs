using EchoesOfTheVoid.Core.Combat.Entities;
using EchoesOfTheVoid.Core.Roster.Data;
using UnityEngine;

namespace EchoesOfTheVoid.Core.Roster.Progression.Payloads {
  public abstract class EchoSkillNodePayload : ScriptableObject {
    public abstract void Apply(PlayerEchoData echo, Combatant combatant);
  }
}
