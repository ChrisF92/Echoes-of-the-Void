using System;

namespace EchoesOfTheVoid.UI
{
  /// <summary>
  /// A simple delegate-based implementation of <see cref="IUICommand"/>.
  /// </summary>
  public sealed class UICommand : IUICommand
  {
    public event Action CanExecuteChanged;

    private readonly Action _execute;
    private readonly Func<bool> _canExecute;

    public UICommand(Action execute, Func<bool> canExecute = null)
    {
      _execute = execute ?? throw new ArgumentNullException(nameof(execute));
      _canExecute = canExecute;
    }

    public bool CanExecute()
    {
      return _canExecute == null || _canExecute();
    }

    public void Execute()
    {
      if (!CanExecute())
      {
        return;
      }
      _execute();
    }

    public void RaiseCanExecuteChanged()
    {
      CanExecuteChanged?.Invoke();
    }
  }
}

