using EchoesOfTheVoid.Core.Combat.Entities;

namespace EchoesOfTheVoid.Core.Combat.Components {
  public abstract class CombatComponent {
    public abstract void Initialize(ICombatant owner);
    public abstract void Update(float deltaTime);
  }
}

