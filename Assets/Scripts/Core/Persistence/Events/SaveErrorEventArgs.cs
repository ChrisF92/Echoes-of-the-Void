using System;

namespace EchoesOfTheVoid.Core.Persistence {
  /// <summary>
  /// Event arguments for save errors.
  /// </summary>
  public class SaveErrorEventArgs : EventArgs {
    public string ErrorMessage { get; }
    public Exception Exception { get; }

    public SaveErrorEventArgs(string errorMessage, Exception exception = null) {
      ErrorMessage = errorMessage;
      Exception = exception;
    }
  }
}
