using System;
using Encryptum.ViewModels.Dialogs;
using ShadWindow = ShadUI.Window;

namespace Encryptum.Views.Dialogs;

public partial class UnsavedChangesDialog : ShadWindow
{
    public UnsavedChangesDialog()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (DataContext is UnsavedChangesViewModel vm)
            vm.RequestClose += OnRequestClose;
    }

    private void OnRequestClose() => Close();
}