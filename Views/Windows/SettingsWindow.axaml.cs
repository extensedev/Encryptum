using System;
using Avalonia.Controls;

namespace Encryptum.Views.Windows;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
        if (!OperatingSystem.IsWindows())
            (Chrome.Parent as Panel)?.Children.Remove(Chrome);
    }
}