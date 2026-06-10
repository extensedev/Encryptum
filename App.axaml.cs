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
        AppDomain.CurrentDomain.ProcessExit += (_, _) => ClearSecrets();

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
                SetupTray();
            };

            desktop.MainWindow = loginWindow;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnSettingChanged(string name)
    {
        if (name == nameof(ISettingsService.IsLightTheme))
            ApplyTheme();
    }

    private void ApplyTheme()
    {
        var watcher = new ShadUI.ThemeWatcher(this);
        watcher.SwitchTheme(_settings.IsLightTheme ? ShadUI.ThemeMode.Light : ShadUI.ThemeMode.Dark);
    }

    // Tray is always on: minimizing hides the window to the tray, closing (✕) exits.
    private void SetupTray()
    {
        if (_mainWindow is null || _desktop is null) return;

        if (_trayIcon is null)
            SetupTrayIcon(_mainWindow, _desktop);

        _mainWindow.PropertyChanged += OnMainWindowPropertyChanged;
    }

    private void OnMainWindowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Window.WindowStateProperty && e.NewValue is WindowState.Minimized)
            (sender as Window)!.Hide();
    }

    private void ShowSettingsDialog(Window owner)
    {
        var settingsVm = new SettingsViewModel(_settings);
        var settingsWindow = new SettingsWindow { DataContext = settingsVm };
        settingsVm.Closed += () => settingsWindow.Close();
        settingsWindow.ShowDialog(owner);
    }

    private void ClearSecrets() =>
        (_mainWindow?.DataContext as VaultViewModel)?.ClearSecret();

    private static void RestoreFromTray(Window mainWindow)
    {
        mainWindow.Show();
        mainWindow.WindowState = WindowState.Normal;
        mainWindow.Activate();
    }

    private void SetupTrayIcon(Window mainWindow, IClassicDesktopStyleApplicationLifetime desktop)
    {
        _trayIcon = new TrayIcon
        {
            Icon = mainWindow.Icon,
            ToolTipText = "Encryptum",
            IsVisible = true
        };

        _trayIcon.Clicked += (_, _) => RestoreFromTray(mainWindow);

        var showMenuItem = new NativeMenuItem("Show");
        showMenuItem.Click += (_, _) => RestoreFromTray(mainWindow);

        var exitMenuItem = new NativeMenuItem("Exit");
        exitMenuItem.Click += (_, _) =>
        {
            _trayIcon.IsVisible = false;
            ClearSecrets();
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