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
    private bool _runAtStartup;

    [ObservableProperty]
    private bool _minimizeToTray;

    public event Action? Closed;

    public SettingsViewModel(ISettingsService settings)
    {
        _settings = settings;

        RunAtStartup = _settings.RunAtStartup;
        MinimizeToTray = _settings.MinimizeToTray;
    }

    partial void OnRunAtStartupChanged(bool value) => _settings.RunAtStartup = value;

    partial void OnMinimizeToTrayChanged(bool value) => _settings.MinimizeToTray = value;

    [RelayCommand]
    private void Close() => Closed?.Invoke();
}