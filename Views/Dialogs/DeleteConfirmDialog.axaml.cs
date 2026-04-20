using System;
using Avalonia.Input;
using Encryptum.ViewModels.Dialogs;
using ShadWindow = ShadUI.Window;

namespace Encryptum.Views.Dialogs;

public partial class DeleteConfirmDialog : ShadWindow
{
    public DeleteConfirmDialog()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (DataContext is DeleteConfirmViewModel vm)
            vm.RequestClose += OnRequestClose;
    }

    private void OnRequestClose() => Close();
}