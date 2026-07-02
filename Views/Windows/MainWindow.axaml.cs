using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Lucide.Avalonia;
using Encryptum.Models;
using Encryptum.Services;
using Encryptum.ViewModels.Windows;
using Encryptum.ViewModels.Dialogs;
using ShadowUI;

namespace Encryptum.Views.Windows;

public partial class MainWindow : Window
{
    private ISettingsService _settings = null!;
    private string? _pendingName;

    // Drag
    private ExplorerItemViewModel? _dragSource;
    private Border? _dropTargetBorder;
    private Point _dragStartPoint;
    private bool _isDragging;

    // Double-click detection
    private ExplorerItemViewModel? _lastClickedItem;
    private DateTime _lastClickTime;
    private const int DoubleClickThresholdMs = 400;

    public MainWindow()
    {
        InitializeComponent();
        if (!OperatingSystem.IsWindows())
            (Chrome.Parent as Panel)?.Children.Remove(Chrome);
    }

    public void InitializeSettings(ISettingsService settings)
    {
        _settings = settings;
        _settings.SettingChanged += OnSettingChanged;
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        if (DataContext is VaultViewModel vm)
        {
            vm.SettingsRequested += OnSettingsRequested;
            vm.RenameRequested += OnRenameRequested;
            vm.ExportRequested += OnExportClick;
            vm.ConfirmUnsavedChanges += OnConfirmUnsavedChanges;
            vm.PropertyChanged += (s, args) =>
            {
                if (args.PropertyName == nameof(VaultViewModel.IsListView))
                    OnIsListViewChanged(vm.IsListView);
            };
            vm.SelectedItems.CollectionChanged += (_, _) => UpdateSelectionVisuals();
            OnIsListViewChanged(vm.IsListView);
            vm.PropertyChanged += (s, args) =>
            {
                if (args.PropertyName == nameof(VaultViewModel.IsFileOpen) && vm.IsFileOpen)
                {
                    var editor = this.FindControl<TextBox>("FileEditor");
                    if (editor != null)
                        Dispatcher.UIThread.Post(() => editor.Focus());
                }
            };
        }

        var newFolderBtn = this.FindControl<Button>("NewFolderBtn");
        var newFileBtn = this.FindControl<Button>("NewFileBtn");
        var importFolderBtn = this.FindControl<Button>("ImportFolderBtn");
        var exportBtn = this.FindControl<Button>("ExportBtn");
        var deleteBtn = this.FindControl<Button>("DeleteBtn");

        var themeToggleBtn = this.FindControl<Button>("ThemeToggleBtn");
        var themeToggleIcon = this.FindControl<LucideIcon>("ThemeToggleIcon");

        if (newFolderBtn != null) newFolderBtn.Click += OnNewFolderClick;
        if (newFileBtn != null) newFileBtn.Click += OnNewFileClick;
        if (importFolderBtn != null) importFolderBtn.Click += OnImportFolderClick;
        if (exportBtn != null) exportBtn.Click += OnExportButtonClick;
        if (deleteBtn != null) deleteBtn.Click += OnDeleteClick;
        if (themeToggleBtn != null) themeToggleBtn.Click += OnThemeToggleClick;

        if (themeToggleIcon != null)
            themeToggleIcon.Kind = _settings.IsLightTheme ? LucideIconKind.Moon : LucideIconKind.Sun;

        // Restore saved window size
        Width = _settings.WindowWidth;
        Height = _settings.WindowHeight;

        // Save window size on close
        Closed += OnWindowClosed;

        // Setup drag-and-drop on explorer area parent (NOT on ScrollViewers — causes infinite loop)
        var explorerArea = this.FindControl<Grid>("ExplorerArea");
        if (explorerArea != null)
        {
            DragDrop.SetAllowDrop(explorerArea, true);
            DragDrop.AddDragOverHandler(explorerArea, OnDragOver);
            DragDrop.AddDropHandler(explorerArea, OnDrop);
        }
    }

    private void OnIsListViewChanged(bool isListView)
    {
        var icon = this.FindControl<LucideIcon>("ToggleViewIcon");
        if (icon != null)
            icon.Kind = isListView ? LucideIconKind.List : LucideIconKind.LayoutGrid;
    }

    private void OnThemeToggleClick(object? sender, RoutedEventArgs e)
    {
        _settings.IsLightTheme = !_settings.IsLightTheme;
        ApplyTheme();
        UpdateThemeIcon();
    }

    private void ApplyTheme() =>
        Application.Current!.RequestedThemeVariant = _settings.IsLightTheme
            ? Avalonia.Styling.ThemeVariant.Light
            : Avalonia.Styling.ThemeVariant.Dark;

    private void UpdateThemeIcon()
    {
        var icon = this.FindControl<LucideIcon>("ThemeToggleIcon");
        if (icon != null)
            icon.Kind = _settings.IsLightTheme ? LucideIconKind.Moon : LucideIconKind.Sun;
    }

    private void OnSettingChanged(string name)
    {
        if (name == nameof(ISettingsService.IsLightTheme))
        {
            var icon = this.FindControl<LucideIcon>("ThemeToggleIcon");
            if (icon != null)
                icon.Kind = _settings.IsLightTheme ? LucideIconKind.Moon : LucideIconKind.Sun;
        }
    }

    private void UpdateSelectionVisuals()
    {
        if (DataContext is not VaultViewModel vm) return;

        // Use the ACTUALLY VISIBLE control, not just any matching name.
        // FindControl finds hidden controls too, which breaks selection in list view.
        var itemsControl = vm.IsListView
            ? this.FindControl<ItemsControl>("ExplorerList")
            : this.FindControl<ItemsControl>("ExplorerGrid");
        if (itemsControl == null) return;

        // Reset all items first
        foreach (var descendant in itemsControl.GetVisualDescendants())
        {
            if (descendant is Border border && border.Name == "ItemBorder")
            {
                border.Classes.Remove("explorer-item-selected");
            }
        }

        // Mark selected items
        foreach (var item in vm.SelectedItems)
        {
            var container = itemsControl.ContainerFromItem(item);
            if (container != null)
            {
                foreach (var desc in container.GetVisualDescendants())
                {
                    if (desc is Border border && border.Name == "ItemBorder")
                    {
                        border.Classes.Add("explorer-item-selected");
                        break;
                    }
                }
            }
        }
    }

    private async void OnSettingsRequested()
    {
        var settingsVm = new SettingsViewModel(_settings);
        var settingsWindow = new SettingsWindow { DataContext = settingsVm };
        settingsVm.Closed += () => settingsWindow.Close();
        await settingsWindow.ShowDialog(this);
    }

    // Opens a ShadowUI overlay dialog and completes when it closes
    // (footer buttons, X button, click outside, or Esc).
    private static Task ShowOverlayDialogAsync(Dialog dialog, object dialogVm)
    {
        var tcs = new TaskCompletionSource();
        dialog.DataContext = dialogVm;

        void OnDialogPropertyChanged(object? s, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == Dialog.IsOpenProperty && e.NewValue is false)
            {
                dialog.PropertyChanged -= OnDialogPropertyChanged;
                dialog.DataContext = null;
                tcs.TrySetResult();
            }
        }

        dialog.PropertyChanged += OnDialogPropertyChanged;
        dialog.Open();
        return tcs.Task;
    }

    private async void OnRenameRequested(ExplorerItemViewModel item)
    {
        var dialogVm = new RenameViewModel(item.Name);

        dialogVm.RenameConfirmed += newName =>
        {
            if (DataContext is VaultViewModel vm)
                vm.ApplyRename(item, newName);
        };
        dialogVm.RequestClose += () => RenameDialog.Close();

        await ShowOverlayDialogAsync(RenameDialog, dialogVm);
    }

    private void OnCreateNameKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && CreateDialog.DataContext is CreateItemViewModel vm)
        {
            vm.CreateCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void OnRenameNameKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && RenameDialog.DataContext is RenameViewModel vm)
        {
            vm.ConfirmCommand.Execute(null);
            e.Handled = true;
        }
    }

    private async Task<bool> OnConfirmUnsavedChanges(string fileName)
    {
        if (DataContext is not VaultViewModel vm) return true;

        var dialogVm = new UnsavedChangesViewModel(fileName);

        bool? result = null;
        dialogVm.Result += r => result = r;
        dialogVm.RequestClose += () => UnsavedDialog.Close();

        await ShowOverlayDialogAsync(UnsavedDialog, dialogVm);

        if (result == true)
        {
            // User chose Save
            await vm.SaveFileCommand.ExecuteAsync(null);
            return true; // Proceed after saving
        }
        else if (result == false)
        {
            // User chose Discard
            return true; // Proceed without saving
        }
        else
        {
            // User chose Cancel
            return false; // Don't proceed
        }
    }

    // === Item pointer events ===

    private void OnItemTapped(object? sender, RoutedEventArgs e)
    {
        // Not used — selection is handled in PointerPressed for modifier support
    }

    private void OnItemPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border border || border.DataContext is not ExplorerItemViewModel item) return;

        var properties = e.GetCurrentPoint(this).Properties;

        if (DataContext is VaultViewModel vm)
        {
            // For right-click: select if not selected, but don't change existing multi-selection
            if (!properties.IsLeftButtonPressed && properties.IsRightButtonPressed)
            {
                if (!vm.SelectedItems.Contains(item))
                {
                    vm.SelectItem(item, false, false);
                    UpdateSelectionVisuals();
                }
                return; // Don't start drag on right-click
            }

            // Left button
            if (properties.IsLeftButtonPressed)
            {
                var modifiers = e.KeyModifiers;
                var ctrl = modifiers.HasFlag(KeyModifiers.Control);
                var shift = modifiers.HasFlag(KeyModifiers.Shift);

                // Double-click detection
                var now = DateTime.UtcNow;
                if (_lastClickedItem == item && (now - _lastClickTime).TotalMilliseconds < DoubleClickThresholdMs)
                {
                    vm.OpenItemCommand.Execute(item);
                    _lastClickedItem = null;
                    return;
                }

                _lastClickedItem = item;
                _lastClickTime = now;

                vm.SelectItem(item, ctrl, shift);
                UpdateSelectionVisuals();

                // Drag tracking (left button only)
                _dragSource = item;
                _dragStartPoint = e.GetPosition(this);
                _isDragging = false;
            }
        }
    }

    private void OnItemDoubleTapped(object? sender, RoutedEventArgs e)
    {
        // Double-click is handled in OnItemPointerPressed via manual detection.
        // This handler exists only to suppress Avalonia's default double-tap behavior.
        e.Handled = true;
    }

    private void OnItemPointerEntered(object? sender, PointerEventArgs e)
    {
        if (sender is Border border)
            border.Classes.Add("explorer-item-hover");
    }

    private void OnItemPointerExited(object? sender, PointerEventArgs e)
    {
        if (sender is Border border)
            border.Classes.Remove("explorer-item-hover");
    }

    private void OnItemPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_isDragging && _dragSource != null && _dropTargetBorder?.DataContext is ExplorerItemViewModel target && target.IsFolder)
        {
            if (DataContext is VaultViewModel vm)
            {
                vm.MoveItem(_dragSource, target);
                e.Handled = true;
            }
        }

        CancelDrag();
    }

    // === Explorer background events ===

    private void OnExplorerPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not VaultViewModel vm) return;

        // If clicked on an item, don't clear selection
        if (e.Source is Visual v && FindItemBorder(v) != null) return;

        // Cancel any pending drag from a previous item click
        CancelDrag();

        // Click on empty space — deselect
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            vm.ClearSelection();
            UpdateSelectionVisuals();
        }
    }

    private void OnExplorerPointerMoved(object? sender, PointerEventArgs e)
    {
        // Drag
        if (_dragSource == null) return;

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            CancelDrag();
            return;
        }

        var current = e.GetPosition(this);
        if (!_isDragging)
        {
            if (Math.Abs(current.X - _dragStartPoint.X) < 5 && Math.Abs(current.Y - _dragStartPoint.Y) < 5)
                return;
            _isDragging = true;
        }

        // Use the VISIBLE control (same fix as UpdateSelectionVisuals)
        if (DataContext is not VaultViewModel vm) return;
        var explorerGrid = vm.IsListView
            ? this.FindControl<ItemsControl>("ExplorerList")
            : this.FindControl<ItemsControl>("ExplorerGrid");
        if (explorerGrid == null) return;

        var hitBorder = FindItemBorderAt(explorerGrid, e.GetPosition(explorerGrid));
        if (hitBorder?.DataContext is ExplorerItemViewModel item && item.IsFolder && item != _dragSource)
        {
            if (_dropTargetBorder != hitBorder)
            {
                ClearDropHighlight();
                _dropTargetBorder = hitBorder;
                hitBorder.Classes.Add("item-drop-target");
            }
        }
        else
        {
            ClearDropHighlight();
        }
    }

    private void OnExplorerPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_isDragging && _dragSource != null && _dropTargetBorder?.DataContext is ExplorerItemViewModel target && target.IsFolder)
        {
            if (DataContext is VaultViewModel vm)
                vm.MoveItem(_dragSource, target);
        }

        CancelDrag();
    }

    // === Context menu handlers ===

    private void OnContextOpenClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem mi) return;
        if (mi.Parent is not ContextMenu cm) return;
        if (cm.DataContext is not ExplorerItemViewModel item) return;
        if (DataContext is VaultViewModel vm)
            vm.OpenItemCommand.Execute(item);
    }

    private void OnContextRenameClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is VaultViewModel vm)
            vm.RenameSelectedItemCommand.Execute(null);
    }

    private void OnContextOpenExternallyClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem mi) return;
        if (mi.Parent is not ContextMenu cm) return;
        if (cm.DataContext is not ExplorerItemViewModel item) return;
        if (item.IsFolder) return;
        if (DataContext is VaultViewModel vm)
            vm.OpenExternally((VirtualFile)item.Node);
    }

    private void OnContextCopyClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is VaultViewModel vm)
            vm.CopySelectedCommand.Execute(null);
    }

    private void OnContextCutClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is VaultViewModel vm)
            vm.CutSelectedCommand.Execute(null);
    }

    private void OnContextPasteClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is VaultViewModel vm)
            vm.PasteCommand.Execute(null);
    }

    private void OnContextExportClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is VaultViewModel vm)
            vm.ExportCommand.Execute(null);
    }

    private async void OnContextDeleteClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem mi) return;
        if (mi.Parent is not ContextMenu cm) return;
        if (cm.DataContext is not ExplorerItemViewModel item) return;
        if (DataContext is VaultViewModel vm)
        {
            vm.SelectedExplorerItem = item;
            if (!vm.SelectedItems.Contains(item))
            {
                vm.SelectItem(item, false, false);
                UpdateSelectionVisuals();
            }
            await ConfirmAndDelete(vm);
        }
    }

    private async void OnDeleteClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is VaultViewModel vm)
        {
            await ConfirmAndDelete(vm);
        }
    }

    private async System.Threading.Tasks.Task ConfirmAndDelete(VaultViewModel vm)
    {
        var count = vm.SelectedItems.Count;
        if (count == 0) return;

        var dialogVm = new DeleteConfirmViewModel(count);
        dialogVm.RequestClose += () => DeleteDialog.Close();
        await ShowOverlayDialogAsync(DeleteDialog, dialogVm);

        if (dialogVm.Confirmed)
        {
            await vm.DeleteSelectedAsync();
            UpdateSelectionVisuals();
        }
    }

    // === New folder/file/import dialogs ===

    private async void OnNewFolderClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not VaultViewModel vm) return;
        _pendingName = null;
        var dialogVm = new CreateItemViewModel(CreateItemType.Folder);
        dialogVm.ItemCreated += name => { _pendingName = name; };
        dialogVm.RequestClose += () => CreateDialog.Close();
        await ShowOverlayDialogAsync(CreateDialog, dialogVm);
        if (!string.IsNullOrWhiteSpace(_pendingName))
            await vm.AddFolderAsync(_pendingName);
    }

    private async void OnNewFileClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not VaultViewModel vm) return;
        _pendingName = null;
        var dialogVm = new CreateItemViewModel(CreateItemType.File);
        dialogVm.ItemCreated += name => { _pendingName = name; };
        dialogVm.RequestClose += () => CreateDialog.Close();
        await ShowOverlayDialogAsync(CreateDialog, dialogVm);
        if (!string.IsNullOrWhiteSpace(_pendingName))
            await vm.AddFileAsync(_pendingName);
    }

    private async void OnImportFolderClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not VaultViewModel vm) return;
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider == null) { vm.StatusText = "StorageProvider unavailable"; return; }

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select folder to import",
            AllowMultiple = false
        });

        if (folders.Count > 0)
        {
            var path = folders[0].TryGetLocalPath();
            if (!string.IsNullOrWhiteSpace(path))
                await vm.ImportFolderAsync(path);
        }
    }

    // === Export ===

    private async void OnExportClick()
    {
        if (DataContext is not VaultViewModel vm) return;
        if (vm.SelectedItems.Count == 0 && vm.SelectedExplorerItem == null) return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider == null) { vm.StatusText = "StorageProvider unavailable"; return; }

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select export folder",
            AllowMultiple = false
        });

        if (folders.Count == 0) return;
        var exportPath = folders[0].TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(exportPath)) return;

        var items = vm.SelectedItems.Count > 0 ? vm.SelectedItems.ToList() :
            new List<ExplorerItemViewModel> { vm.SelectedExplorerItem! };

        try
        {
            if (items.Count == 1 && !items[0].IsFolder)
            {
                // Single file — export directly
                var file = (VirtualFile)items[0].Node;
                var filePath = Path.Combine(exportPath, file.Name);
                if (File.Exists(filePath))
                    filePath = Path.Combine(exportPath, $"{Path.GetFileNameWithoutExtension(file.Name)}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}{Path.GetExtension(file.Name)}");

                var ext = Path.GetExtension(file.Name);
                var isText = FileTypes.IsTextFile(ext);
                if (isText)
                {
                    File.WriteAllText(filePath, file.Content);
                }
                else
                {
                    var bytes = Convert.FromBase64String(file.Content);
                    File.WriteAllBytes(filePath, bytes);
                }
                vm.StatusText = $"Exported: {file.Name}";
                vm.MessageService.Success("Exported", file.Name);
            }
            else
            {
                // Multiple items — create ZIP
                var zipName = $"encryptum_export_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.zip";
                var zipPath = Path.Combine(exportPath, zipName);

                using var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create);
                foreach (var item in items)
                {
                    if (item.IsFolder)
                        AddFolderToZip(zip, (VirtualFolder)item.Node, "");
                    else
                        AddFileToZip(zip, (VirtualFile)item.Node, "");
                }

                vm.StatusText = $"Exported: {zipName}";
                vm.MessageService.Success("Exported", zipName);
            }
        }
        catch (Exception ex)
        {
            vm.StatusText = $"Export failed: {ex.Message}";
            vm.MessageService.Error("Export failed", ex.Message);
        }
    }

    private void OnExportButtonClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is VaultViewModel vm)
            vm.ExportCommand.Execute(null);
    }

    private static void AddFileToZip(ZipArchive zip, VirtualFile file, string prefix)
    {
        var entryName = string.IsNullOrEmpty(prefix) ? file.Name : $"{prefix}/{file.Name}";
        var entry = zip.CreateEntry(entryName);
        var ext = Path.GetExtension(file.Name);
        var isText = FileTypes.IsTextFile(ext);

        if (isText)
        {
            using var writer = new StreamWriter(entry.Open());
            writer.Write(file.Content);
        }
        else
        {
            using var stream = entry.Open();
            var bytes = Convert.FromBase64String(file.Content);
            stream.Write(bytes, 0, bytes.Length);
        }
    }

    private static void AddFolderToZip(ZipArchive zip, VirtualFolder folder, string prefix)
    {
        var folderPrefix = string.IsNullOrEmpty(prefix) ? folder.Name : $"{prefix}/{folder.Name}";
        foreach (var file in folder.Files)
            AddFileToZip(zip, file, folderPrefix);
        foreach (var sub in folder.Folders)
            AddFolderToZip(zip, sub, folderPrefix);
    }

    // === Helpers ===

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        _settings.WindowWidth = Width;
        _settings.WindowHeight = Height;
    }

    private Border? FindItemBorder(Visual v)
    {
        var current = v;
        while (current != null)
        {
            if (current is Border b && b.Name == "ItemBorder") return b;
            if (current is ItemsControl) break;
            var parent = current.GetVisualParent() as Visual;
            if (parent == null) break; // reached visual tree root — stop
            current = parent;
        }
        return null;
    }

    private Border? FindItemBorderAt(ItemsControl itemsControl, Point position)
    {
        var hit = itemsControl.InputHitTest(position) as Visual;
        while (hit != null)
        {
            if (hit is Border b && b.Name == "ItemBorder") return b;
            if (hit == itemsControl) break;
            hit = hit.GetVisualParent() as Visual;
        }
        return null;
    }

    private void ClearDropHighlight()
    {
        if (_dropTargetBorder != null)
        {
            _dropTargetBorder.Classes.Remove("item-drop-target");
            _dropTargetBorder = null;
        }
    }

    private void CancelDrag()
    {
        ClearDropHighlight();
        _dragSource = null;
        _isDragging = false;
    }

    // === External drag-and-drop (from Windows Explorer) ===
    // These are OLE DragDrop handlers on the parent Grid — separate from pointer events on ScrollViewers

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        try
        {
            if (e.DataTransfer.Contains(DataFormat.File))
            {
                e.DragEffects = DragDropEffects.Copy;
                e.Handled = true;
            }
            else
            {
                e.DragEffects = DragDropEffects.None;
            }
        }
        catch
        {
            e.DragEffects = DragDropEffects.None;
        }
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        try
        {
            if (DataContext is not VaultViewModel vm) return;
            if (!e.DataTransfer.Contains(DataFormat.File)) return;

            var files = e.DataTransfer.TryGetFiles();
            if (files == null) return;

            var imported = 0;
            foreach (var file in files)
            {
                var path = file.Path.LocalPath;

                if (Directory.Exists(path))
                {
                    await vm.ImportFolderAsync(path);
                    imported++;
                }
                else if (File.Exists(path))
                {
                    await vm.ImportFileAsync(path);
                    imported++;
                }
            }

            if (imported > 0)
                vm.StatusText = $"Imported {imported} item(s) via drag & drop";
        }
        catch
        {
            // Silently ignore drop errors
        }
    }
}