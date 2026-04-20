using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Encryptum.ViewModels;

namespace Encryptum.ViewModels.Dialogs;

public enum CreateItemType
{
    Folder,
    File
}

public partial class CreateItemViewModel : ViewModelBase
{
    public static IReadOnlyList<string> FileFormats { get; } =
    [
        ".txt", ".md", ".json", ".py", ".js", ".ts", ".cs", ".html", ".css",
        ".yaml", ".xml", ".sh", ".sql", ".env", ".toml", ".cfg", ".log", ".csv"
    ];

    [ObservableProperty]
    private string _itemName = string.Empty;

    [ObservableProperty]
    private CreateItemType _itemType;

    [ObservableProperty]
    private int _selectedFormatIndex;

    [ObservableProperty]
    private bool _isNameErrorVisible;

    public string DialogTitle => ItemType == CreateItemType.Folder ? "New Folder" : "New File";
    public string DialogIcon => ItemType == CreateItemType.Folder ? "FolderPlus" : "FilePlus";
    public bool IsFileMode => ItemType == CreateItemType.File;

    public event Action<string>? ItemCreated;
    public event Action? RequestClose;

    public CreateItemViewModel(CreateItemType type)
    {
        _itemType = type;
        _selectedFormatIndex = 0;
    }

    public string GetFullName()
    {
        var name = ItemName.Trim();
        if (ItemType == CreateItemType.File)
        {
            var ext = FileFormats[SelectedFormatIndex];
            if (!name.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                name += ext;
        }
        return name;
    }

    [RelayCommand]
    private void Create()
    {
        if (string.IsNullOrWhiteSpace(ItemName))
        {
            IsNameErrorVisible = true;
            return;
        }

        ItemCreated?.Invoke(GetFullName());
        RequestClose?.Invoke();
    }

    [RelayCommand]
    private void Cancel() => RequestClose?.Invoke();

    partial void OnItemNameChanged(string value) => IsNameErrorVisible = false;
}