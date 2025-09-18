using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using EchoesOfTheVoid.Core.Combat;
using EchoesOfTheVoid.Core.Combat.Entities;
using EchoesOfTheVoid.Core.Combat.ScriptableObjects;
using EchoesOfTheVoid.Core.Combat.Systems;
using EchoesOfTheVoid.UI.Combat;

namespace EchoesOfTheVoid.Tests
{
  [DisallowMultipleComponent]
  public class CombatUITestHarness : MonoBehaviour
  {
    [Header("References")]
    [SerializeField] private CombatViewController combatViewController;
    [SerializeField] private CombatSystem combatSystem;

    [Header("Templates")]
    [SerializeField] private List<CombatantTemplateScriptableObject> playerTemplates = new List<CombatantTemplateScriptableObject>();
    [SerializeField] private List<CombatantTemplateScriptableObject> enemyTemplates = new List<CombatantTemplateScriptableObject>();

    [Header("Behaviour")]
    [SerializeField] private bool autoInitializeOnStart = true;
    [SerializeField] private bool autoSimulateTurns;
    [SerializeField] private float autoTurnInterval = 2f;

    [Header("Manual Controls")]
    [SerializeField] private KeyCode simulateTurnKey = KeyCode.T;
    [SerializeField] private KeyCode reopenItemsKey = KeyCode.I;
    [SerializeField] private KeyCode reopenSkillsKey = KeyCode.K;

    private readonly List<Combatant> spawnedCombatants = new List<Combatant>();
    private Coroutine autoSimCoroutine;

    private void Awake()
    {
      if (combatViewController == null)
      {
        combatViewController = FindObjectOfType<CombatViewController>();
      }

      if (combatSystem == null)
      {
        combatSystem = CombatSystem.Instance;
      }
    }

    private void Start()
    {
      if (autoInitializeOnStart)
      {
        InitializeHarness();
      }
      else if (autoSimulateTurns)
      {
        StartAutoSimulation();
      }
    }

    private void OnDisable()
    {
      StopAutoSimulation();
    }

    private void OnDestroy()
    {
      StopAutoSimulation();
      CleanupSpawnedCombatants();
    }

    private void Update()
    {
      if (combatViewController == null)
      {
        return;
      }

      if (simulateTurnKey != KeyCode.None && Input.GetKeyDown(simulateTurnKey))
      {
        combatViewController.SimulateCombatTurn();
      }

      if (reopenItemsKey != KeyCode.None && Input.GetKeyDown(reopenItemsKey))
      {
        combatViewController.TestItemUsage();
      }

      if (reopenSkillsKey != KeyCode.None && Input.GetKeyDown(reopenSkillsKey))
      {
        combatViewController.TestSkillUsage();
      }
    }

    public void InitializeHarness()
    {
      if (combatViewController == null)
      {
        Debug.LogWarning("CombatUITestHarness requires a CombatViewController reference.", this);
        return;
      }

      StopAutoSimulation();
      CleanupSpawnedCombatants();

      var players = CreateCombatants(playerTemplates, true);
      var enemies = CreateCombatants(enemyTemplates, false);

      combatViewController.InitializeBattle(players, enemies);

      if (combatSystem != null)
      {
        var playerInterfaces = players.Cast<ICombatant>().ToList();
        var enemyInterfaces = enemies.Cast<ICombatant>().ToList();
        combatSystem.StartCombat(playerInterfaces, enemyInterfaces);
      }
      else if (players.Count > 0)
      {
        combatViewController.SetActivePlayer(players[0]);
      }

      if (autoSimulateTurns)
      {
        StartAutoSimulation();
      }
    }

    private List<Combatant> CreateCombatants(IEnumerable<CombatantTemplateScriptableObject> templates, bool isPlayerTeam)
    {
      var results = new List<Combatant>();
      if (templates == null)
      {
        return results;
      }

      foreach (var template in templates)
      {
        if (template == null)
        {
          continue;
        }

        var combatant = combatViewController.CreateTestCombatantFromTemplate(template, isPlayerTeam);
        if (combatant == null)
        {
          continue;
        }

        spawnedCombatants.Add(combatant);
        results.Add(combatant);
      }

      return results;
    }

    private void StartAutoSimulation()
    {
      if (!autoSimulateTurns || autoTurnInterval <= 0f || combatViewController == null)
      {
        return;
      }

      StopAutoSimulation();
      autoSimCoroutine = StartCoroutine(AutoSimulationLoop());
    }

    private void StopAutoSimulation()
    {
      if (autoSimCoroutine != null)
      {
        StopCoroutine(autoSimCoroutine);
        autoSimCoroutine = null;
      }
    }

    private IEnumerator AutoSimulationLoop()
    {
      var wait = new WaitForSeconds(autoTurnInterval);
      while (true)
      {
        yield return wait;
        combatViewController?.SimulateCombatTurn();
      }
    }

    private void CleanupSpawnedCombatants()
    {
      for (var i = spawnedCombatants.Count - 1; i >= 0; i--)
      {
        var combatant = spawnedCombatants[i];
        if (combatant == null)
        {
          spawnedCombatants.RemoveAt(i);
          continue;
        }

        if (Application.isPlaying)
        {
          Destroy(combatant.gameObject);
        }
        else
        {
          DestroyImmediate(combatant.gameObject);
        }

        spawnedCombatants.RemoveAt(i);
      }

      spawnedCombatants.Clear();
    }
  }
}

