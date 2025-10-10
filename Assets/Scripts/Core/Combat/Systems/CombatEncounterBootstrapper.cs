using System;
using System.Collections.Generic;
using System.Linq;
using EchoesOfTheVoid.Core.Combat.Entities;
using EchoesOfTheVoid.Core.Combat.ScriptableObjects;
using EchoesOfTheVoid.Core.Roster;
using EchoesOfTheVoid.Core.Roster.Data;
using UnityEngine;

namespace EchoesOfTheVoid.Core.Combat.Systems {
  [DisallowMultipleComponent]
  public class CombatEncounterBootstrapper : MonoBehaviour {
    [Header("References")]
    [SerializeField] private PlayerRosterService _rosterService;
    [SerializeField] private CombatSystem _combatSystem;
    [SerializeField] private Transform _playerPartyParent;
    [SerializeField] private Transform _enemyPartyParent;

    [Header("Fallbacks")]
    [SerializeField] private List<CombatantSO> _defaultEnemyTemplates = new();

    private readonly List<Combatant> _spawnedPlayerCombatants = new();
    private readonly List<Combatant> _spawnedEnemyCombatants = new();

    public event Action<IReadOnlyList<Combatant>, IReadOnlyList<Combatant>> OnPartiesPrepared;

    private void Awake() {
      if (_rosterService == null) {
        _rosterService = FindFirstObjectByType<PlayerRosterService>();
      }

      if (_combatSystem == null) {
        _combatSystem = CombatSystem.Instance;
      }
    }

    public void BeginEncounter(IEnumerable<CombatantSO> enemyTemplates = null) {
      CleanupSpawnedCombatants();

      if (_rosterService == null) {
        Debug.LogWarning("CombatEncounterBootstrapper requires a PlayerRosterService.", this);
        return;
      }

      List<Combatant> playerCombatants = RosterCombatPartyBuilder.BuildPlayerParty(_rosterService, _playerPartyParent);
      if (playerCombatants.Count == 0) {
        Debug.LogWarning("CombatEncounterBootstrapper found no configured party members. Aborting combat start.", this);
        return;
      }

      _spawnedPlayerCombatants.AddRange(playerCombatants);

      List<CombatantSO> enemyTemplateList = ResolveEnemyTemplates(enemyTemplates);
      List<Combatant> enemyCombatants = RosterCombatPartyBuilder.BuildEnemyParty(enemyTemplateList, _enemyPartyParent);
      _spawnedEnemyCombatants.AddRange(enemyCombatants);

      OnPartiesPrepared?.Invoke(playerCombatants, enemyCombatants);

      if (_combatSystem != null) {
        var playerInterfaces = playerCombatants.Cast<ICombatant>().ToList();
        var enemyInterfaces = enemyCombatants.Cast<ICombatant>().ToList();
        _combatSystem.StartCombat(playerInterfaces, enemyInterfaces);
      }
    }

    private List<CombatantSO> ResolveEnemyTemplates(IEnumerable<CombatantSO> enemyTemplates) {
      if (enemyTemplates != null) {
        return enemyTemplates.Where(static template => template != null).ToList();
      }

      _defaultEnemyTemplates ??= new List<CombatantSO>();
      return _defaultEnemyTemplates.Where(static template => template != null).ToList();
    }

    private void OnDestroy() {
      CleanupSpawnedCombatants();
    }

    private void CleanupSpawnedCombatants() {
      CleanupList(_spawnedPlayerCombatants);
      CleanupList(_spawnedEnemyCombatants);
    }

    private void CleanupList(List<Combatant> list) {
      for (int i = list.Count - 1; i >= 0; i--) {
        Combatant combatant = list[i];
        if (combatant == null) {
          list.RemoveAt(i);
          continue;
        }

        if (Application.isPlaying) {
          Destroy(combatant.gameObject);
        } else {
          DestroyImmediate(combatant.gameObject);
        }

        list.RemoveAt(i);
      }
    }
  }
}
