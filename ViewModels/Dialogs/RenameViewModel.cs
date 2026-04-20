using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Encryptum.ViewModels;

namespace Encryptum.ViewModels.Dialogs;

public partial class RenameViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _newName = string.Empty;

    [ObservableProperty]
    private bool _isNameErrorVisible;

    public string OriginalName { get; }

    public event Action<string>? RenameConfirmed;
    public event Action? RequestClose;

    public RenameViewModel(string currentName)
    {
        OriginalName = currentName;
        _newName = currentName;
    }

    [RelayCommand]
    private void Confirm()
    {
        if (string.IsNullOrWhiteSpace(NewName))
        {
            IsNameErrorVisible = true;
            return;
        }

        RenameConfirmed?.Invoke(NewName.Trim());
        RequestClose?.Invoke();
    }

    [RelayCommand]
    private void Cancel() => RequestClose?.Invoke();

    partial void OnNewNameChanged(string value) => IsNameErrorVisible = false;
}