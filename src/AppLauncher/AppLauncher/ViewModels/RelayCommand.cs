using System.Windows.Input;

namespace AppLauncher.ViewModels;

public sealed class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        this._execute = execute;
        this._canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter)
    {
        if (this._canExecute is null)
        {
            return true;
        }

        return this._canExecute();
    }

    public void Execute(object? parameter)
    {
        if (this.CanExecute(parameter))
        {
            this._execute();
        }
    }

    public void RaiseCanExecuteChanged()
    {
        this.CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
