using System;

namespace EchoesOfTheVoid.Core
{
  /// <summary>
  /// Capability for receiving health changes.
  /// </summary>
  public interface IDamageable
  {
    void ApplyDamage(int amount);
    void RestoreHealth(int amount);
  }
}

