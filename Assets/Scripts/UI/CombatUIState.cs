using System;
using System.Collections.Generic;
using System.ComponentModel;
using EchoesOfTheVoid.Core;

namespace EchoesOfTheVoid.UI
{
  /// <summary>
  /// Observable combat UI state for binding with UI Toolkit at runtime.
  /// Exposes core properties and notifies via <see cref="INotifyPropertyChanged"/>.
  /// </summary>
  public sealed class CombatUIState : INotifyPropertyChanged
  {
    public event PropertyChangedEventHandler PropertyChanged;

    private string _currentTurnName = string.Empty;
    private List<ICombatAction> _availableActions = new List<ICombatAction>();
    private List<ICombatant> _validTargets = new List<ICombatant>();
    private ICombatAction _selectedAction;
    private IUICommand _attackCommand;
    private IUICommand _defendCommand;
    private IUICommand _itemCommand;
    private IUICommand _skillCommand;
    private IUICommand _cancelCommand;
    private List<ICombatAction> _availableItemActions = new List<ICombatAction>();
    private List<ICombatAction> _availableSkillActions = new List<ICombatAction>();

    /// <summary>
    /// The display name of the combatant whose turn is active.
    /// </summary>
    public string CurrentTurnName
    {
      get { return _currentTurnName; }
      set { SetProperty(ref _currentTurnName, value, nameof(CurrentTurnName)); }
    }

    /// <summary>
    /// The actions available to the current user. Replaced atomically for binding updates.
    /// </summary>
    public IReadOnlyList<ICombatAction> AvailableActions
    {
      get { return _availableActions.AsReadOnly(); }
    }

    /// <summary>
    /// The valid targets based on the selected action and current context.
    /// </summary>
    public IReadOnlyList<ICombatant> ValidTargets
    {
      get { return _validTargets.AsReadOnly(); }
    }

    /// <summary>
    /// The action currently selected by the user (can be null).
    /// </summary>
    public ICombatAction SelectedAction
    {
      get { return _selectedAction; }
      set { SetProperty(ref _selectedAction, value, nameof(SelectedAction)); }
    }

    /// <summary>
    /// Command bindings for the four primary actions.
    /// </summary>
    public IUICommand AttackCommand
    {
      get { return _attackCommand; }
      set { SetProperty(ref _attackCommand, value, nameof(AttackCommand)); }
    }

    public IUICommand DefendCommand
    {
      get { return _defendCommand; }
      set { SetProperty(ref _defendCommand, value, nameof(DefendCommand)); }
    }

    public IUICommand ItemCommand
    {
      get { return _itemCommand; }
      set { SetProperty(ref _itemCommand, value, nameof(ItemCommand)); }
    }

    public IUICommand SkillCommand
    {
      get { return _skillCommand; }
      set { SetProperty(ref _skillCommand, value, nameof(SkillCommand)); }
    }

    /// <summary>
    /// Command to cancel the current selection or close open lists.
    /// </summary>
    public IUICommand CancelCommand
    {
      get { return _cancelCommand; }
      set { SetProperty(ref _cancelCommand, value, nameof(CancelCommand)); }
    }

    /// <summary>
    /// The filtered list of item actions usable in the current state.
    /// </summary>
    public IReadOnlyList<ICombatAction> AvailableItemActions
    {
      get { return _availableItemActions.AsReadOnly(); }
    }

    /// <summary>
    /// The filtered list of skill actions usable in the current state.
    /// </summary>
    public IReadOnlyList<ICombatAction> AvailableSkillActions
    {
      get { return _availableSkillActions.AsReadOnly(); }
    }

    /// <summary>
    /// Replaces the list of available actions and notifies bindings.
    /// </summary>
    public void SetAvailableActions(IEnumerable<ICombatAction> actions)
    {
      _availableActions = actions != null ? new List<ICombatAction>(actions) : new List<ICombatAction>();
      OnPropertyChanged(nameof(AvailableActions));
    }

    /// <summary>
    /// Replaces the list of valid targets and notifies bindings.
    /// </summary>
    public void SetValidTargets(IEnumerable<ICombatant> targets)
    {
      _validTargets = targets != null ? new List<ICombatant>(targets) : new List<ICombatant>();
      OnPropertyChanged(nameof(ValidTargets));
    }

    /// <summary>
    /// Clears the current selection and lists.
    /// </summary>
    public void Clear()
    {
      _availableActions.Clear();
      _validTargets.Clear();
      _selectedAction = null;
      _attackCommand = null;
      _defendCommand = null;
      _itemCommand = null;
      _skillCommand = null;
      _cancelCommand = null;
      _availableItemActions.Clear();
      _availableSkillActions.Clear();
      OnPropertyChanged(nameof(AvailableActions));
      OnPropertyChanged(nameof(ValidTargets));
      OnPropertyChanged(nameof(SelectedAction));
      OnPropertyChanged(nameof(AttackCommand));
      OnPropertyChanged(nameof(DefendCommand));
      OnPropertyChanged(nameof(ItemCommand));
      OnPropertyChanged(nameof(SkillCommand));
      OnPropertyChanged(nameof(CancelCommand));
      OnPropertyChanged(nameof(AvailableItemActions));
      OnPropertyChanged(nameof(AvailableSkillActions));
    }

    public void SetAvailableItemActions(IEnumerable<ICombatAction> actions)
    {
      _availableItemActions = actions != null ? new List<ICombatAction>(actions) : new List<ICombatAction>();
      OnPropertyChanged(nameof(AvailableItemActions));
    }

    public void SetAvailableSkillActions(IEnumerable<ICombatAction> actions)
    {
      _availableSkillActions = actions != null ? new List<ICombatAction>(actions) : new List<ICombatAction>();
      OnPropertyChanged(nameof(AvailableSkillActions));
    }

    private void SetProperty<T>(ref T field, T value, string propertyName = null)
    {
      if (EqualityComparer<T>.Default.Equals(field, value))
      {
        return;
      }
      field = value;
      OnPropertyChanged(propertyName);
    }

    private void OnPropertyChanged(string propertyName)
    {
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
  }
}
