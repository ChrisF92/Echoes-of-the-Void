using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using EchoesOfTheVoid.Core.Combat;
using EchoesOfTheVoid.Core.Combat.Entities;

namespace EchoesOfTheVoid.Core.Combat.Turn
{
  public class TurnOrderManager
  {
    private readonly List<CombatTurnEntry> _turnOrder = new();
    private int _currentTurnIndex;
    private int _roundCounter;

    public ICombatant CurrentCombatant => _turnOrder.Count > 0 ? _turnOrder[_currentTurnIndex].Combatant : null;
    public int CurrentRound => _roundCounter;

    public event Action<ICombatant> OnTurnStart;
    public event Action<ICombatant> OnTurnEnd;
    public event Action<int> OnNewRound;

    public void StartCombat(List<ICombatant> allCombatants)
    {
      InitializeTurnOrder(allCombatants);
      _currentTurnIndex = 0;
      _roundCounter = 1;
      StartCurrentTurn();
    }

    private void InitializeTurnOrder(List<ICombatant> combatants)
    {
      _turnOrder.Clear();
      foreach (var combatant in combatants.Where(c => c.IsAlive))
      {
        _turnOrder.Add(new CombatTurnEntry(combatant));
      }
      SortTurnOrder();
    }

    private void SortTurnOrder()
    {
      _turnOrder.Sort((a, b) =>
      {
        var speedComparison = b.Combatant.GetStat(StatType.Speed).CompareTo(a.Combatant.GetStat(StatType.Speed));
        if (speedComparison != 0)
        {
          return speedComparison;
        }

        var luckComparison = b.Combatant.GetStat(StatType.Luck).CompareTo(a.Combatant.GetStat(StatType.Luck));
        if (luckComparison != 0)
        {
          return luckComparison;
        }

        return UnityEngine.Random.Range(-1, 2);
      });
    }

    public void EndCurrentTurn()
    {
      var currentCombatant = CurrentCombatant;
      OnTurnEnd?.Invoke(currentCombatant);
      AdvanceToNextTurn();
    }

    private void AdvanceToNextTurn()
    {
      _currentTurnIndex++;

      if (_currentTurnIndex >= _turnOrder.Count)
      {
        _currentTurnIndex = 0;
        _roundCounter++;
        OnNewRound?.Invoke(_roundCounter);
      }

      while (_currentTurnIndex < _turnOrder.Count && !CurrentCombatant.IsAlive)
      {
        _currentTurnIndex++;
      }

      if (_currentTurnIndex >= _turnOrder.Count)
      {
        _currentTurnIndex = 0;
      }

      if (CurrentCombatant?.IsAlive == true)
      {
        StartCurrentTurn();
      }
    }

    private void StartCurrentTurn()
    {
      if (CurrentCombatant != null)
      {
        OnTurnStart?.Invoke(CurrentCombatant);
      }
    }

    public void RemoveCombatant(ICombatant combatant)
    {
      var entryToRemove = _turnOrder.FirstOrDefault(entry => entry.Combatant == combatant);
      if (entryToRemove != null)
      {
        var removedIndex = _turnOrder.IndexOf(entryToRemove);
        _turnOrder.Remove(entryToRemove);

        if (removedIndex <= _currentTurnIndex)
        {
          _currentTurnIndex = Math.Max(0, _currentTurnIndex - 1);
        }

        if (_currentTurnIndex >= _turnOrder.Count)
        {
          _currentTurnIndex = 0;
        }
      }
    }

    public List<ICombatant> GetTurnOrder()
    {
      return _turnOrder.Select(entry => entry.Combatant).ToList();
    }

    public int GetTurnPosition(ICombatant combatant)
    {
      for (var i = 0; i < _turnOrder.Count; i++)
      {
        if (_turnOrder[i].Combatant == combatant)
        {
          return i;
        }
      }
      return -1;
    }
  }
}
