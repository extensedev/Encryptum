using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using System;
using System.IO;
using System.Linq;
using Encryptum.Services;
using Encryptum.ViewModels.Windows;
using Encryptum.Views.Windows;

namespace Encryptum;

public partial class App : Application
{
    private ISettingsService _settings = null!;
    private TrayIcon? _trayIcon;
    private Window? _mainWindow;
    private IClassicDesktopStyleApplicationLifetime? _desktop;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        _settings = new SettingsService();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _desktop = desktop;
            DisableAvaloniaDataAnnotationValidation();

            var crypto = new CryptoService();
            var vault = new VaultRepository(crypto);
            var messageService = new MessageService();
            var filePreview = new FilePreviewService();

            var loginVm = new LoginViewModel(crypto, vault, _settings);
            var loginWindow = new LoginWindow { DataContext = loginVm };

            loginVm.LoginSucceeded += () =>
            {
                var vaultVm = new VaultViewModel(crypto, vault, filePreview, messageService, _settings);
                vaultVm.Initialize(loginVm.Password);

                var mainWindow = new MainWindow { DataContext = vaultVm };
                _mainWindow = mainWindow;
                mainWindow.InitializeSettings(_settings);
                _settings.SettingChanged += OnSettingChanged;

                desktop.MainWindow = _mainWindow;
                _mainWindow.Show();
                loginWindow.Close();
                ApplyMinimizeToTray();
            };

            desktop.MainWindow = loginWindow;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnSettingChanged(string name)
    {
        if (name == nameof(ISettingsService.MinimizeToTray))
            ApplyMinimizeToTray();
        else if (name == nameof(ISettingsService.IsLightTheme))
            ApplyTheme();
    }

    private void ApplyTheme()
    {
        var watcher = new ShadUI.ThemeWatcher(this);
        watcher.SwitchTheme(_settings.IsLightTheme ? ShadUI.ThemeMode.Light : ShadUI.ThemeMode.Dark);
    }

    private void ApplyMinimizeToTray()
    {
        if (_mainWindow is null || _desktop is null) return;

        if (_settings.MinimizeToTray)
        {
            if (_trayIcon is null)
                SetupTrayIcon(_mainWindow, _desktop);

            _mainWindow.Closing += OnMainWindowClosing;
        }
        else
        {
            _mainWindow.Closing -= OnMainWindowClosing;

            if (_trayIcon is not null)
            {
                _trayIcon.IsVisible = false;
                _trayIcon.Dispose();
                _trayIcon = null;
            }
        }
    }

    private void OnMainWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        e.Cancel = true;
        (sender as Window)!.Hide();
    }

    private void ShowSettingsDialog(Window owner)
    {
        var settingsVm = new SettingsViewModel(_settings);
        var settingsWindow = new SettingsWindow { DataContext = settingsVm };
        settingsVm.Closed += () => settingsWindow.Close();
        settingsWindow.ShowDialog(owner);
    }

    private void SetupTrayIcon(Window mainWindow, IClassicDesktopStyleApplicationLifetime desktop)
    {
        _trayIcon = new TrayIcon
        {
            Icon = mainWindow.Icon,
            ToolTipText = "Encryptum",
            IsVisible = true
        };

        _trayIcon.Clicked += (_, _) =>
        {
            mainWindow.Show();
            mainWindow.Activate();
        };

        var showMenuItem = new NativeMenuItem("Show");
        showMenuItem.Click += (_, _) =>
        {
            mainWindow.Show();
            mainWindow.Activate();
        };

        var exitMenuItem = new NativeMenuItem("Exit");
        exitMenuItem.Click += (_, _) =>
        {
            _trayIcon.IsVisible = false;
            desktop.Shutdown();
        };

        _trayIcon.Menu = new NativeMenu
        {
            Items = { showMenuItem, exitMenuItem }
        };
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}