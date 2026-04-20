using System;
using Avalonia.Controls;
using Avalonia.Input;
using Encryptum.ViewModels.Dialogs;
using ShadWindow = ShadUI.Window;

namespace Encryptum.Views.Dialogs;

public partial class CreateItemDialog : ShadWindow
{
    public CreateItemDialog()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (DataContext is CreateItemViewModel vm)
            vm.RequestClose += OnRequestClose;
    }

    private void OnRequestClose() => Close();

    private void OnNameKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is CreateItemViewModel vm)
        {
            vm.CreateCommand.Execute(null);
            e.Handled = true;
        }
    }
}