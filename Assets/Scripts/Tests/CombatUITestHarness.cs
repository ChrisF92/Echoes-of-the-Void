using System.Collections;
using System.Collections.Generic;
using System.Linq;
using EchoesOfTheVoid.Core.Combat.Entities;
using EchoesOfTheVoid.Core.Combat.ScriptableObjects;
using EchoesOfTheVoid.Core.Combat.Systems;
using EchoesOfTheVoid.UI.Combat;
using UnityEngine;

namespace EchoesOfTheVoid.Tests {
  [DisallowMultipleComponent]
  public class CombatUITestHarness : MonoBehaviour {
    [Header("References")]
    [SerializeField] private CombatViewController _combatViewController;
    [SerializeField] private CombatSystem _combatSystem;

    [Header("Templates")]
    [SerializeField] private List<CombatantTemplateScriptableObject> _playerTemplates = new();
    [SerializeField] private List<CombatantTemplateScriptableObject> _enemyTemplates = new();

    [Header("Behaviour")]
    [SerializeField] private bool _autoInitializeOnStart = true;
    [SerializeField] private bool _autoSimulateTurns;
    [SerializeField] private float _autoTurnInterval = 2f;

    private readonly List<Combatant> _spawnedCombatants = new();
    private Coroutine _autoSimCoroutine;

    private void Awake() {
      if (_combatViewController == null) {
        _combatViewController = FindFirstObjectByType<CombatViewController>();
      }

      if (_combatSystem == null) {
        _combatSystem = CombatSystem.Instance;
      }
    }

    private void Start() {
      if (_autoInitializeOnStart) {
        InitializeHarness();
      } else if (_autoSimulateTurns) {
        StartAutoSimulation();
      }
    }

    private void OnDisable() {
      StopAutoSimulation();
    }

    private void OnDestroy() {
      StopAutoSimulation();
      CleanupSpawnedCombatants();
    }

    private void Update() {
      if (_combatViewController == null) {
        return;
      }
    }

    public void InitializeHarness() {
      if (_combatViewController == null) {
        Debug.LogWarning("CombatUITestHarness requires a CombatViewController reference.", this);
        return;
      }

      StopAutoSimulation();
      CleanupSpawnedCombatants();

      List<Combatant> players = CreateCombatants(_playerTemplates, true);
      List<Combatant> enemies = CreateCombatants(_enemyTemplates, false);

      _combatViewController.InitializeBattle(players, enemies);

      if (_combatSystem != null) {
        var playerInterfaces = players.Cast<ICombatant>().ToList();
        var enemyInterfaces = enemies.Cast<ICombatant>().ToList();
        _combatSystem.StartCombat(playerInterfaces, enemyInterfaces);
      } else if (players.Count > 0) {
        _combatViewController.SetActivePlayer(players[0]);
      }

      if (_autoSimulateTurns) {
        StartAutoSimulation();
      }
    }

    private List<Combatant> CreateCombatants(IEnumerable<CombatantTemplateScriptableObject> templates, bool isPlayerTeam) {
      var results = new List<Combatant>();
      if (templates == null) {
        return results;
      }

      foreach (CombatantTemplateScriptableObject template in templates) {
        if (template == null) {
          continue;
        }

        Combatant combatant = _combatViewController.CreateTestCombatantFromTemplate(template, isPlayerTeam);
        if (combatant == null) {
          continue;
        }

        _spawnedCombatants.Add(combatant);
        results.Add(combatant);
      }

      return results;
    }

    private void StartAutoSimulation() {
      if (!_autoSimulateTurns || _autoTurnInterval <= 0f || _combatViewController == null) {
        return;
      }

      StopAutoSimulation();
      _autoSimCoroutine = StartCoroutine(AutoSimulationLoop());
    }

    private void StopAutoSimulation() {
      if (_autoSimCoroutine != null) {
        StopCoroutine(_autoSimCoroutine);
        _autoSimCoroutine = null;
      }
    }

    private IEnumerator AutoSimulationLoop() {
      var wait = new WaitForSeconds(_autoTurnInterval);
      while (true) {
        yield return wait;
        _combatViewController?.SimulateCombatTurn();
      }
    }

    private void CleanupSpawnedCombatants() {
      for (int i = _spawnedCombatants.Count - 1; i >= 0; i--) {
        Combatant combatant = _spawnedCombatants[i];
        if (combatant == null) {
          _spawnedCombatants.RemoveAt(i);
          continue;
        }

        if (Application.isPlaying) {
          Destroy(combatant.gameObject);
        } else {
          DestroyImmediate(combatant.gameObject);
        }

        _spawnedCombatants.RemoveAt(i);
      }

      _spawnedCombatants.Clear();
    }
  }
}

