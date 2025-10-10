using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace EchoesOfTheVoid.UI.Combat {
  /// <summary>
  /// Manages the combat log UI and handles message pooling and scrolling.
  /// </summary>
  public sealed class CombatLogPresenter {
    private const int MaxLogEntries = 60;

    private readonly MonoBehaviour _coroutineRunner;
    private readonly ScrollView _scrollView;
    private readonly VisualElement _logContainer;
    private readonly List<Label> _activeEntries = new();
    private readonly Stack<Label> _pooledEntries = new();
    private readonly WaitForSeconds _scrollDelay = new(0.01f);

    private Coroutine _scrollRoutine;

    public CombatLogPresenter(MonoBehaviour coroutineRunner, ScrollView scrollView, VisualElement logContainer) {
      _coroutineRunner = coroutineRunner;
      _scrollView = scrollView;
      _logContainer = logContainer;
    }

    public void AddMessage(string message, MessageType messageType) {
      if (_logContainer == null) {
        return;
      }

      Label entry = _pooledEntries.Count > 0 ? _pooledEntries.Pop() : new Label();
      entry.text = message;
      entry.style.whiteSpace = WhiteSpace.Normal;
      entry.style.flexGrow = 1f;
      entry.style.flexShrink = 1f;

      if (!entry.ClassListContains("log-message")) {
        entry.AddToClassList("log-message");
      }

      entry.RemoveFromClassList("log-message-normal");
      entry.RemoveFromClassList("log-message-damage");
      entry.RemoveFromClassList("log-message-healing");
      entry.RemoveFromClassList("log-message-system");

      switch (messageType) {
        case MessageType.Damage:
          entry.AddToClassList("log-message-damage");
          break;
        case MessageType.Healing:
          entry.AddToClassList("log-message-healing");
          break;
        case MessageType.System:
          entry.AddToClassList("log-message-system");
          break;
        default:
          entry.AddToClassList("log-message-normal");
          break;
      }

      _logContainer.Add(entry);
      _activeEntries.Add(entry);

      if (_activeEntries.Count > MaxLogEntries) {
        Label oldest = _activeEntries[0];
        _activeEntries.RemoveAt(0);
        _logContainer.Remove(oldest);
        _pooledEntries.Push(oldest);
      }

      ScheduleScrollToBottom();
    }

    public void Clear() {
      foreach (Label entry in _activeEntries) {
        if (entry == null) {
          continue;
        }

        _logContainer?.Remove(entry);
        _pooledEntries.Push(entry);
      }

      _activeEntries.Clear();

      if (_scrollRoutine != null) {
        _coroutineRunner.StopCoroutine(_scrollRoutine);
        _scrollRoutine = null;
      }
    }

    private void ScheduleScrollToBottom() {
      if (_coroutineRunner == null || _scrollView == null) {
        return;
      }

      if (_scrollRoutine != null) {
        _coroutineRunner.StopCoroutine(_scrollRoutine);
      }

      _scrollRoutine = _coroutineRunner.StartCoroutine(ScrollToBottomRoutine());
    }

    private IEnumerator ScrollToBottomRoutine() {
      yield return _scrollDelay;

      if (_scrollView != null && _activeEntries.Count > 0) {
        Label latest = _activeEntries[^1];
        _scrollView.ScrollTo(latest);
      }

      _scrollRoutine = null;
    }
  }
}
