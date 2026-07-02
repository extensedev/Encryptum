using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Encryptum.ViewModels.Windows;

namespace Encryptum.Views.Windows;

public partial class LoginWindow : Window
{
    public LoginWindow()
    {
        InitializeComponent();
        if (!OperatingSystem.IsWindows())
            (Chrome.Parent as Panel)?.Children.Remove(Chrome);
    }

    private void OnPasswordKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is LoginViewModel vm)
        {
            vm.LoginCommand.Execute(null);
            e.Handled = true;
        }
    }

    // Keyboard panel (272) + StackPanel spacing (12).
    private const double KeyboardBlockHeight = 284;

    private void OnKeyboardToggle(object? sender, RoutedEventArgs e)
    {
        var panel = this.FindControl<Panel>("KeyboardPanel");
        if (panel is null) return;

        panel.IsVisible = !panel.IsVisible;

        SizeToContent = SizeToContent.Manual;
        Height += panel.IsVisible ? KeyboardBlockHeight : -KeyboardBlockHeight;
    }
}