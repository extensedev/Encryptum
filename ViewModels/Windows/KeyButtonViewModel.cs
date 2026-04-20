using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Encryptum.ViewModels;

namespace Encryptum.ViewModels.Windows;

public partial class KeyButtonViewModel : ViewModelBase
{
    private readonly LoginViewModel _owner;

    [ObservableProperty]
    private string _key = string.Empty;

    public KeyButtonViewModel(string key, LoginViewModel owner)
    {
        _key = key;
        _owner = owner;
    }

    [RelayCommand]
    private void Press() => _owner.InputCharCommand.Execute(Key);
}