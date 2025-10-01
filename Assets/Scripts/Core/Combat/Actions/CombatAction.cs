using EchoesOfTheVoid.Core.Combat.Entities;
using EchoesOfTheVoid.Core.Inventory.ScriptableObjects;

namespace EchoesOfTheVoid.Core.Combat.Actions {
  public class CombatAction {
    public CombatActionType ActionType { get; set; }
    public ICombatant Target { get; set; }
    public string SkillId { get; set; }
    public ItemScriptableObject ItemData { get; set; }
  }
}
