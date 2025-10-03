using System;

namespace EchoesOfTheVoid.Core.Combat.Systems {
  /// <summary>
  /// Manages combat state transitions.
  /// </summary>
  public class CombatStateManager {
    public CombatState CurrentState { get; private set; } = CombatState.Setup;

    public event Action<CombatState> OnStateChanged;

    public void ChangeState(CombatState newState) {
      if (CurrentState == newState) {
        return;
      }

      CurrentState = newState;
      OnStateChanged?.Invoke(newState);
    }

    public bool CanExecuteActions() {
      return CurrentState == CombatState.InProgress;
    }
  }
}