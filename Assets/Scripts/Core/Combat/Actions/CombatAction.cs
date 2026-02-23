using System.Collections.Generic;
using EchoesOfTheVoid.Core.Combat.Entities;
using EchoesOfTheVoid.Core.Inventory.ScriptableObjects;

namespace EchoesOfTheVoid.Core.Combat.Actions {
  public class CombatAction {
    private readonly List<ICombatant> _targets = new();

    public CombatActionType ActionType { get; set; }

    public ICombatant Target {
      get => _targets.Count > 0 ? _targets[0] : null;
      set {
        _targets.Clear();
        AddTarget(value);
      }
    }

    public IReadOnlyList<ICombatant> Targets => _targets;

    public string SkillId { get; set; }
    public ItemScriptableObject ItemData { get; set; }

    public void SetTargets(IEnumerable<ICombatant> targets) {
      _targets.Clear();
      if (targets == null) {
        return;
      }

      foreach (ICombatant target in targets) {
        AddTarget(target);
      }
    }

    public void AddTarget(ICombatant target) {
      if (target == null || _targets.Contains(target)) {
        return;
      }

      _targets.Add(target);
    }

    public void ClearTargets() {
      _targets.Clear();
    }
  }
}
