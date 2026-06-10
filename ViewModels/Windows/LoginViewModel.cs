using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Encryptum.Services;
using Encryptum.ViewModels;

namespace Encryptum.ViewModels.Windows;

public partial class LoginViewModel : ViewModelBase
{
    private readonly ICryptoService _crypto;
    private readonly IVaultRepository _vault;
    private readonly ISettingsService _settings;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _isErrorVisible;

    [ObservableProperty]
    private bool _isNewVault;

    public string UnlockButtonText => IsNewVault ? "Create" : "Unlock";

    partial void OnIsNewVaultChanged(bool value) => OnPropertyChanged(nameof(UnlockButtonText));

    [ObservableProperty]
    private bool _isPasswordVisible;

    public ObservableCollection<KeyButtonViewModel> KeyRow1 { get; } = [];
    public ObservableCollection<KeyButtonViewModel> KeyRow2 { get; } = [];
    public ObservableCollection<KeyButtonViewModel> KeyRow3 { get; } = [];
    public ObservableCollection<KeyButtonViewModel> KeyRow4 { get; } = [];
    public ObservableCollection<KeyButtonViewModel> KeyRow5 { get; } = [];

    public event Action? LoginSucceeded;
    public event Action? SettingsRequested;

    public LoginViewModel(ICryptoService crypto, IVaultRepository vault, ISettingsService settings)
    {
        _crypto = crypto;
        _vault = vault;
        _settings = settings;
        IsNewVault = !File.Exists(Path.Combine(AppContext.BaseDirectory, "vault.dat"));
        InitKeyRows();
    }

    [RelayCommand]
    private void TogglePassword() => IsPasswordVisible = !IsPasswordVisible;

    [RelayCommand]
    private void OpenSettings() => SettingsRequested?.Invoke();

    [RelayCommand]
    private void InputChar(string ch)
    {
        Password += ch;
    }

    [RelayCommand]
    private void Backspace()
    {
        if (Password.Length > 0)
            Password = Password[..^1];
    }

    partial void OnPasswordChanged(string value)
    {
        IsErrorVisible = false;
    }

    [ObservableProperty]
    private bool _isBusy;

    [RelayCommand]
    private async Task Login()
    {
        if (string.IsNullOrWhiteSpace(Password))
        {
            ShowError("Enter a password");
            return;
        }

        var pw = System.Text.Encoding.UTF8.GetBytes(Password);
        try
        {
            IsBusy = true;
            await Task.Run(() => _vault.TryLoad(pw));
            LoginSucceeded?.Invoke();
        }
        catch (Exception)
        {
            ShowError("Wrong password");
        }
        finally
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(pw);
            IsBusy = false;
        }
    }

    private void InitKeyRows()
    {
        AddKeys(KeyRow1, "!@#$%^&*()-_=");
        AddKeys(KeyRow2, "1234567890");
        AddKeys(KeyRow3, "QWERTYUIOP");
        AddKeys(KeyRow4, "ASDFGHJKL");
        AddKeys(KeyRow5, "ZXCVBNM");
    }

    private void AddKeys(ObservableCollection<KeyButtonViewModel> row, string chars)
    {
        foreach (var c in chars)
            row.Add(new KeyButtonViewModel(c.ToString(), this));
    }

    public void ShowError(string message)
    {
        ErrorMessage = message;
        IsErrorVisible = true;
    }
}