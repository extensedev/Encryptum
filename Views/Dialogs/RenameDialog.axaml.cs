using System;
using Avalonia.Controls;
using Avalonia.Input;
using Encryptum.ViewModels.Dialogs;
using ShadWindow = ShadUI.Window;

namespace Encryptum.Views.Dialogs;

public partial class RenameDialog : ShadWindow
{
    public RenameDialog()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (DataContext is RenameViewModel vm)
            vm.RequestClose += OnRequestClose;
    }

    private void OnRequestClose() => Close();

    private void OnNameKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is RenameViewModel vm)
        {
            vm.ConfirmCommand.Execute(null);
            e.Handled = true;
        }
    }
}