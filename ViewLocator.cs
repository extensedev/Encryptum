using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Encryptum.ViewModels;
using Encryptum.ViewModels.Windows;
using Encryptum.Views.Windows;

namespace Encryptum;

public class ViewLocator : IDataTemplate
{
    public Control? Build(object? param)
    {
        if (param is null)
            return null;

        return param switch
        {
            LoginViewModel => new LoginWindow(),
            SettingsViewModel => new SettingsWindow(),
            VaultViewModel => new MainWindow(),
            MainWindowViewModel => new MainWindow(),
            _ => new TextBlock { Text = "Not Found: " + param.GetType().FullName },
        };
    }

    public bool Match(object? data)
    {
        return data is ViewModelBase;
    }
}