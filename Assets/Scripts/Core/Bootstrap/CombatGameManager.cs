using System;
using System.Collections.Generic;
using EchoesOfTheVoid.Combat;
using EchoesOfTheVoid.Combat.Actions;
using EchoesOfTheVoid.UI.UITK;
using UnityEngine;

namespace EchoesOfTheVoid.Core
{
  /// <summary>
  /// Bootstraps the combat scene: builds a 6x6 grid, places combatants,
  /// and wires core systems (TurnManager, TargetingSystem, UI, ActionExecutor).
  /// </summary>
  [DisallowMultipleComponent]
  [DefaultExecutionOrder(-1000)]
  [AddComponentMenu("Combat/Combat Game Manager")]
  public sealed class CombatGameManager : MonoBehaviour
  {
    [Header("Core Systems")]
    [SerializeField] private TurnManager _turnManager;
    [SerializeField] private TargetingSystem _targetingSystem;
    [SerializeField] private ActionExecutor _actionExecutor;
    [SerializeField] private CombatUIController _uiController;
    [SerializeField] private TargetHighlightView _highlightView;

    [Header("Optional Managers")]
    [SerializeField] private EchoesOfTheVoid.Items.ItemManager _itemManager;
    [SerializeField] private EchoesOfTheVoid.Skills.SkillManager _skillManager;

    [Header("Grid Setup (6x6)")]
    [SerializeField] private bool _autoDiscoverSceneCombatants = true;
    [SerializeField] private List<Placement> _placements = new List<Placement>();

    [Header("Flow")]
    [SerializeField] private bool _autoStartCombat = true;

    [Header("Debug")]
    [SerializeField] private bool _forceEnableLogging = true;

    [Serializable]
    public sealed class Placement
    {
      public MonoBehaviour Behaviour;
      public int Row;
      public int Column;
    }

    private const int Rows = 3;
    private const int Columns = 6;

    private ICombatant[,] _grid = new ICombatant[Rows, Columns];

    private void Awake()
    {
      if (_forceEnableLogging)
      {
        try
        {
          Debug.unityLogger.logEnabled = true;
          Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.ScriptOnly);
          Debug.Log("[Bootstrap] Logging enabled");
        }
        catch { }
      }
      // Locate dependencies if not set (simple DI via scene graph).
      _turnManager ??= FindFirstObjectByType<TurnManager>();
      _targetingSystem ??= FindFirstObjectByType<TargetingSystem>();
      _actionExecutor ??= FindFirstObjectByType<ActionExecutor>();
      _uiController ??= FindFirstObjectByType<CombatUIController>();
      _highlightView ??= FindFirstObjectByType<TargetHighlightView>();

      // Inject dependencies into components that support it.
      _uiController?.Configure(_turnManager, _targetingSystem, _itemManager, _skillManager);
      _actionExecutor?.Configure(_turnManager, _targetingSystem);
      _highlightView?.Configure(_targetingSystem, _actionExecutor);

      BuildGrid();
      WireSystems();
    }

    private void Start()
    {
      if (_autoStartCombat)
      {
        _turnManager?.StartCombat();
      }
    }

    private void BuildGrid()
    {
      _grid = new ICombatant[Rows, Columns];

      // Apply explicit placements.
      foreach (Placement p in _placements)
      {
        if (p == null || p.Behaviour == null)
        {
          continue;
        }
        if (p.Row < 0 || p.Row >= Rows || p.Column < 0 || p.Column >= Columns)
        {
          Debug.LogWarning($"CombatGameManager: Placement out of bounds ({p.Row},{p.Column}).");
          continue;
        }
        if (p.Behaviour is ICombatant combatant)
        {
          _grid[p.Row, p.Column] = combatant;
        }
        else
        {
          Debug.LogWarning($"CombatGameManager: Behaviour '{p.Behaviour.name}' does not implement ICombatant.");
        }
      }

      // Optionally auto-place discovered combatants into free slots.
      if (_autoDiscoverSceneCombatants)
      {
        AutoPlaceSceneCombatants();
      }
    }

    private void AutoPlaceSceneCombatants()
    {
      var players = FindObjectsByType<EchoesOfTheVoid.Combat.PlayerCharacter>(FindObjectsSortMode.None);
      var enemies = FindObjectsByType<EchoesOfTheVoid.Combat.EnemyCharacter>(FindObjectsSortMode.None);

      // Fill left half (cols 0..2) with players, right half (3..5) with enemies
      // without overwriting any explicitly placed units.
      int playerIndex = 0;
      for (int r = 0; r < Rows && playerIndex < players.Length; r++)
      {
        for (int c = 0; c < Columns / 2 && playerIndex < players.Length; c++)
        {
          if (_grid[r, c] == null)
          {
            _grid[r, c] = players[playerIndex++];
          }
        }
      }

      int enemyIndex = 0;
      for (int r = 0; r < Rows && enemyIndex < enemies.Length; r++)
      {
        for (int c = Columns / 2; c < Columns && enemyIndex < enemies.Length; c++)
        {
          if (_grid[r, c] == null)
          {
            _grid[r, c] = enemies[enemyIndex++];
          }
        }
      }
    }

    private void WireSystems()
    {
      if (_targetingSystem != null)
      {
        _targetingSystem.SetGrid(_grid);
      }
      if (_highlightView != null)
      {
        _highlightView.SetGrid(_grid);
      }

      if (_turnManager != null)
      {
        List<ICombatant> order = BuildTurnOrderFromGrid();
        _turnManager.Initialize(order);
      }
    }

    private List<ICombatant> BuildTurnOrderFromGrid()
    {
      var order = new List<ICombatant>();
      var seen = new HashSet<ICombatant>();
      for (int r = 0; r < Rows; r++)
      {
        for (int c = 0; c < Columns; c++)
        {
          ICombatant unit = _grid[r, c];
          if (unit != null && seen.Add(unit))
          {
            order.Add(unit);
          }
        }
      }
      return order;
    }
  }
}
