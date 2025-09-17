using System;
using System.Collections.Generic;
using UnityEngine;

namespace EchoesOfTheVoid.Core
{
  /// <summary>
  /// Manages the flow of turn-based combat across a set of <see cref="ICombatant"/>s.
  /// - Maintains the ordered list of combatants.
  /// - Tracks whose turn it is.
  /// - Advances turns and rounds while skipping ineligible combatants.
  /// Adheres to SOLID by depending only on the <see cref="ICombatant"/> abstraction.
  /// </summary>
  [DisallowMultipleComponent]
  [AddComponentMenu("Combat/Turn Manager")]
  public sealed class TurnManager : MonoBehaviour
  {
    /// <summary>
    /// Invoked after a combatant's <see cref="ICombatant.BeginTurn"/> is called.
    /// </summary>
    public event Action<ICombatant> TurnStarted;

    /// <summary>
    /// Invoked after a combatant's <see cref="ICombatant.EndTurn"/> is called.
    /// </summary>
    public event Action<ICombatant> TurnEnded;

    /// <summary>
    /// Invoked when a new round starts. Argument is the 1-based round number.
    /// </summary>
    public event Action<int> RoundStarted;

    /// <summary>
    /// Invoked when combat ends (no eligible combatants remain).
    /// </summary>
    public event Action CombatEnded;

    /// <summary>
    /// Returns a read-only view of the current combatant order.
    /// </summary>
    public IReadOnlyList<ICombatant> Combatants => _combatants.AsReadOnly();

    /// <summary>
    /// The combatant whose turn is currently active, or <c>null</c> if none.
    /// </summary>
    public ICombatant CurrentCombatant { get; private set; }

    /// <summary>
    /// True when combat is active and turns can advance.
    /// </summary>
    public bool IsCombatActive => _isActive;

    /// <summary>
    /// The 1-based round number. Zero when combat has not started.
    /// </summary>
    public int RoundNumber => _roundNumber;

    private readonly List<ICombatant> _combatants = new List<ICombatant>();
    private int _currentIndex = -1;
    private bool _isActive;
    private int _roundNumber;

    /// <summary>
    /// Initializes the manager with a predefined turn order. Does not start combat.
    /// </summary>
    /// <param name="combatants">The ordered set of combatants.</param>
    public void Initialize(IEnumerable<ICombatant> combatants)
    {
      if (combatants == null) throw new ArgumentNullException(nameof(combatants));

      _combatants.Clear();
      foreach (ICombatant c in combatants)
      {
        if (c == null) continue;
        _combatants.Add(c);
      }

      ResetState();
    }

    /// <summary>
    /// Adds a combatant to the end of the turn order.
    /// Safe to call during an active combat; the new entrant is considered on subsequent turns.
    /// </summary>
    public void AddCombatant(ICombatant combatant)
    {
      if (combatant == null) throw new ArgumentNullException(nameof(combatant));
      _combatants.Add(combatant);
    }

    /// <summary>
    /// Removes a combatant from the turn order.
    /// If the removed combatant is the current one, the current turn is ended immediately.
    /// </summary>
    public bool RemoveCombatant(ICombatant combatant)
    {
      if (combatant == null) return false;

      int index = _combatants.IndexOf(combatant);
      if (index < 0) return false;

      bool wasCurrent = index == _currentIndex;
      _combatants.RemoveAt(index);

      // Keep index stable relative to the same logical next combatant.
      if (index <= _currentIndex)
      {
        _currentIndex--;
      }

      if (wasCurrent)
      {
        // End the turn of the removed combatant and advance to the next eligible.
        // Do not double-end if it was already ended externally.
        SafeEndTurn(combatant);
        CurrentCombatant = null;
        if (_isActive)
        {
          AdvanceToNextTurn();
        }
      }

      // If nothing remains, stop combat.
      if (_combatants.Count == 0 && _isActive)
      {
        StopCombat();
      }

      return true;
    }

    /// <summary>
    /// Starts combat and immediately advances to the first combatant's turn.
    /// </summary>
    public void StartCombat()
    {
      if (_isActive) return;
      if (_combatants.Count == 0)
      {
        Debug.LogWarning("TurnManager: Cannot start combat with no combatants.");
        return;
      }

      _isActive = true;
      _roundNumber = 0;
      _currentIndex = -1;
      CurrentCombatant = null;
      AdvanceToNextTurn();
    }

    /// <summary>
    /// Ends combat. The current turn (if any) is ended, and state is reset.
    /// </summary>
    public void StopCombat()
    {
      if (!_isActive) return;

      if (CurrentCombatant != null)
      {
        SafeEndTurn(CurrentCombatant);
        CurrentCombatant = null;
      }

      _isActive = false;
      CombatEnded?.Invoke();
      ResetState();
    }

    /// <summary>
    /// Ends the current combatant's turn and advances to the next eligible combatant.
    /// Returns <c>true</c> if a next turn was started; otherwise <c>false</c>.
    /// </summary>
    public bool AdvanceToNextTurn()
    {
      if (!_isActive) return false;
      if (_combatants.Count == 0)
      {
        StopCombat();
        return false;
      }

      // End previous turn if applicable.
      if (CurrentCombatant != null)
      {
        SafeEndTurn(CurrentCombatant);
        CurrentCombatant = null;
      }

      // Seek the next eligible combatant, skipping those that are not alive.
      int searched = 0;
      while (searched < _combatants.Count)
      {
        _currentIndex = WrapIndex(_currentIndex + 1, _combatants.Count);
        if (_currentIndex == 0)
        {
          _roundNumber++;
          RoundStarted?.Invoke(_roundNumber);
        }

        ICombatant candidate = _combatants[_currentIndex];
        if (candidate != null && candidate.IsAlive)
        {
          CurrentCombatant = candidate;
          SafeBeginTurn(candidate);
          return true;
        }

        searched++;
      }

      // No eligible combatants remain.
      StopCombat();
      return false;
    }

    /// <summary>
    /// Returns the index of the current combatant in the order, or -1 if none.
    /// </summary>
    public int GetCurrentIndex()
    {
      return _isActive ? _currentIndex : -1;
    }

    /// <summary>
    /// Resets internal state without altering the combatant list.
    /// </summary>
    private void ResetState()
    {
      _currentIndex = -1;
      _roundNumber = 0;
      CurrentCombatant = null;
    }

    private static int WrapIndex(int index, int count)
    {
      if (count <= 0) return -1;
      int result = index % count;
      return result < 0 ? result + count : result;
    }

    private void SafeBeginTurn(ICombatant combatant)
    {
      try
      {
        combatant.BeginTurn();
      }
      catch (Exception e)
      {
        Debug.LogException(e);
      }
      finally
      {
        TurnStarted?.Invoke(combatant);
      }
    }

    private void SafeEndTurn(ICombatant combatant)
    {
      try
      {
        combatant.EndTurn();
      }
      catch (Exception e)
      {
        Debug.LogException(e);
      }
      finally
      {
        TurnEnded?.Invoke(combatant);
      }
    }
  }
}

