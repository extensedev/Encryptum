using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Encryptum.Services;
using Encryptum.ViewModels;

namespace Encryptum.ViewModels.Windows;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly ISettingsService _settings;

    [ObservableProperty]
    private bool _minimizeToTray;

    [ObservableProperty]
    private bool _runAtStartup;

    public event Action? Closed;

    public SettingsViewModel(ISettingsService settings)
    {
        _settings = settings;

        MinimizeToTray = _settings.MinimizeToTray;
        RunAtStartup = _settings.RunAtStartup;
    }

    partial void OnMinimizeToTrayChanged(bool value) => _settings.MinimizeToTray = value;
    partial void OnRunAtStartupChanged(bool value) => _settings.RunAtStartup = value;

    [RelayCommand]
    private void Close() => Closed?.Invoke();
}