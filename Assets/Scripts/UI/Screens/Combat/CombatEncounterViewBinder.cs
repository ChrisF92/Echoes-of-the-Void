using System.Collections.Generic;
using UnityEngine;

using EchoesOfTheVoid.Core.Combat.Entities;
using EchoesOfTheVoid.Core.Combat.Systems;

namespace EchoesOfTheVoid.UI.Combat {
  [DisallowMultipleComponent]
  public class CombatEncounterViewBinder : MonoBehaviour {
    [SerializeField] private CombatEncounterBootstrapper _bootstrapper;
    [SerializeField] private CombatViewController _viewController;

    private void Awake() {
      if (_bootstrapper == null) {
        _bootstrapper = FindFirstObjectByType<CombatEncounterBootstrapper>();
      }

      if (_viewController == null) {
        _viewController = FindFirstObjectByType<CombatViewController>();
      }
    }

    private void OnEnable() {
      Subscribe();
    }

    private void OnDisable() {
      Unsubscribe();
    }

    private void Subscribe() {
      if (_bootstrapper == null) {
        return;
      }

      _bootstrapper.OnPartiesPrepared -= HandlePartiesPrepared;
      _bootstrapper.OnPartiesPrepared += HandlePartiesPrepared;
    }

    private void Unsubscribe() {
      if (_bootstrapper == null) {
        return;
      }

      _bootstrapper.OnPartiesPrepared -= HandlePartiesPrepared;
    }

    private void HandlePartiesPrepared(IReadOnlyList<Combatant> playerParty, IReadOnlyList<Combatant> enemyParty) {
      if (_viewController == null) {
        return;
      }

      var players = playerParty != null ? new List<Combatant>(playerParty) : new List<Combatant>();
      var enemies = enemyParty != null ? new List<Combatant>(enemyParty) : new List<Combatant>();
      _viewController.InitializeBattle(players, enemies);
    }
  }
}
