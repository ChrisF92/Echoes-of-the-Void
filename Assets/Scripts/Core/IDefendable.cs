using System;

namespace EchoesOfTheVoid.Core
{
  /// <summary>
  /// Capability for entering a defensive stance and mitigating incoming damage.
  /// </summary>
  public interface IDefendable
  {
    /// <summary>
    /// Applies a defend stance for a number of turns, reducing incoming damage by
    /// <paramref name="damageReduction"/> fraction (0..1).
    /// </summary>
    void ApplyDefense(int turns, float damageReduction);

    /// <summary>
    /// Returns the mitigated damage for an incoming attack and updates internal state.
    /// </summary>
    int MitigateDamage(int incomingDamage);
  }
}

