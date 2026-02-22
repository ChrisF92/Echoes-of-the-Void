using System;
using UnityEngine;
using UnityEngine.UIElements;

public abstract class UIModal : MonoBehaviour {
  [SerializeField] protected string _modalId;
  [SerializeField] protected VisualTreeAsset _modalTemplate;
  [SerializeField] protected bool _closeOnBackgroundClick = true;

  protected VisualElement _rootElement;
  protected VisualElement _modalContainer;
  protected VisualElement _backdrop;

  private bool _isVisible;

  public string ModalId => _modalId;
  public bool IsVisible => _isVisible;

  public event Action<UIModal> OnModalShown;
  public event Action<UIModal> OnModalHidden;

  public virtual void Initialize(VisualElement root) {
    _rootElement = root;
    CreateModalStructure();
    SetupUI();
    BindEvents();
  }

  protected virtual void CreateModalStructure() {
    if (_modalTemplate == null) {
      Debug.LogError($"Modal '{_modalId}' missing UXML template!");
      return;
    }

    // Create backdrop/scrim
    _backdrop = new VisualElement {
      name = $"{_modalId}-backdrop"
    };
    _backdrop.AddToClassList("modal-backdrop");
    _backdrop.style.position = Position.Absolute;
    _backdrop.style.left = 0;
    _backdrop.style.top = 0;
    _backdrop.style.right = 0;
    _backdrop.style.bottom = 0;
    _backdrop.style.justifyContent = Justify.Center;
    _backdrop.style.alignItems = Align.Center;
    _backdrop.style.backgroundColor = new Color(0, 0, 0, 0.5f);
    _backdrop.style.display = DisplayStyle.None;

    // Block all input by consuming events
    _backdrop.pickingMode = PickingMode.Position;
    _backdrop.focusable = true;

    if (_closeOnBackgroundClick) {
      _backdrop.RegisterCallback<ClickEvent>(OnBackdropClick);
    }

    // Instantiate modal from template
    _modalContainer = _modalTemplate.Instantiate();
    _modalContainer.style.position = Position.Relative;
    _modalContainer.style.left = StyleKeyword.Undefined;
    _modalContainer.style.top = StyleKeyword.Undefined;
    _modalContainer.style.right = StyleKeyword.Undefined;
    _modalContainer.style.bottom = StyleKeyword.Undefined;
    _modalContainer.style.display = DisplayStyle.Flex;

    _backdrop.Add(_modalContainer);
    _rootElement.Add(_backdrop);
  }

  public virtual void Show() {
    if (_backdrop != null) {
      _backdrop.BringToFront();
      _backdrop.style.display = DisplayStyle.Flex;
      if (_modalContainer != null) {
        _modalContainer.BringToFront();
        _modalContainer.style.display = DisplayStyle.Flex;
      }
      _backdrop.Focus(); // Ensure modal has focus to block input
      _isVisible = true;
      OnShow();
      OnModalShown?.Invoke(this);
    }
  }

  public virtual void Hide() {
    if (_backdrop != null) {
      _backdrop.style.display = DisplayStyle.None;
      if (_modalContainer != null) {
        _modalContainer.style.display = DisplayStyle.None;
      }
      _isVisible = false;
      OnHide();
      OnModalHidden?.Invoke(this);
    }
  }

  protected virtual void OnBackdropClick(ClickEvent evt) {
    if (evt.target == _backdrop) {
      Hide();
    }
  }

  private void ConsumeEvent<T>(T evt) where T : EventBase<T>, new() {
    if (evt.target == _backdrop) {
      evt.StopPropagation();
    }
  }

  protected virtual void SetupUI() {
    // Override in derived classes to setup UI elements
  }

  protected virtual void BindEvents() {
    // Consume all events to prevent input from reaching elements behind the modal
    _backdrop.RegisterCallback<PointerDownEvent>(ConsumeEvent, TrickleDown.TrickleDown);
    _backdrop.RegisterCallback<PointerUpEvent>(ConsumeEvent, TrickleDown.TrickleDown);
    _backdrop.RegisterCallback<PointerMoveEvent>(ConsumeEvent, TrickleDown.TrickleDown);
    _backdrop.RegisterCallback<KeyDownEvent>(ConsumeEvent, TrickleDown.TrickleDown);
    _backdrop.RegisterCallback<KeyUpEvent>(ConsumeEvent, TrickleDown.TrickleDown);
    _backdrop.RegisterCallback<WheelEvent>(ConsumeEvent, TrickleDown.TrickleDown);

    // Override in derived classes to bind button events, etc.
  }

  protected virtual void OnShow() {
    // Override in derived classes for custom show logic
  }

  protected virtual void OnHide() {
    // Override in derived classes for custom hide logic
  }

  protected T FindElement<T>(string name) where T : VisualElement {
    return _modalContainer?.Q<T>(name);
  }

  protected Button FindButton(string name) {
    return _modalContainer?.Q<Button>(name);
  }

  protected Label FindLabel(string name) {
    return _modalContainer?.Q<Label>(name);
  }
}
