using System;
using System.Collections.Generic;
using System.Linq;
using EchoesOfTheVoid.Core.Combat.Gambits;
using EchoesOfTheVoid.Core.Combat.Systems;
using UnityEditor;
using UnityEngine;

namespace EchoesOfTheVoid.Editor.Gambits {
  public class GambitLogWindow : EditorWindow {
    private const int _maxEntries = 200;

    private readonly List<GambitEvaluationLog> _entries = new();
    private Vector2 _scrollPosition;
    private string _actorFilter = string.Empty;
    private bool _showOnlySuccessful;
    private bool _isBound;
    private bool _rebindScheduled;

    [MenuItem("Echoes/Gambit Log")]
    public static void ShowWindow() {
      GambitLogWindow window = GetWindow<GambitLogWindow>();
      window.titleContent = new GUIContent("Gambit Log");
      window.Show();
    }

    private void OnEnable() {
      EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
      TryBind();
    }

    private void OnDisable() {
      EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
      Unbind();
    }

    private void HandlePlayModeStateChanged(PlayModeStateChange state) {
      if (state == PlayModeStateChange.EnteredPlayMode) {
        _entries.Clear();
        TryBind();
      } else if (state == PlayModeStateChange.ExitingPlayMode) {
        Unbind();
      }
    }

    private void TryBind() {
      if (_isBound || !EditorApplication.isPlaying) {
        _rebindScheduled = false;
        return;
      }

      CombatSystem system = CombatSystem.Instance;
      if (system == null) {
        ScheduleRebind();
        return;
      }

      system.OnGambitEvaluated += HandleGambitEvaluated;
      _isBound = true;
      _rebindScheduled = false;
    }

    private void ScheduleRebind() {
      if (_rebindScheduled) {
        return;
      }

      _rebindScheduled = true;
      EditorApplication.delayCall += OnDelayBind;
    }

    private void OnDelayBind() {
      _rebindScheduled = false;
      TryBind();
    }

    private void Unbind() {
      if (!_isBound) {
        return;
      }

      CombatSystem system = CombatSystem.Instance;
      if (system != null) {
        system.OnGambitEvaluated -= HandleGambitEvaluated;
      }

      _isBound = false;
      _rebindScheduled = false;
    }

    private void HandleGambitEvaluated(GambitEvaluationLog log) {
      if (log == null) {
        return;
      }

      _entries.Add(log);
      if (_entries.Count > _maxEntries) {
        _entries.RemoveAt(0);
      }

      Repaint();
    }

    private void OnGUI() {
      DrawToolbar();

      if (!EditorApplication.isPlaying) {
        EditorGUILayout.HelpBox("Enter Play Mode to record gambit activity.", MessageType.Info);
        return;
      }

      if (!_isBound) {
        EditorGUILayout.HelpBox("CombatSystem instance not found. Start a battle to populate gambit logs.", MessageType.Warning);
        if (GUILayout.Button("Retry Bind")) {
          TryBind();
        }
        return;
      }

      List<GambitEvaluationLog> filtered = ApplyFilters(_entries);
      if (filtered.Count == 0) {
        EditorGUILayout.HelpBox("No gambit evaluations recorded yet with current filters.", MessageType.Info);
        return;
      }

      _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
      foreach (GambitEvaluationLog entry in filtered) {
        DrawEntry(entry);
      }
      EditorGUILayout.EndScrollView();
    }

    private void DrawToolbar() {
      _ = EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

      GUIStyle searchStyle = GUI.skin.FindStyle("ToolbarSeachTextField") ?? GUI.skin.FindStyle("ToolbarSearchTextField") ?? EditorStyles.toolbarTextField;
      string newFilter = GUILayout.TextField(_actorFilter, searchStyle, GUILayout.MinWidth(140f));
      if (!string.Equals(newFilter, _actorFilter, StringComparison.Ordinal)) {
        _actorFilter = newFilter;
      }

      _showOnlySuccessful = GUILayout.Toggle(_showOnlySuccessful, "Successful Only", EditorStyles.toolbarButton);

      if (GUILayout.Button("Clear", EditorStyles.toolbarButton)) {
        _entries.Clear();
      }

      GUILayout.FlexibleSpace();
      EditorGUILayout.EndHorizontal();
    }

    private List<GambitEvaluationLog> ApplyFilters(IEnumerable<GambitEvaluationLog> source) {
      IEnumerable<GambitEvaluationLog> query = source;

      if (!string.IsNullOrWhiteSpace(_actorFilter)) {
        query = query.Where(entry => entry.Actor != null && entry.Actor.Name.IndexOf(_actorFilter, StringComparison.OrdinalIgnoreCase) >= 0);
      }

      if (_showOnlySuccessful) {
        query = query.Where(entry => entry.HasMatch);
      }

      return query.ToList();
    }

    private void DrawEntry(GambitEvaluationLog entry) {
      using (new EditorGUILayout.VerticalScope(GUI.skin.box)) {
        string actorName = entry.Actor != null ? entry.Actor.Name : "<Unknown>";
        string header = $"{entry.Timestamp:HH:mm:ss} - {actorName}";
        EditorGUILayout.LabelField(header, EditorStyles.boldLabel);

        EditorGUILayout.LabelField("Profile", !string.IsNullOrEmpty(entry.ProfileName) ? entry.ProfileName : "<None>");

        if (entry.HasMatch && entry.SelectedAction != null) {
          string targetName = entry.SelectedTarget != null ? entry.SelectedTarget.Name : "<None>";
          EditorGUILayout.LabelField("Result", $"{entry.SelectedAction.ActionType} -> {targetName}");
        } else {
          EditorGUILayout.LabelField("Result", "No matching rule");
        }

        foreach (GambitRuleEvaluationRecord record in entry.Records) {
          DrawRecord(record);
        }
      }
    }

    private void DrawRecord(GambitRuleEvaluationRecord record) {
      string ruleName = string.IsNullOrEmpty(record.RuleName) ? "<Unnamed Rule>" : record.RuleName;
      string status = record.ActionBuilt
        ? "Success"
        : record.ConditionMatched
          ? "Passed Target"
          : "Skipped";

      string targetName = record.Target != null ? record.Target.Name : "--";
      string details = string.IsNullOrEmpty(record.FailureReason) ? string.Empty : $" - {record.FailureReason}";

      EditorGUILayout.LabelField($"- {ruleName}", $"{status} (Target: {targetName}){details}");
    }
  }
}
