using System;
using System.Collections.Generic;
using System.Linq;
using EchoesOfTheVoid.Core.Combat.Components;
using EchoesOfTheVoid.Core.Combat.Entities;

namespace EchoesOfTheVoid.Core.Combat.Turn {
  public class TurnOrderManager {
    private readonly List<CombatTurnEntry> _turnOrder = new();
    private int _currentTurnIndex;

    public ICombatant CurrentCombatant => _turnOrder.Count > 0 ? _turnOrder[_currentTurnIndex].Combatant : null;
    public int CurrentRound { get; private set; }

    public event Action<ICombatant> OnTurnStart;
    public event Action<ICombatant> OnTurnEnd;
    public event Action<int> OnNewRound;

    public void StartCombat(List<ICombatant> allCombatants) {
      InitializeTurnOrder(allCombatants);
      _currentTurnIndex = 0;
      CurrentRound = 1;
      StartCurrentTurn();
    }

    private void InitializeTurnOrder(List<ICombatant> combatants) {
      _turnOrder.Clear();
      foreach (ICombatant combatant in combatants.Where(static c => c.IsAlive)) {
        _turnOrder.Add(new CombatTurnEntry(combatant));
      }
      SortTurnOrder();
    }

    private void SortTurnOrder() {
      _turnOrder.Sort(static (a, b) => {
        int speedComparison = b.Combatant.GetStat(StatType.Speed).CompareTo(a.Combatant.GetStat(StatType.Speed));
        if (speedComparison != 0) {
          return speedComparison;
        }

        int luckComparison = b.Combatant.GetStat(StatType.Luck).CompareTo(a.Combatant.GetStat(StatType.Luck));
        return luckComparison != 0 ? luckComparison : UnityEngine.Random.Range(-1, 2);
      });
    }

    public void EndCurrentTurn() {
      ICombatant currentCombatant = CurrentCombatant;
      SkillComponent skillComponent = currentCombatant?.GetComponent<SkillComponent>();
      skillComponent?.OnTurnEnd();
      OnTurnEnd?.Invoke(currentCombatant);
      AdvanceToNextTurn();
    }

    private void AdvanceToNextTurn() {
      _currentTurnIndex++;

      if (_currentTurnIndex >= _turnOrder.Count) {
        _currentTurnIndex = 0;
        CurrentRound++;
        OnNewRound?.Invoke(CurrentRound);
      }

      while (_currentTurnIndex < _turnOrder.Count && !CurrentCombatant.IsAlive) {
        _currentTurnIndex++;
      }

      if (_currentTurnIndex >= _turnOrder.Count) {
        _currentTurnIndex = 0;
      }

      if (CurrentCombatant?.IsAlive == true) {
        StartCurrentTurn();
      }
    }

    private void StartCurrentTurn() {
      if (CurrentCombatant != null) {
        OnTurnStart?.Invoke(CurrentCombatant);
      }
    }

    public void RemoveCombatant(ICombatant combatant) {
      CombatTurnEntry entryToRemove = _turnOrder.FirstOrDefault(entry => entry.Combatant == combatant);
      if (entryToRemove != null) {
        int removedIndex = _turnOrder.IndexOf(entryToRemove);
        _ = _turnOrder.Remove(entryToRemove);

        if (removedIndex <= _currentTurnIndex) {
          _currentTurnIndex = Math.Max(0, _currentTurnIndex - 1);
        }

        if (_currentTurnIndex >= _turnOrder.Count) {
          _currentTurnIndex = 0;
        }
      }
    }

    public List<ICombatant> GetTurnOrder() {
      return _turnOrder.Select(static entry => entry.Combatant).ToList();
    }

    public int GetTurnPosition(ICombatant combatant) {
      for (int i = 0; i < _turnOrder.Count; i++) {
        if (_turnOrder[i].Combatant == combatant) {
          return i;
        }
      }
      return -1;
    }
  }
}
