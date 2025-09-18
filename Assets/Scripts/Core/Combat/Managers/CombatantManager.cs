using System;
using System.Collections.Generic;
using System.Linq;

using EchoesOfTheVoid.Core.Combat.Entities;

namespace EchoesOfTheVoid.Core.Combat.Managers
{
  public class CombatantManager
  {
    private readonly List<ICombatant> _registeredCombatants = new();

    public event Action<ICombatant> OnCombatantDefeated;
    public event Action<ICombatant, int> OnCombatantDamaged;
    public event Action<ICombatant, int> OnCombatantHealed;

    public void RegisterCombatant(ICombatant combatant)
    {
      if (!_registeredCombatants.Contains(combatant))
      {
        _registeredCombatants.Add(combatant);
        combatant.OnDefeated += () => OnCombatantDefeated?.Invoke(combatant);
        combatant.OnDamaged += damage => OnCombatantDamaged?.Invoke(combatant, damage);
        combatant.OnHealed += amount => OnCombatantHealed?.Invoke(combatant, amount);
      }
    }

    public void UnregisterCombatant(ICombatant combatant)
    {
      _registeredCombatants.Remove(combatant);
    }

    public void UpdateAllCombatants(float deltaTime)
    {
      foreach (var combatant in _registeredCombatants.Where(c => c.IsAlive))
      {
        combatant.UpdateComponents(deltaTime);
      }
    }
  }
}

