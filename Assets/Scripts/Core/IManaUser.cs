using System;

namespace EchoesOfTheVoid.Core
{
  /// <summary>
  /// Capability for spending and restoring mana-like resources.
  /// </summary>
  public interface IManaUser
  {
    int Mana { get; }
    bool TryConsumeMana(int amount);
    void RestoreMana(int amount);
  }
}

