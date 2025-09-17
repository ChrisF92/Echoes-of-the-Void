using System;
using System.Collections.Generic;
using EchoesOfTheVoid.Core;
using EchoesOfTheVoid.Combat.Actions;
using UnityEngine;

namespace EchoesOfTheVoid.Combat
{
  /// <summary>
  /// Determines valid targets for an action from a fixed 6x6 grid of combatants.
  /// Uses simple, replaceable policies per action type and falls back to all alive.
  /// </summary>
  [DisallowMultipleComponent]
  [AddComponentMenu("Combat/Targeting System")]
  public sealed class TargetingSystem : MonoBehaviour
  {
    public const int DefaultRows = 3;
    public const int DefaultColumns = 6;

    /// <summary>
    /// Returns the number of rows in the active grid.
    /// </summary>
    public int Rows => _grid.GetLength(0);

    /// <summary>
    /// Returns the number of columns in the active grid.
    /// </summary>
    public int Columns => _grid.GetLength(1);

    private readonly Dictionary<Type, ITargetingPolicy> _policies = new Dictionary<Type, ITargetingPolicy>();
    private ICombatant[,] _grid = new ICombatant[DefaultRows, DefaultColumns];
    private readonly List<ICombatant> _highlighted = new List<ICombatant>();

    /// <summary>
    /// The current set of highlighted targets.
    /// </summary>
    public IReadOnlyList<ICombatant> HighlightedTargets => _highlighted.AsReadOnly();

    /// <summary>
    /// Raised when the highlighted targets change.
    /// </summary>
    public event Action<IReadOnlyList<ICombatant>> TargetsHighlighted;

    private void Awake()
    {
      RegisterDefaultPolicies();
    }

    /// <summary>
    /// Sets or replaces the grid contents. Values are copied into an internal 6x6 grid.
    /// If the provided array is not 6x6, its contents are copied into the top-left area.
    /// </summary>
    public void SetGrid(ICombatant[,] source)
    {
      if (source == null) throw new ArgumentNullException(nameof(source));

      _grid = new ICombatant[DefaultRows, DefaultColumns];
      int rMax = Math.Min(DefaultRows, source.GetLength(0));
      int cMax = Math.Min(DefaultColumns, source.GetLength(1));
      for (int r = 0; r < rMax; r++)
      {
        for (int c = 0; c < cMax; c++)
        {
          _grid[r, c] = source[r, c];
        }
      }
    }

    /// <summary>
    /// Sets the list of highlighted targets and notifies listeners.
    /// </summary>
    public void HighlightTargets(IEnumerable<ICombatant> targets)
    {
      _highlighted.Clear();
      if (targets != null)
      {
        foreach (ICombatant t in targets)
        {
          if (t != null)
          {
            _highlighted.Add(t);
          }
        }
      }
      TargetsHighlighted?.Invoke(HighlightedTargets);
    }

    /// <summary>
    /// Clears current highlights.
    /// </summary>
    public void ClearHighlights()
    {
      if (_highlighted.Count == 0)
      {
        return;
      }
      _highlighted.Clear();
      TargetsHighlighted?.Invoke(HighlightedTargets);
    }

    /// <summary>
    /// Sets a single cell in the grid. Out-of-bounds indices are ignored with a warning.
    /// </summary>
    public void SetCombatantAt(int row, int column, ICombatant combatant)
    {
      if (!IsInBounds(row, column))
      {
        Debug.LogWarning($"TargetingSystem: Index out of bounds ({row},{column}).");
        return;
      }
      _grid[row, column] = combatant;
    }

    /// <summary>
    /// Computes valid targets for the given <paramref name="action"/> and <paramref name="user"/>.
    /// </summary>
    public List<ICombatant> GetValidTargets(ICombatAction action, ICombatant user)
    {
      if (action == null) throw new ArgumentNullException(nameof(action));
      if (user == null) throw new ArgumentNullException(nameof(user));

      List<ICombatant> allAlive = EnumerateAllAliveUnique();
      ITargetingPolicy policy = ResolvePolicy(action);
      try
      {
        return policy.SelectTargets(this, action, user, allAlive);
      }
      catch (Exception e)
      {
        Debug.LogException(e);
        return new List<ICombatant>();
      }
    }

    /// <summary>
    /// Registers a targeting policy for a specific action type.
    /// </summary>
    public void RegisterPolicy<TAction>(ITargetingPolicy policy) where TAction : ICombatAction
    {
      if (policy == null) throw new ArgumentNullException(nameof(policy));
      _policies[typeof(TAction)] = policy;
    }

    private void RegisterDefaultPolicies()
    {
      // Default mappings for provided actions. These can be overridden via RegisterPolicy.
      _policies[typeof(AttackAction)] = new EnemiesOnlyPolicy(excludeSelf: true);
      _policies[typeof(DefendAction)] = new SelfOnlyPolicy();
      // Items commonly include heals/buffs → allies by default (can be overridden).
      _policies[typeof(UseItemAction)] = new AlliesOnlyPolicy(includeSelf: true);
      _policies[typeof(UseSkillAction)] = new AllAlivePolicy();
    }

    private ITargetingPolicy ResolvePolicy(ICombatAction action)
    {
      Type t = action.GetType();
      if (_policies.TryGetValue(t, out ITargetingPolicy policy))
      {
        return policy;
      }
      return new AllAlivePolicy();
    }

    private List<ICombatant> EnumerateAllAliveUnique()
    {
      var list = new List<ICombatant>();
      var seen = new HashSet<ICombatant>();
      for (int r = 0; r < Rows; r++)
      {
        for (int c = 0; c < Columns; c++)
        {
          ICombatant combatant = _grid[r, c];
          if (combatant == null || !combatant.IsAlive)
          {
            continue;
          }
          if (seen.Add(combatant))
          {
            list.Add(combatant);
          }
        }
      }
      return list;
    }

    private enum Side
    {
      Unknown = 0,
      Left = 1,
      Right = 2,
    }

    private bool TryFindPosition(ICombatant target, out int row, out int column)
    {
      for (int r = 0; r < Rows; r++)
      {
        for (int c = 0; c < Columns; c++)
        {
          if (ReferenceEquals(_grid[r, c], target))
          {
            row = r;
            column = c;
            return true;
          }
        }
      }
      row = -1;
      column = -1;
      return false;
    }

    private Side GetSideOf(ICombatant combatant)
    {
      if (!TryFindPosition(combatant, out _, out int col))
      {
        return Side.Unknown;
      }
      int leftWidth = Columns / 2; // 6x6 → 3 columns per side.
      return col < leftWidth ? Side.Left : Side.Right;
    }

    private List<ICombatant> EnumerateAliveOnSide(Side side)
    {
      var list = new List<ICombatant>();
      if (side == Side.Unknown)
      {
        return EnumerateAllAliveUnique();
      }

      int leftWidth = Columns / 2;
      int startCol = side == Side.Left ? 0 : leftWidth;
      int endColExclusive = side == Side.Left ? leftWidth : Columns;

      var seen = new HashSet<ICombatant>();
      for (int r = 0; r < Rows; r++)
      {
        for (int c = startCol; c < endColExclusive; c++)
        {
          ICombatant combatant = _grid[r, c];
          if (combatant == null || !combatant.IsAlive)
          {
            continue;
          }
          if (seen.Add(combatant))
          {
            list.Add(combatant);
          }
        }
      }
      return list;
    }

    private bool IsInBounds(int row, int column)
    {
      return row >= 0 && row < Rows && column >= 0 && column < Columns;
    }

    // Targeting policies keep TargetingSystem closed to modification while open for extension.
    public interface ITargetingPolicy
    {
      List<ICombatant> SelectTargets(TargetingSystem system, ICombatAction action, ICombatant user, List<ICombatant> allAlive);
    }

    private sealed class AllAlivePolicy : ITargetingPolicy
    {
      public List<ICombatant> SelectTargets(TargetingSystem system, ICombatAction action, ICombatant user, List<ICombatant> allAlive)
      {
        return new List<ICombatant>(allAlive);
      }
    }

    private sealed class ExcludeSelfPolicy : ITargetingPolicy
    {
      public List<ICombatant> SelectTargets(TargetingSystem system, ICombatAction action, ICombatant user, List<ICombatant> allAlive)
      {
        var list = new List<ICombatant>(allAlive.Count);
        foreach (ICombatant c in allAlive)
        {
          if (!ReferenceEquals(c, user))
          {
            list.Add(c);
          }
        }
        return list;
      }
    }

    private sealed class SelfOnlyPolicy : ITargetingPolicy
    {
      public List<ICombatant> SelectTargets(TargetingSystem system, ICombatAction action, ICombatant user, List<ICombatant> allAlive)
      {
        if (user.IsAlive)
        {
          return new List<ICombatant> { user };
        }
        return new List<ICombatant>();
      }
    }

    private sealed class AlliesOnlyPolicy : ITargetingPolicy
    {
      private readonly bool _includeSelf;

      public AlliesOnlyPolicy(bool includeSelf)
      {
        _includeSelf = includeSelf;
      }

      public List<ICombatant> SelectTargets(TargetingSystem system, ICombatAction action, ICombatant user, List<ICombatant> allAlive)
      {
        Side side = system.GetSideOf(user);
        var allies = system.EnumerateAliveOnSide(side);
        if (!_includeSelf)
        {
          allies.RemoveAll(c => ReferenceEquals(c, user));
        }
        return allies;
      }
    }

    private sealed class EnemiesOnlyPolicy : ITargetingPolicy
    {
      private readonly bool _excludeSelf;

      public EnemiesOnlyPolicy(bool excludeSelf)
      {
        _excludeSelf = excludeSelf;
      }

      public List<ICombatant> SelectTargets(TargetingSystem system, ICombatAction action, ICombatant user, List<ICombatant> allAlive)
      {
        Side side = system.GetSideOf(user);
        Side enemySide = side == Side.Left ? Side.Right : side == Side.Right ? Side.Left : Side.Unknown;
        var enemies = system.EnumerateAliveOnSide(enemySide);
        if (_excludeSelf)
        {
          enemies.RemoveAll(c => ReferenceEquals(c, user));
        }
        return enemies;
      }
    }
  }
}
