using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Encryptum.ViewModels;

namespace Encryptum.ViewModels.Dialogs;

public partial class UnsavedChangesViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _fileName = string.Empty;

    public string Message => $"Save changes to '{FileName}'?";

    public event Action<bool?>? Result;

    public UnsavedChangesViewModel(string fileName)
    {
        _fileName = fileName;
    }

    [RelayCommand]
    private void Save()
    {
        Result?.Invoke(true);
        RequestClose?.Invoke();
    }

    [RelayCommand]
    private void Discard()
    {
        Result?.Invoke(false);
        RequestClose?.Invoke();
    }

    [RelayCommand]
    private void Cancel()
    {
        Result?.Invoke(null);
        RequestClose?.Invoke();
    }

    public event Action? RequestClose;
}