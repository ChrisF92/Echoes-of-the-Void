using System;
using System.Collections.Generic;
using System.Globalization;
using EchoesOfTheVoid.Core.Combat;
using EchoesOfTheVoid.Core.Combat.Run;
using EchoesOfTheVoid.Core.Inventory.ScriptableObjects;
using UnityEngine;
using UnityEngine.UIElements;

namespace EchoesOfTheVoid.UI.Modals {
  /// <summary>
  /// Modal dialog that summarises the outcome of a combat run.
  /// </summary>
  [DisallowMultipleComponent]
  public sealed class CombatRunResultsModal : UIModal {
    private Label _titleLabel;
    private Label _summaryLabel;
    private Label _experienceLabel;
    private Label _currencyLabel;
    private ListView _itemListView;
    private Label _itemsEmptyLabel;
    private ListView _floorListView;
    private Label _floorsEmptyLabel;
    private Button _confirmButton;

    private readonly List<string> _itemRows = new();
    private readonly List<string> _floorRows = new();

    public event Action OnConfirmed;

    protected override void SetupUI() {
      base.SetupUI();

      _titleLabel = FindLabel("results-title");
      _summaryLabel = FindLabel("results-summary");
      _experienceLabel = FindLabel("results-exp");
      _currencyLabel = FindLabel("results-currency");
      _itemListView = FindElement<ListView>("results-item-list");
      _itemsEmptyLabel = FindLabel("results-item-empty");
      _floorListView = FindElement<ListView>("results-floor-list");
      _floorsEmptyLabel = FindLabel("results-floor-empty");
      _confirmButton = FindButton("results-confirm-button");

      ConfigureListView(_itemListView, _itemRows);
      ConfigureListView(_floorListView, _floorRows);
      UpdateEmptyStates();
    }

    protected override void BindEvents() {
      base.BindEvents();
      if (_confirmButton != null) {
        _confirmButton.clicked += HandleConfirmClicked;
      }
    }

    private void OnDisable() {
      if (_confirmButton != null) {
        _confirmButton.clicked -= HandleConfirmClicked;
      }
    }

    public void ShowResults(CombatRunState state) {
      if (state == null) {
        Debug.LogWarning("CombatRunResultsModal.ShowResults called with null state.", this);
        return;
      }

      PopulateSummary(state);
      PopulateRewards(state.Rewards);
      PopulateFloors(state.FloorResults);
      UpdateEmptyStates();

      Show();
    }

    private void PopulateSummary(CombatRunState state) {
      string outcome = ResolveOutcome(state);
      _titleLabel.text = outcome;

      int clearedFloors = state.FloorResults.Count;
      int totalFloors = state.Definition != null ? state.Definition.FloorCount : clearedFloors;
      totalFloors = Mathf.Max(totalFloors, 1);

      _summaryLabel.text = $"{clearedFloors}/{totalFloors} floors cleared";

      _experienceLabel.text = $"EXP: {Mathf.Max(0, state.Rewards?.Experience ?? 0)}";
      _currencyLabel.text = $"Echoes: {Mathf.Max(0, state.Rewards?.Currency ?? 0)}";
    }

    private void PopulateRewards(CombatRunRewards rewards) {
      _itemRows.Clear();
      if (rewards == null) {
        return;
      }

      foreach (KeyValuePair<ItemScriptableObject, int> entry in rewards.ItemTotals) {
        if (entry.Key == null || entry.Value <= 0) {
          continue;
        }

        string displayName = !string.IsNullOrWhiteSpace(entry.Key.DisplayName)
          ? entry.Key.DisplayName
          : entry.Key.name;

        _itemRows.Add($"{entry.Value} x {displayName}");
      }

      _itemListView?.RefreshItems();
    }

    private void PopulateFloors(IReadOnlyList<CombatRunFloorResult> floors) {
      _floorRows.Clear();
      if (floors == null) {
        return;
      }

      for (int i = 0; i < floors.Count; i++) {
        CombatRunFloorResult result = floors[i];
        if (result == null) {
          continue;
        }

        string floorName = result.Definition != null
          ? result.Definition.DisplayName
          : $"Floor {result.FloorIndex + 1}";

        string outcome = result.Outcome.ToString();
        string duration = FormatDuration(result.DurationSeconds);
        string turns = result.TurnCount > 0 ? $"{result.TurnCount} turns" : "-";

        _floorRows.Add($"{floorName}: {outcome} - {duration} - {turns}");
      }

      _floorListView?.RefreshItems();
    }

    private static string ResolveOutcome(CombatRunState state) {
      if (state.WasCancelled) {
        return "Run Cancelled";
      }

      if (state.FloorResults.Count == 0) {
        return "Run Incomplete";
      }

      CombatRunFloorResult last = state.FloorResults[^1];
      return last.Outcome switch {
        CombatOutcome.Victory when state.Definition != null && state.FloorResults.Count >= state.Definition.FloorCount
          => "Run Completed",
        CombatOutcome.Victory => "Run Paused",
        CombatOutcome.Defeat => "Party Defeated",
        CombatOutcome.Draw => "Run Ended (Draw)",
        CombatOutcome.Escape => "Party Escaped",
        _ => "Run Complete"
      };
    }

    private static string FormatDuration(float seconds) {
      if (seconds <= 0f || float.IsNaN(seconds) || float.IsInfinity(seconds)) {
        return "00:00";
      }

      TimeSpan span = TimeSpan.FromSeconds(seconds);
      if (span.TotalHours >= 1d) {
        return span.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture);
      }

      return span.ToString(@"mm\:ss", CultureInfo.InvariantCulture);
    }

    private void ConfigureListView(ListView listView, List<string> source) {
      if (listView == null) {
        return;
      }

      listView.itemsSource = source;
      listView.selectionType = SelectionType.None;
      listView.makeItem = static () => new Label {
        pickingMode = PickingMode.Ignore
      };
      listView.bindItem = (element, index) => {
        if (element is Label label && index >= 0 && index < source.Count) {
          label.text = source[index];
        }
      };
    }

    private void UpdateEmptyStates() {
      if (_itemsEmptyLabel != null) {
        _itemsEmptyLabel.style.display = _itemRows.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
      }

      if (_floorsEmptyLabel != null) {
        _floorsEmptyLabel.style.display = _floorRows.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
      }
    }

    private void HandleConfirmClicked() {
      Hide();
      OnConfirmed?.Invoke();
    }
  }
}
