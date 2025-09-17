using System;

namespace EchoesOfTheVoid.UI
{
  /// <summary>
  /// Minimal command abstraction for binding UI actions.
  /// </summary>
  public interface IUICommand
  {
    event Action CanExecuteChanged;
    bool CanExecute();
    void Execute();
  }
}

