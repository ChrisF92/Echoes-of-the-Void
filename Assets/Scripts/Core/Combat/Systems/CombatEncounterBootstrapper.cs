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
    [SerializeField] private List<CombatantTemplateScriptableObject> _defaultEnemyTemplates = new();

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

    public void BeginEncounter(IEnumerable<CombatantTemplateScriptableObject> enemyTemplates = null) {
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

      List<CombatantTemplateScriptableObject> enemyTemplateList = ResolveEnemyTemplates(enemyTemplates);
      List<Combatant> enemyCombatants = BuildEnemyParty(enemyTemplateList, _enemyPartyParent);
      _spawnedEnemyCombatants.AddRange(enemyCombatants);

      OnPartiesPrepared?.Invoke(playerCombatants, enemyCombatants);

      if (_combatSystem != null) {
        var playerInterfaces = playerCombatants.Cast<ICombatant>().ToList();
        var enemyInterfaces = enemyCombatants.Cast<ICombatant>().ToList();
        _combatSystem.StartCombat(playerInterfaces, enemyInterfaces);
      }
    }

    private List<Combatant> BuildEnemyParty(IEnumerable<CombatantTemplateScriptableObject> templates, Transform parent) {
      var result = new List<Combatant>();
      if (templates == null) {
        return result;
      }

      foreach (CombatantTemplateScriptableObject template in templates) {
        if (template == null) {
          continue;
        }

        var tempEcho = new PlayerEchoData(template.combatantId, template);
        tempEcho.SetEquipment(template.startingEquipment);
        tempEcho.SetGambitProfile(RosterCloneUtility.CloneGambitProfile(template.gambitProfile));

        Combatant combatant = RosterCombatPartyBuilder.CreateCombatantForEcho(tempEcho, parent);
        if (combatant == null) {
          continue;
        }

        combatant.SetTeam(CombatTeam.Enemy);
        result.Add(combatant);
      }

      return result;
    }

    private List<CombatantTemplateScriptableObject> ResolveEnemyTemplates(IEnumerable<CombatantTemplateScriptableObject> enemyTemplates) {
      if (enemyTemplates != null) {
        return enemyTemplates.Where(static template => template != null).ToList();
      }

      _defaultEnemyTemplates ??= new List<CombatantTemplateScriptableObject>();
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
