using System;
using UnityEngine;
using UnityEngine.UIElements;

public abstract class UIScreen : MonoBehaviour {
  [SerializeField] protected string _screenId;
  [SerializeField] protected VisualTreeAsset _screenTemplate;

  protected VisualElement _rootElement;
  protected VisualElement _screenContainer;

  public string ScreenId => _screenId;
  public bool IsVisible => _screenContainer?.style.display == DisplayStyle.Flex;

  public event Action<UIScreen> OnScreenShown;
  public event Action<UIScreen> OnScreenHidden;

  public virtual void Initialize(VisualElement root) {
    _rootElement = root;

    if (_screenTemplate == null) {
      Debug.LogError($"Screen '{_screenId}' missing UXML template!");
      return;
    }

    _screenContainer = _screenTemplate.Instantiate();
    _rootElement.Add(_screenContainer);
    _screenContainer.style.display = DisplayStyle.None;

    SetupUI();
    BindEvents();
  }

  public virtual void Show() {
    if (_screenContainer != null) {
      _screenContainer.style.display = DisplayStyle.Flex;
      OnShow();
      OnScreenShown?.Invoke(this);
    }
  }

  public virtual void Hide() {
    if (_screenContainer != null) {
      _screenContainer.style.display = DisplayStyle.None;
      OnHide();
      OnScreenHidden?.Invoke(this);
    }
  }

  protected virtual void SetupUI() {
    // Override in derived classes to setup UI elements
  }

  protected virtual void BindEvents() {
    // Override in derived classes to bind button events, etc.
  }

  protected virtual void OnShow() {
    // Override in derived classes for custom show logic
  }

  protected virtual void OnHide() {
    // Override in derived classes for custom hide logic
  }

  protected T FindElement<T>(string name) where T : VisualElement {
    return _screenContainer?.Q<T>(name);
  }

  protected Button FindButton(string name) {
    return _screenContainer?.Q<Button>(name);
  }

  protected Label FindLabel(string name) {
    return _screenContainer?.Q<Label>(name);
  }
}