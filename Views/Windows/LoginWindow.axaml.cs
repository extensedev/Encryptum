using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Encryptum.ViewModels.Windows;
using ShadWindow = ShadUI.Window;

namespace Encryptum.Views.Windows;

public partial class LoginWindow : ShadWindow
{
    public LoginWindow()
    {
        InitializeComponent();
    }

    private void OnPasswordKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is LoginViewModel vm)
        {
            vm.LoginCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void OnKeyboardToggle(object? sender, RoutedEventArgs e)
    {
        var panel = this.FindControl<Panel>("KeyboardPanel");
        if (panel is not null)
            panel.IsVisible = !panel.IsVisible;
    }
}