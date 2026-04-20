using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Encryptum.ViewModels;

namespace Encryptum.ViewModels.Dialogs;

public partial class DeleteConfirmViewModel : ViewModelBase
{
    [ObservableProperty]
    private int _count;

    public string Message => $"Are you sure you want to delete {Count} item(s)?";

    public event Action? RequestClose;

    public DeleteConfirmViewModel(int count)
    {
        _count = count;
    }

    [RelayCommand]
    private void Delete()
    {
        Confirmed = true;
        RequestClose?.Invoke();
    }

    [RelayCommand]
    private void Cancel() => RequestClose?.Invoke();

    public bool Confirmed { get; private set; }
}