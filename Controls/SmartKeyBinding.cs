using System;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;

namespace Encryptum.Controls;

/// <summary>
///     A key binding with special handling for TextBox controls: when a TextBox is focused,
///     the key event is first passed to the TextBox before executing the command.
/// </summary>
public class SmartKeyBinding : KeyBinding, ICommand
{
    public static readonly StyledProperty<ICommand> SmartCommandProperty =
        AvaloniaProperty.Register<SmartKeyBinding, ICommand>(nameof(SmartCommand));

    public ICommand SmartCommand
    {
        get => GetValue(SmartCommandProperty);
        set => SetValue(SmartCommandProperty, value);
    }

    [Obsolete("Use SmartCommand instead for proper TextBox handling.", true)]
    public new ICommand Command
    {
        get => base.Command;
        set => base.Command = value;
    }

    public SmartKeyBinding()
    {
        base.Command = this;
    }

    private static IInputElement? GetFocusedElement() =>
        (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?
        .MainWindow?.FocusManager?.GetFocusedElement();

    public bool CanExecute(object? parameter) =>
        GetFocusedElement() is TextBox || SmartCommand.CanExecute(parameter);

    public void Execute(object? parameter)
    {
        if (GetFocusedElement() is TextBox textBox)
        {
            var ev = new KeyEventArgs
            {
                Key = Gesture.Key,
                KeyModifiers = Gesture.KeyModifiers,
                RoutedEvent = InputElement.KeyDownEvent
            };
            textBox.RaiseEvent(ev);
            if (!ev.Handled && CanExecute(parameter)) SmartCommand.Execute(parameter);
        }
        else
        {
            SmartCommand.Execute(parameter);
        }
    }

    public event EventHandler? CanExecuteChanged
    {
        add => SmartCommand.CanExecuteChanged += value;
        remove => SmartCommand.CanExecuteChanged -= value;
    }
}
