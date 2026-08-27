using System.Windows.Input;

namespace BTDeviceBatteryInfo.ViewModels;

internal sealed class AsyncCommand(Func<Task> execute) : ICommand
{
    event EventHandler? ICommand.CanExecuteChanged { add { } remove { } }
    public bool CanExecute(object? parameter) => true;
    public async void Execute(object? parameter) => await execute();
}
