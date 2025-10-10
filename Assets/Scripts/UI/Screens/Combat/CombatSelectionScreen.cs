using System.Collections;
using System.Collections.Generic;
using System.Linq;
using EchoesOfTheVoid.Core.Combat;
using EchoesOfTheVoid.Core.Combat.Entities;
using EchoesOfTheVoid.Core.Combat.Run;
using EchoesOfTheVoid.UI.Modals;
using UnityEngine;
using UnityEngine.UIElements;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace EchoesOfTheVoid.UI.Combat {
  /// <summary>
  /// Screen for selecting and managing combat runs.
  /// </summary>
  public sealed class CombatSelectionScreen : UIScreen {
    [Header("References")]
    [SerializeField] private CombatRunController _runController;
    [SerializeField] private CombatScreen _combatViewController;
    [SerializeField] private CombatRunResultsModal _resultsModal;

    [Header("Runs")]
    [SerializeField] private List<CombatRunDefinition> _runDefinitions = new();

    [Header("Floor Transitions")]
    [SerializeField, Min(0f)] private float _transitionHoldSeconds = 0.2f;
    [SerializeField, Min(0f)] private float _transitionFadeSeconds = 0.35f;
    [SerializeField, Min(0f)] private float _autoAdvanceNextFloorDelay = 0.5f;

    private VisualElement _selectionPanel;
    private ListView _runListView;
    private Label _runTitleLabel;
    private Label _runDescriptionLabel;
    private Label _runFloorsLabel;
    private Button _startButton;

    private VisualElement _runPanel;
    private Label _currentRunTitleLabel;
    private Label _floorsProgressLabel;
    private Label _currentFloorLabel;
    private Label _runStatusLabel;
    private Button _nextFloorButton;
    private Button _quitRunButton;
    private VisualElement _fadeOverlay;

    private readonly List<RunListEntry> _runEntries = new();
    private int _selectedRunIndex = -1;
    private bool _awaitingNextFloor;
    private Coroutine _fadeRoutine;
    private Coroutine _autoAdvanceRoutine;

    private CombatRunDefinition SelectedRun => _selectedRunIndex >= 0 && _selectedRunIndex < _runEntries.Count
      ? _runEntries[_selectedRunIndex].Definition
      : null;

    public override void Initialize(VisualElement root) {
      base.Initialize(root);
      ConfigureRunList();
      UpdateSelectionUI();
      UpdateRunControls();
    }

    protected override void SetupUI() {
      ResolveDependencies();

      _selectionPanel = FindElement<VisualElement>("combat-selection-panel");
      _runListView = FindElement<ListView>("combat-run-list");
      _runTitleLabel = FindLabel("combat-run-title");
      _runDescriptionLabel = FindLabel("combat-run-description");
      _runFloorsLabel = FindLabel("combat-run-floors");
      _startButton = FindButton("combat-start-button");

      _runPanel = FindElement<VisualElement>("combat-run-panel");
      _currentRunTitleLabel = FindLabel("combat-current-run-title");
      _floorsProgressLabel = FindLabel("combat-floors-remaining");
      _currentFloorLabel = FindLabel("combat-current-floor");
      _runStatusLabel = FindLabel("combat-run-status");
      _nextFloorButton = FindButton("combat-next-floor-button");
      _quitRunButton = FindButton("combat-quit-button");
      _fadeOverlay = FindElement<VisualElement>("combat-fade-overlay");

      if (_fadeOverlay != null) {
        _fadeOverlay.style.display = DisplayStyle.None;
        _fadeOverlay.style.opacity = 0f;
      }
    }

    protected override void BindEvents() {
      if (_runListView != null) {
        _runListView.selectionChanged += HandleRunSelectionChanged;
      }

      _startButton?.RegisterCallback<ClickEvent>(_ => StartSelectedRun());
      _nextFloorButton?.RegisterCallback<ClickEvent>(_ => { RequestNextFloor(); });
      _quitRunButton?.RegisterCallback<ClickEvent>(_ => CancelActiveRun());

      if (_resultsModal != null) {
        _resultsModal.OnConfirmed += HandleResultsConfirmed;
      }
    }

    protected override void OnShow() {
      base.OnShow();
      SubscribeRunController();
      RefreshRunList();
      UpdateSelectionUI();
      UpdateRunControls();
    }

    protected override void OnHide() {
      base.OnHide();
      StopFadeRoutine();
      StopAutoAdvanceRoutine();
    }

    private void OnDisable() {
      UnsubscribeRunController();
      StopAutoAdvanceRoutine();

      if (_runListView != null) {
        _runListView.selectionChanged -= HandleRunSelectionChanged;
      }

      if (_resultsModal != null) {
        _resultsModal.OnConfirmed -= HandleResultsConfirmed;
      }
    }

    private void ResolveDependencies() {
      if (_runController == null) {
        _runController = FindFirstObjectByType<CombatRunController>();
      }

      if (_combatViewController == null) {
        _combatViewController = FindFirstObjectByType<CombatScreen>();
      }

      if (_resultsModal == null) {
        _resultsModal = FindFirstObjectByType<CombatRunResultsModal>();
      }
    }

    private void ConfigureRunList() {
      if (_runListView == null) {
        return;
      }

      _runListView.itemsSource = _runEntries;
      _runListView.selectionType = SelectionType.Single;
      _runListView.makeItem = static () => new Label {
        pickingMode = PickingMode.Ignore,
        name = "combat-run-list-item"
      };
      _runListView.bindItem = (element, index) => {
        if (element is not Label label || index < 0 || index >= _runEntries.Count) {
          return;
        }

        RunListEntry entry = _runEntries[index];
        label.text = $"{entry.DisplayName} ({entry.FloorCount} floors)";
      };
    }

    private void RefreshRunList() {
      _runEntries.Clear();

      var seen = new HashSet<CombatRunDefinition>();
      foreach (CombatRunDefinition definition in _runDefinitions) {
        if (definition == null || !seen.Add(definition)) {
          continue;
        }

        _runEntries.Add(new RunListEntry(definition));
      }

      CombatRunDefinition activeDefinition = _runController != null && _runController.State.IsActive
        ? _runController.State.Definition
        : null;

      if (activeDefinition != null && seen.Add(activeDefinition)) {
        _runEntries.Add(new RunListEntry(activeDefinition));
      }

      _runListView?.RefreshItems();

      if (_selectedRunIndex < 0 && _runEntries.Count > 0) {
        _selectedRunIndex = 0;
        if (_runListView != null) {
          _runListView.selectedIndex = 0;
        }
      } else if (_selectedRunIndex >= _runEntries.Count) {
        _selectedRunIndex = _runEntries.Count - 1;
        if (_runListView != null) {
          _runListView.selectedIndex = _selectedRunIndex;
        }
      }
    }

    private void HandleRunSelectionChanged(IEnumerable<object> _) {
      _selectedRunIndex = _runListView != null ? _runListView.selectedIndex : -1;
      UpdateSelectionUI();
      UpdateRunControls();
    }

    private void UpdateSelectionUI() {
      CombatRunDefinition selected = SelectedRun;
      if (selected == null) {
        if (_runTitleLabel != null) {
          _runTitleLabel.text = "Select a run";
        }

        if (_runFloorsLabel != null) {
          _runFloorsLabel.text = string.Empty;
        }

        if (_runDescriptionLabel != null) {
          _runDescriptionLabel.text = string.Empty;
        }

        return;
      }

      if (_runTitleLabel != null) {
        _runTitleLabel.text = selected.DisplayName;
      }

      if (_runFloorsLabel != null) {
        _runFloorsLabel.text = $"{selected.FloorCount} floors";
      }

      if (_runDescriptionLabel != null) {
        _runDescriptionLabel.text = selected.Description;
      }
    }

    private void UpdateRunControls() {
      bool runActive = _runController != null && _runController.IsRunning;

      _startButton?.SetEnabled(!runActive && SelectedRun != null);
      _runListView?.SetEnabled(!runActive);

      if (_selectionPanel != null) {
        _selectionPanel.style.display = runActive ? DisplayStyle.None : DisplayStyle.Flex;
      }

      if (_runPanel != null) {
        _runPanel.style.display = runActive ? DisplayStyle.Flex : DisplayStyle.None;
      }

      ToggleNextFloorButton(runActive && _awaitingNextFloor);
      _quitRunButton?.SetEnabled(runActive);

      if (!runActive && _runStatusLabel != null) {
        _runStatusLabel.text = "Select a run to begin.";
      }
    }

    private void StartSelectedRun() {
      if (_runController == null) {
        Debug.LogWarning("CombatSelectionScreen requires a CombatRunController.", this);
        return;
      }

      CombatRunDefinition selected = SelectedRun ?? _runController.State.Definition ?? _runDefinitions.FirstOrDefault();
      if (selected == null) {
        Debug.LogWarning("No combat run definition available to start.", this);
        return;
      }

      if (_runStatusLabel != null) {
        _runStatusLabel.text = "Preparing first floor...";
      }

      StopAutoAdvanceRoutine();

      bool started = _runController.StartRun(selected);
      if (!started) {
        Debug.LogWarning("Unable to start combat run.", this);
        if (_runStatusLabel != null) {
          _runStatusLabel.text = "Unable to start run.";
        }
      }
    }

    private bool RequestNextFloor() {
      if (_runController == null || !_awaitingNextFloor) {
        return false;
      }

      StopAutoAdvanceRoutine();

      if (!_runController.HasPendingNextFloor) {
        ScheduleAutoAdvanceNextFloor();
        return false;
      }

      bool moved = _runController.ProceedToNextFloor();
      if (!moved) {
        ScheduleAutoAdvanceNextFloor();
        return false;
      }

      _awaitingNextFloor = false;
      ToggleNextFloorButton(false);
      if (_runStatusLabel != null) {
        _runStatusLabel.text = "Advancing to next floor...";
      }

      return true;
    }

    private void CancelActiveRun() {
      if (_runController == null || !_runController.IsRunning) {
        return;
      }

      StopAutoAdvanceRoutine();
      _runController.CancelRun();
    }

    private void HandleRunStarted(CombatRunState state) {
      if (state?.Definition == null) {
        return;
      }

      StopAutoAdvanceRoutine();
      _awaitingNextFloor = false;
      ToggleNextFloorButton(false);

      if (_currentRunTitleLabel != null) {
        _currentRunTitleLabel.text = state.Definition.DisplayName;
      }
      UpdateFloorProgress(state.CurrentFloorIndex + 1, state.Definition.FloorCount);
      if (_runStatusLabel != null) {
        _runStatusLabel.text = "Run started. Awaiting floor...";
      }

      UpdateRunControls();
    }

    private void HandleFloorStarted(
      CombatRunFloorDefinition floor,
      int floorIndex,
      IReadOnlyList<Combatant> playerParty,
      IReadOnlyList<Combatant> enemyParty) {

      StopAutoAdvanceRoutine();

      if (floor != null && _currentFloorLabel != null) {
        _currentFloorLabel.text = $"{floor.DisplayName} (Floor {floorIndex + 1})";
      }

      CombatRunDefinition definition = _runController?.State.Definition;
      int totalFloors = definition != null ? definition.FloorCount : 0;
      UpdateFloorProgress(floorIndex + 1, totalFloors);

      if (_runStatusLabel != null) {
        _runStatusLabel.text = "Battle in progress...";
      }
      _awaitingNextFloor = false;
      ToggleNextFloorButton(false);

      InitializeCombatView(playerParty, enemyParty);
      if (NavigationManager.Instance == null || NavigationManager.Instance.IsScreenActive(_screenId)) {
        PlayFloorTransition();
      }
    }

    private void HandleFloorCompleted(CombatRunFloorResult result) {
      if (result == null) {
        return;
      }

      string duration = FormatDuration(result.DurationSeconds);
      string status = result.Outcome == CombatOutcome.Victory
        ? $"Cleared {result.Definition?.DisplayName ?? $"Floor {result.FloorIndex + 1}"} in {duration}."
        : $"Party {result.Outcome.ToString().ToLowerInvariant()} on {result.Definition?.DisplayName ?? $"Floor {result.FloorIndex + 1}"}";

      if (result.TurnCount > 0) {
        status += $" ({result.TurnCount} turns)";
      }

      if (_runStatusLabel != null) {
        _runStatusLabel.text = status;
      }

      CombatRunState state = _runController?.State;
      bool moreFloorsRemain = state != null && state.Definition != null && state.FloorResults.Count < state.Definition.FloorCount;

      _awaitingNextFloor = result.Outcome == CombatOutcome.Victory && moreFloorsRemain;
      ToggleNextFloorButton(_awaitingNextFloor);

      bool isSelectionVisible = NavigationManager.Instance != null
        ? NavigationManager.Instance.IsScreenActive(_screenId)
        : IsVisible;

      if (_awaitingNextFloor && isSelectionVisible) {
        ScheduleAutoAdvanceNextFloor();
      } else {
        StopAutoAdvanceRoutine();
      }
    }

    private void HandleRunCompleted(CombatRunState state) {
      StopAutoAdvanceRoutine();
      _awaitingNextFloor = false;
      ToggleNextFloorButton(false);
      UpdateRunControls();
      HideCombatView();

      if (_resultsModal != null) {
        _resultsModal.ShowResults(state);
      } else {
        // Fall back to re-enabling selection if no modal is configured.
        HandleResultsConfirmed();
      }
    }

    private void HandleRunCancelled(CombatRunState state) {
      StopAutoAdvanceRoutine();
      _awaitingNextFloor = false;
      ToggleNextFloorButton(false);

      if (_runStatusLabel != null) {
        _runStatusLabel.text = "Run cancelled.";
      }
      StopFadeRoutine();
      ClearCombatView();

      UpdateRunControls();
      RefreshRunList();
    }

    private void HandleResultsConfirmed() {
      if (_runController != null) {
        _runController.ReleaseRunState();
      }

      _awaitingNextFloor = false;
      ToggleNextFloorButton(false);
      RefreshRunList();
      UpdateSelectionUI();
      UpdateRunControls();
      ClearCombatView();
    }

    private void SubscribeRunController() {
      if (_runController == null) {
        ResolveDependencies();
      }

      if (_runController == null) {
        return;
      }

      _runController.OnRunStarted -= HandleRunStarted;
      _runController.OnRunStarted += HandleRunStarted;

      _runController.OnFloorStarted -= HandleFloorStarted;
      _runController.OnFloorStarted += HandleFloorStarted;

      _runController.OnFloorCompleted -= HandleFloorCompleted;
      _runController.OnFloorCompleted += HandleFloorCompleted;

      _runController.OnRunCompleted -= HandleRunCompleted;
      _runController.OnRunCompleted += HandleRunCompleted;

      _runController.OnRunCancelled -= HandleRunCancelled;
      _runController.OnRunCancelled += HandleRunCancelled;
    }

    private void UnsubscribeRunController() {
      if (_runController == null) {
        return;
      }

      _runController.OnRunStarted -= HandleRunStarted;
      _runController.OnFloorStarted -= HandleFloorStarted;
      _runController.OnFloorCompleted -= HandleFloorCompleted;
      _runController.OnRunCompleted -= HandleRunCompleted;
      _runController.OnRunCancelled -= HandleRunCancelled;
    }

    private void InitializeCombatView(
      IReadOnlyList<Combatant> playerParty,
      IReadOnlyList<Combatant> enemyParty) {

      if (_combatViewController == null) {
        return;
      }

      ShowCombatView();

      var players = playerParty != null
        ? new List<Combatant>(playerParty.Where(static c => c != null))
        : new List<Combatant>();

      var enemies = enemyParty != null
        ? new List<Combatant>(enemyParty.Where(static c => c != null))
        : new List<Combatant>();

      _combatViewController.InitializeBattle(players, enemies);
    }

    private void ClearCombatView() {
      if (_combatViewController == null) {
        return;
      }

      _combatViewController.InitializeBattle(new List<Combatant>(), new List<Combatant>());
      HideCombatView();
    }

    private void PlayFloorTransition() {
      if (_fadeOverlay == null) {
        return;
      }

      StopFadeRoutine();
      _fadeOverlay.style.display = DisplayStyle.Flex;
      _fadeOverlay.style.opacity = 1f;
      _fadeRoutine = StartCoroutine(FadeOverlayRoutine());
    }

    private IEnumerator FadeOverlayRoutine() {
      yield return new WaitForSeconds(_transitionHoldSeconds);
      if (_fadeOverlay == null) {
        yield break;
      }

      float elapsed = 0f;
      while (elapsed < _transitionFadeSeconds) {
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / _transitionFadeSeconds);
        _fadeOverlay.style.opacity = 1f - t;
        yield return null;
      }

      _fadeOverlay.style.opacity = 0f;
      _fadeOverlay.style.display = DisplayStyle.None;
      _fadeRoutine = null;
    }

    private void StopFadeRoutine() {
      if (_fadeRoutine != null) {
        StopCoroutine(_fadeRoutine);
        _fadeRoutine = null;
      }

      if (_fadeOverlay != null) {
        _fadeOverlay.style.opacity = 0f;
        _fadeOverlay.style.display = DisplayStyle.None;
      }
    }

    private void ScheduleAutoAdvanceNextFloor() {
      if (!_awaitingNextFloor) {
        StopAutoAdvanceRoutine();
        return;
      }

      StopAutoAdvanceRoutine();
      _autoAdvanceRoutine = StartCoroutine(AutoAdvanceNextFloorRoutine());
    }

    private IEnumerator AutoAdvanceNextFloorRoutine() {
      float elapsed = 0f;
      while (_awaitingNextFloor && _runController != null) {
        if (_runController.HasPendingNextFloor && elapsed >= _autoAdvanceNextFloorDelay) {
          _autoAdvanceRoutine = null;
          RequestNextFloor();
          yield break;
        }

        elapsed += Time.deltaTime;
        yield return null;
      }

      _autoAdvanceRoutine = null;
    }

    private void StopAutoAdvanceRoutine() {
      if (_autoAdvanceRoutine != null) {
        StopCoroutine(_autoAdvanceRoutine);
        _autoAdvanceRoutine = null;
      }
    }

    private void ToggleNextFloorButton(bool enabled) {
      if (_nextFloorButton == null) {
        return;
      }

      _nextFloorButton.style.display = enabled ? DisplayStyle.Flex : DisplayStyle.None;
      _nextFloorButton.SetEnabled(enabled);
    }

    private void UpdateFloorProgress(int currentFloor, int totalFloors) {
      if (_floorsProgressLabel == null) {
        return;
      }

      if (totalFloors <= 0) {
        _floorsProgressLabel.text = string.Empty;
        return;
      }

      currentFloor = Mathf.Clamp(currentFloor, 0, totalFloors);
      _floorsProgressLabel.text = $"{currentFloor}/{totalFloors} floors";
    }

    private void ShowCombatView() {
      if (_combatViewController == null) {
        return;
      }

      if (NavigationManager.Instance != null) {
        string combatScreenId = _combatViewController.ScreenId;
        if (!string.IsNullOrEmpty(combatScreenId) && !NavigationManager.Instance.IsScreenActive(combatScreenId)) {
          NavigationManager.Instance.NavigateToScreen(combatScreenId);
        }
      } else if (!_combatViewController.IsVisible) {
        _combatViewController.Show();
      }
    }

    private void HideCombatView() {
      if (_combatViewController == null) {
        return;
      }

      if (NavigationManager.Instance != null) {
        if (!string.IsNullOrEmpty(_screenId) && !NavigationManager.Instance.IsScreenActive(_screenId)) {
          NavigationManager.Instance.NavigateToScreen(_screenId, addToHistory: false);
        }
      } else if (_combatViewController.IsVisible) {
        _combatViewController.Hide();
      }
    }

    private static string FormatDuration(float seconds) {
      if (seconds <= 0f || float.IsNaN(seconds) || float.IsInfinity(seconds)) {
        return "00:00";
      }

      System.TimeSpan span = System.TimeSpan.FromSeconds(seconds);
      return span.TotalHours >= 1d
        ? span.ToString(@"hh\:mm\:ss")
        : span.ToString(@"mm\:ss");
    }

    private readonly struct RunListEntry {
      public RunListEntry(CombatRunDefinition definition) {
        Definition = definition;
        DisplayName = definition != null ? definition.DisplayName : "Unnamed Run";
        Description = definition != null ? definition.Description : string.Empty;
        FloorCount = definition != null ? definition.FloorCount : 0;
      }

      public CombatRunDefinition Definition { get; }
      public string DisplayName { get; }
      public string Description { get; }
      public int FloorCount { get; }
    }

#if UNITY_EDITOR
    private void OnValidate() {
      if (string.IsNullOrEmpty(_screenId)) {
        _screenId = "CombatSelectionScreen";
      }

      if (_screenTemplate == null) {
        _screenTemplate = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI Toolkit/Screens/Combat/CombatSelectionScreen.uxml");
      }
    }
#endif
  }
}
