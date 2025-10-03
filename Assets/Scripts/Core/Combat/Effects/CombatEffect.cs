using EchoesOfTheVoid.Core.Combat.Entities;
using EchoesOfTheVoid.Core.Combat.ScriptableObjects;

namespace EchoesOfTheVoid.Core.Combat.Effects {
  public class CombatEffect {
    public EffectType Type { get; set; }
    public int Value { get; set; }
    public ICombatant Target { get; set; }
    public float Duration { get; set; } = 0f;
    public StatusEffectSO StatusEffect { get; set; }
    public int AppliedValue { get; set; }
  }
}
