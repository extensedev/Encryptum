using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Encryptum.Models;
using Encryptum.Services;
using Encryptum.ViewModels;

namespace Encryptum.ViewModels.Windows;

public partial class VaultViewModel : ViewModelBase
{
    private readonly IVaultRepository _vault;
    private readonly ICryptoService _crypto;
    private readonly IFilePreviewService _filePreview;
    private readonly SemaphoreSlim _saveLock = new(1, 1);
    private string _password = string.Empty;

    public IMessageService MessageService { get; }


    private const long MaxFileSize = 1024 * 1024;
    private const long MaxVaultSize = 1024L * 1024 * 1024; // 1 GB total vault limit

    // Clipboard
    private enum ClipboardMode { None, Copy, Cut }
    private List<ExplorerItemViewModel> _clipboardItems = [];
    private ClipboardMode _clipboardMode = ClipboardMode.None;

    [ObservableProperty]
    private VaultData _vaultData = new();

    [ObservableProperty]
    private VirtualFolder _currentFolder = new();

    [ObservableProperty]
    private ObservableCollection<ExplorerItemViewModel> _currentItems = [];

    [ObservableProperty]
    private ExplorerItemViewModel? _selectedExplorerItem;

    [ObservableProperty]
    private ObservableCollection<ExplorerItemViewModel> _selectedItems = [];

    [ObservableProperty]
    private bool _isFileOpen;

    [ObservableProperty]
    private string _fileContent = string.Empty;

    [ObservableProperty]
    private bool _isModified;

    partial void OnFileContentChanged(string value)
    {
        if (IsFileOpen)
            IsModified = true;
    }

    [ObservableProperty]
    private string _fileTitle = string.Empty;

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private string _breadcrumbText = "Root";

    [ObservableProperty]
    private bool _canGoBack;

    [ObservableProperty]
    private bool _isListView;

    partial void OnIsListViewChanged(bool value)
    {
        if (_settings != null)
            _settings.IsListView = value;
    }

    [ObservableProperty]
    private string? _renamingItemId;

    public bool ShowBackButton => IsFileOpen || CanGoBack;

    partial void OnIsFileOpenChanged(bool value) => OnPropertyChanged(nameof(ShowBackButton));
    partial void OnCanGoBackChanged(bool value) => OnPropertyChanged(nameof(ShowBackButton));

    private readonly ISettingsService? _settings;

    private readonly Stack<VirtualFolder> _navigationStack = [];

    private VirtualFile? _openFile;

    public bool HasSelection => SelectedItems.Count > 0;
    public bool CanPaste => _clipboardItems.Count > 0;
    public bool CanDeleteSelected => SelectedItems.Count > 0;

    partial void OnSelectedItemsChanged(ObservableCollection<ExplorerItemViewModel> value)
    {
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(CanDeleteSelected));
    }

    [RelayCommand]
    private void ToggleView() => IsListView = !IsListView;

    public event Action? SettingsRequested;
    public event Action<ExplorerItemViewModel>? RenameRequested;
    public event Action? ExportRequested;
    public event Func<string, Task<bool>>? ConfirmUnsavedChanges;

    [RelayCommand]
    private void OpenSettings() => SettingsRequested?.Invoke();

    [RelayCommand]
    private void Export() => ExportRequested?.Invoke();

    public VaultViewModel(ICryptoService crypto, IVaultRepository vault, IFilePreviewService filePreview, IMessageService messageService, ISettingsService? settings = null)
    {
        _crypto = crypto;
        _vault = vault;
        _filePreview = filePreview;
        MessageService = messageService;
        _settings = settings;
        if (_settings != null)
            IsListView = _settings.IsListView;
    }

    public void Initialize(string password)
    {
        try
        {
            _password = password;
            VaultData = _vault.Load(password);
            CurrentFolder = VaultData.Root;
            RefreshCurrentItems();
            StatusText = "Ready";
            IsModified = false;
        }
        catch (Exception ex) when (ex is System.Security.Cryptography.CryptographicException or System.FormatException)
        {
            StatusText = "Decryption failed: wrong password?";
            VaultData = new VaultData();
            CurrentFolder = VaultData.Root;
            RefreshCurrentItems();
        }
    }

    public async Task InitializeAsync(string password)
    {
        try
        {
            _password = password;
            VaultData = await Task.Run(() => _vault.Load(password));
            CurrentFolder = VaultData.Root;
            RefreshCurrentItems();
            StatusText = "Ready";
            IsModified = false;
        }
        catch (Exception ex) when (ex is System.Security.Cryptography.CryptographicException or System.FormatException)
        {
            StatusText = "Decryption failed: wrong password?";
            VaultData = new VaultData();
            CurrentFolder = VaultData.Root;
            RefreshCurrentItems();
        }
    }

    // Selection
    public void SelectItem(ExplorerItemViewModel item, bool ctrlPressed, bool shiftPressed)
    {
        if (item == null) return;

        if (shiftPressed && SelectedExplorerItem != null)
        {
            var startIndex = CurrentItems.IndexOf(SelectedExplorerItem);
            var endIndex = CurrentItems.IndexOf(item);
            if (startIndex < 0) startIndex = 0;
            if (endIndex < 0) endIndex = 0;

            var lo = Math.Min(startIndex, endIndex);
            var hi = Math.Max(startIndex, endIndex);

            SelectedItems.Clear();
            for (int i = lo; i <= hi; i++)
                SelectedItems.Add(CurrentItems[i]);
        }
        else if (ctrlPressed)
        {
            if (item.IsSelected)
            {
                item.IsSelected = false;
                SelectedItems.Remove(item);
            }
            else
            {
                item.IsSelected = true;
                SelectedItems.Add(item);
            }
            SelectedExplorerItem = item;
        }
        else
        {
            foreach (var si in SelectedItems)
                si.IsSelected = false;
            SelectedItems.Clear();
            item.IsSelected = true;
            SelectedItems.Add(item);
            SelectedExplorerItem = item;
        }

        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(CanDeleteSelected));
    }

    public void ClearSelection()
    {
        foreach (var si in SelectedItems)
            si.IsSelected = false;
        SelectedItems.Clear();
        SelectedExplorerItem = null;
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(CanDeleteSelected));
    }

    // Clipboard
    [RelayCommand]
    private void CopySelected()
    {
        if (SelectedItems.Count == 0) return;
        _clipboardItems = SelectedItems.ToList();
        _clipboardMode = ClipboardMode.Copy;
        OnPropertyChanged(nameof(CanPaste));
        StatusText = $"Copied {SelectedItems.Count} item(s)";
    }

    [RelayCommand]
    private void CutSelected()
    {
        if (SelectedItems.Count == 0) return;
        _clipboardItems = SelectedItems.ToList();
        _clipboardMode = ClipboardMode.Cut;
        OnPropertyChanged(nameof(CanPaste));
        StatusText = $"Cut {SelectedItems.Count} item(s) — paste to move";
    }

    [RelayCommand]
    private async Task Paste()
    {
        if (_clipboardItems.Count == 0) return;

        int actionCount = 0;

        if (_clipboardMode == ClipboardMode.Copy)
        {
            foreach (var item in _clipboardItems)
            {
                var clone = DeepClone(item.Node);
                if (clone is VirtualFolder cf)
                    CurrentFolder.Folders.Add(cf);
                else if (clone is VirtualFile vf)
                    CurrentFolder.Files.Add(vf);
                actionCount++;
            }
            StatusText = $"Copied {actionCount} item(s)";
        }
        else if (_clipboardMode == ClipboardMode.Cut)
        {
            int movedCount = 0;
            int skippedCount = 0;

            // Remove from source first, then add to current folder
            foreach (var item in _clipboardItems.ToList())
            {
                var node = item.Node;
                var parent = FindParent(VaultData.Root, node);

                // If source is the same folder, skip (already there, just remove from clipboard)
                if (parent == CurrentFolder)
                {
                    // Item is already in this folder — just keep it, no duplicate
                    skippedCount++;
                    continue;
                }

                // Remove from source parent
                if (parent != null)
                {
                    parent.Folders.RemoveAll(f => f.Id == node.Id);
                    parent.Files.RemoveAll(f => f.Id == node.Id);
                }

                // Add to current folder
                if (node is VirtualFolder sf)
                    CurrentFolder.Folders.Add(sf);
                else if (node is VirtualFile sf2)
                    CurrentFolder.Files.Add(sf2);
                movedCount++;
            }

            _clipboardItems.Clear();
            _clipboardMode = ClipboardMode.None;
            OnPropertyChanged(nameof(CanPaste));

            StatusText = movedCount > 0
                ? $"Moved {movedCount} item(s)"
                : "Already in this folder";
        }

        ClearSelection();
        RefreshCurrentItems();
        await SaveAsync();
    }

    private static VirtualNode DeepClone(VirtualNode node)
    {
        return node switch
        {
            VirtualFile f => new VirtualFile
            {
                Name = f.Name,
                Content = f.Content,
                CreatedAt = f.CreatedAt,
                UpdatedAt = f.UpdatedAt
            },
            VirtualFolder f => new VirtualFolder
            {
                Name = f.Name,
                Folders = new List<VirtualFolder>(f.Folders.Select(fl => (VirtualFolder)DeepClone(fl))),
                Files = new List<VirtualFile>(f.Files.Select(fl => (VirtualFile)DeepClone(fl)))
            },
            _ => node
        };
    }

    // Rename
    [RelayCommand]
    private void RenameSelectedItem()
    {
        var item = SelectedExplorerItem;
        if (item == null && SelectedItems.Count > 0)
            item = SelectedItems[0];
        if (item != null)
            RenameRequested?.Invoke(item);
    }

    public async Task ApplyRenameAsync(ExplorerItemViewModel item, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName)) return;
        item.Node.Name = newName;
        RefreshCurrentItems();
        await SaveAsync();
        StatusText = $"Renamed to: {newName}";
    }

    public void ApplyRename(ExplorerItemViewModel item, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName)) return;
        item.Node.Name = newName;
        RefreshCurrentItems();
        Save();
        StatusText = $"Renamed to: {newName}";
    }

    public void RefreshCurrentItems()
    {
        CurrentItems.Clear();
        foreach (var f in CurrentFolder.Folders)
            CurrentItems.Add(new ExplorerItemViewModel(f));
        foreach (var f in CurrentFolder.Files)
            CurrentItems.Add(new ExplorerItemViewModel(f));
        UpdateBreadcrumb();
        CanGoBack = _navigationStack.Count > 0;
    }

    private void UpdateBreadcrumb()
    {
        var parts = new List<string>();
        var current = CurrentFolder;
        while (current != null)
        {
            if (current == VaultData.Root)
            {
                parts.Add("Root");
                break;
            }
            parts.Add(current.Name);
            var parent = FindParent(VaultData.Root, current);
            current = parent;
        }
        parts.Reverse();
        BreadcrumbText = string.Join(" / ", parts);
    }

    [RelayCommand]
    private async Task OpenItem(ExplorerItemViewModel item)
    {
        if (item.IsFolder)
            NavigateInto((VirtualFolder)item.Node);
        else
            await OpenFileAsync((VirtualFile)item.Node);
    }

    public void NavigateInto(VirtualFolder folder)
    {
        _navigationStack.Push(CurrentFolder);
        CurrentFolder = folder;
        ClearSelection();
        RefreshCurrentItems();
    }

    [RelayCommand]
    private async Task GoBack()
    {
        if (IsFileOpen)
        {
            if (IsModified)
            {
                var proceed = await (ConfirmUnsavedChanges?.Invoke(_openFile?.Name ?? "file") ?? Task.FromResult(true));
                if (!proceed) return;
            }
            CloseFile();
            return;
        }
        if (_navigationStack.Count > 0)
        {
            CurrentFolder = _navigationStack.Pop();
            ClearSelection();
            RefreshCurrentItems();
        }
    }

    private async Task OpenFileAsync(VirtualFile file)
    {
        // Don't open binary files in the text editor — open externally instead
        var ext = Path.GetExtension(file.Name);
        var isText = FileTypes.IsTextFile(ext);
        if (!isText)
        {
            await OpenExternally(file);
            return;
        }

        if (IsFileOpen && IsModified)
        {
            var proceed = await (ConfirmUnsavedChanges?.Invoke(_openFile?.Name ?? "file") ?? Task.FromResult(true));
            if (!proceed) return;
        }

        _openFile = file;
        FileContent = file.Content;
        IsModified = false; // Reset after programmatic content change
        FileTitle = file.Name;
        IsFileOpen = true;
    }

    [RelayCommand]
    private void CloseFile()
    {
        IsFileOpen = false;
        _openFile = null;
        FileTitle = string.Empty;
        IsModified = false;
    }

    public async Task AddFolderAsync(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        var folder = new VirtualFolder { Name = name };
        CurrentFolder.Folders.Add(folder);
        RefreshCurrentItems();
        await SaveAsync();
        StatusText = $"Created folder: {name}";
        MessageService.Success("Folder created", name);
    }

    public void AddFolder(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        var folder = new VirtualFolder { Name = name };
        CurrentFolder.Folders.Add(folder);
        RefreshCurrentItems();
        Save();
        StatusText = $"Created folder: {name}";
        MessageService.Success("Folder created", name);
    }

    public async Task AddFileAsync(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        var file = new VirtualFile
        {
            Name = name,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        CurrentFolder.Files.Add(file);
        RefreshCurrentItems();
        await SaveAsync();
        StatusText = $"Created file: {name}";
        MessageService.Success("File created", name);
    }

    public void AddFile(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        var file = new VirtualFile
        {
            Name = name,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        CurrentFolder.Files.Add(file);
        RefreshCurrentItems();
        Save();
        StatusText = $"Created file: {name}";
        MessageService.Success("File created", name);
    }

    public async Task ImportFolderAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) { StatusText = "No path provided"; return; }
        if (!Directory.Exists(path)) { StatusText = $"Directory not found: {path}"; return; }

        if (CalculateVaultSize() >= MaxVaultSize)
        {
            StatusText = "Vault is full — maximum size reached (1 GB)";
            MessageService.Warning("Vault is full", "Maximum vault size reached (1 GB). Delete some files to free up space.");
            return;
        }

        var dir = new DirectoryInfo(path);
        StatusText = $"Importing {dir.Name}...";
        var (folder, fileCount) = await Task.Run(() => ImportDirectory(dir, CalculateVaultSize()));

        if (fileCount == 0 && folder.Folders.Count == 0)
        {
            StatusText = $"No files found in: {dir.Name}";
            MessageService.Warning("No files found", dir.Name);
            return;
        }

        CurrentFolder.Folders.Add(folder);
        RefreshCurrentItems();
        await SaveAsync();
        StatusText = $"Imported: {dir.Name} ({fileCount} files)";
        MessageService.Success("Folder imported", $"{dir.Name} ({fileCount} files)");
    }

    public void ImportFolder(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) { StatusText = "No path provided"; return; }
        if (!Directory.Exists(path)) { StatusText = $"Directory not found: {path}"; return; }

        if (CalculateVaultSize() >= MaxVaultSize)
        {
            StatusText = "Vault is full — maximum size reached (1 GB)";
            MessageService.Warning("Vault is full", "Maximum vault size reached (1 GB). Delete some files to free up space.");
            return;
        }

        var dir = new DirectoryInfo(path);
        var (folder, fileCount) = ImportDirectory(dir, CalculateVaultSize());

        if (fileCount == 0 && folder.Folders.Count == 0)
        {
            StatusText = $"No text files found in: {dir.Name}";
            MessageService.Warning("No text files found", dir.Name);
            return;
        }

        CurrentFolder.Folders.Add(folder);
        RefreshCurrentItems();
        Save();
        StatusText = $"Imported: {dir.Name} ({fileCount} files)";
        MessageService.Success("Folder imported", $"{dir.Name} ({fileCount} files)");
    }

    public async Task ImportFileAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) { StatusText = "No path provided"; return; }
        if (!File.Exists(path)) { StatusText = $"File not found: {path}"; return; }

        if (CalculateVaultSize() >= MaxVaultSize)
        {
            StatusText = "Vault is full — maximum size reached (1 GB)";
            MessageService.Warning("Vault is full", "Maximum vault size reached (1 GB). Delete some files to free up space.");
            return;
        }

        var fi = new FileInfo(path);
        if (fi.Length > MaxFileSize)
        {
            StatusText = $"File too large: {fi.Name}";
            MessageService.Warning("File too large", fi.Name);
            return;
        }

        var ext = fi.Extension;
        var isText = FileTypes.IsTextFile(ext);

        try
        {
            string content;
            if (isText)
            {
                content = await File.ReadAllTextAsync(fi.FullName);
            }
            else
            {
                var bytes = await File.ReadAllBytesAsync(fi.FullName);
                content = Convert.ToBase64String(bytes);
            }
            CurrentFolder.Files.Add(new VirtualFile
            {
                Name = fi.Name,
                Content = content,
                CreatedAt = fi.CreationTimeUtc,
                UpdatedAt = fi.LastWriteTimeUtc
            });
            RefreshCurrentItems();
            await SaveAsync();
            StatusText = $"Imported: {fi.Name}";
            MessageService.Success("File imported", fi.Name);
        }
        catch (UnauthorizedAccessException)
        {
            StatusText = $"Access denied: {fi.Name}";
            MessageService.Error("Access denied", fi.Name);
        }
        catch (IOException)
        {
            StatusText = $"IO error: {fi.Name}";
            MessageService.Error("IO error", fi.Name);
        }
    }

    public void ImportFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) { StatusText = "No path provided"; return; }
        if (!File.Exists(path)) { StatusText = $"File not found: {path}"; return; }

        if (CalculateVaultSize() >= MaxVaultSize)
        {
            StatusText = "Vault is full — maximum size reached (1 GB)";
            MessageService.Warning("Vault is full", "Maximum vault size reached (1 GB). Delete some files to free up space.");
            return;
        }

        var fi = new FileInfo(path);
        if (fi.Length > MaxFileSize)
        {
            StatusText = $"File too large: {fi.Name}";
            MessageService.Warning("File too large", fi.Name);
            return;
        }

        var ext = fi.Extension;
        var isText = FileTypes.IsTextFile(ext);

        try
        {
            string content;
            if (isText)
            {
                content = File.ReadAllText(fi.FullName);
            }
            else
            {
                var bytes = File.ReadAllBytes(fi.FullName);
                content = Convert.ToBase64String(bytes);
            }
            CurrentFolder.Files.Add(new VirtualFile
            {
                Name = fi.Name,
                Content = content,
                CreatedAt = fi.CreationTimeUtc,
                UpdatedAt = fi.LastWriteTimeUtc
            });
            RefreshCurrentItems();
            Save();
            StatusText = $"Imported: {fi.Name}";
            MessageService.Success("File imported", fi.Name);
        }
        catch (UnauthorizedAccessException)
        {
            StatusText = $"Access denied: {fi.Name}";
            MessageService.Error("Access denied", fi.Name);
        }
        catch (IOException)
        {
            StatusText = $"IO error: {fi.Name}";
            MessageService.Error("IO error", fi.Name);
        }
    }

    private (VirtualFolder folder, int fileCount) ImportDirectory(DirectoryInfo dir, long existingVaultSize)
    {
        var folder = new VirtualFolder { Name = dir.Name };
        var totalFiles = 0;
        long cumulativeSize = existingVaultSize;

        try
        {
            foreach (var sub in dir.EnumerateDirectories())
            {
                try { var (subFolder, subCount) = ImportDirectory(sub, cumulativeSize); folder.Folders.Add(subFolder); totalFiles += subCount; }
                catch (UnauthorizedAccessException) { }
                catch (IOException) { }
            }

            foreach (var file in dir.EnumerateFiles())
            {
                if (file.Length > MaxFileSize) continue;
                if (cumulativeSize >= MaxVaultSize) break;
                var ext = file.Extension;
                var isText = FileTypes.IsTextFile(ext);

                try
                {
                    string content;
                    if (isText)
                    {
                        content = File.ReadAllText(file.FullName);
                    }
                    else
                    {
                        var bytes = File.ReadAllBytes(file.FullName);
                        content = Convert.ToBase64String(bytes);
                    }
                    if (cumulativeSize + content.Length >= MaxVaultSize) continue;
                    folder.Files.Add(new VirtualFile { Name = file.Name, Content = content, CreatedAt = file.CreationTimeUtc, UpdatedAt = file.LastWriteTimeUtc });
                    cumulativeSize += content.Length;
                    totalFiles++;
                }
                catch (UnauthorizedAccessException) { }
                catch (IOException) { }
            }
        }
        catch (UnauthorizedAccessException) { }
        catch (IOException) { }

        return (folder, totalFiles);
    }

    public async Task MoveItemAsync(ExplorerItemViewModel source, ExplorerItemViewModel targetFolder)
    {
        if (!targetFolder.IsFolder) return;
        if (source.Node == targetFolder.Node) return;
        var target = (VirtualFolder)targetFolder.Node;

        if (source.Node is VirtualFolder sf)
        {
            if (IsDescendant(sf, target)) return;
            CurrentFolder.Folders.Remove(sf);
            target.Folders.Add(sf);
        }
        else if (source.Node is VirtualFile sf2)
        {
            CurrentFolder.Files.Remove(sf2);
            target.Files.Add(sf2);
        }

        RefreshCurrentItems();
        await SaveAsync();
        StatusText = $"Moved {source.Name} to {target.Name}";
    }

    public void MoveItem(ExplorerItemViewModel source, ExplorerItemViewModel targetFolder)
    {
        if (!targetFolder.IsFolder) return;
        if (source.Node == targetFolder.Node) return;
        var target = (VirtualFolder)targetFolder.Node;

        if (source.Node is VirtualFolder sf)
        {
            if (IsDescendant(sf, target)) return;
            CurrentFolder.Folders.Remove(sf);
            target.Folders.Add(sf);
        }
        else if (source.Node is VirtualFile sf2)
        {
            CurrentFolder.Files.Remove(sf2);
            target.Files.Add(sf2);
        }

        RefreshCurrentItems();
        Save();
        StatusText = $"Moved {source.Name} to {target.Name}";
    }

    private static bool IsDescendant(VirtualFolder folder, VirtualNode target)
    {
        if (folder == target) return true;
        foreach (var sub in folder.Folders) { if (IsDescendant(sub, target)) return true; }
        return false;
    }

    [RelayCommand]
    public async Task DeleteSelectedAsync()
    {
        var items = SelectedItems.ToList();
        if (items.Count == 0)
        {
            if (SelectedExplorerItem != null)
                items.Add(SelectedExplorerItem);
            else return;
        }

        foreach (var item in items)
        {
            CurrentFolder.Folders.RemoveAll(f => f.Id == item.Node.Id);
            CurrentFolder.Files.RemoveAll(f => f.Id == item.Node.Id);
            if (IsFileOpen && _openFile?.Id == item.Node.Id)
            {
                IsFileOpen = false;
                _openFile = null;
            }
        }

        ClearSelection();
        RefreshCurrentItems();
        await SaveAsync();
        StatusText = $"Deleted {items.Count} item(s)";
        MessageService.Success("Deleted", $"{items.Count} item(s)");
    }

    public void DeleteSelected()
    {
        var items = SelectedItems.ToList();
        if (items.Count == 0)
        {
            if (SelectedExplorerItem != null)
                items.Add(SelectedExplorerItem);
            else return;
        }

        foreach (var item in items)
        {
            CurrentFolder.Folders.RemoveAll(f => f.Id == item.Node.Id);
            CurrentFolder.Files.RemoveAll(f => f.Id == item.Node.Id);
            if (IsFileOpen && _openFile?.Id == item.Node.Id)
            {
                IsFileOpen = false;
                _openFile = null;
            }
        }

        ClearSelection();
        RefreshCurrentItems();
        Save();
        StatusText = $"Deleted {items.Count} item(s)";
        MessageService.Success("Deleted", $"{items.Count} item(s)");
    }

    [RelayCommand]
    private async Task SaveFile()
    {
        if (_openFile == null) return;
        _openFile.Content = FileContent;
        _openFile.UpdatedAt = DateTime.UtcNow;
        await SaveAsync();
        IsModified = false;
        StatusText = "Saved";
        MessageService.Success("File saved", _openFile.Name);
    }

    [RelayCommand]
    private async Task PreviewFile()
    {
        if (_openFile == null) return;
        await _filePreview.PreviewAsync(_openFile);
    }

    public async Task OpenExternally(VirtualFile file)
    {
        await _filePreview.PreviewAsync(file);
    }

    private static VirtualFolder? FindParent(VirtualFolder root, VirtualNode target)
    {
        if (root.Folders.Any(f => f.Id == target.Id) || root.Files.Any(f => f.Id == target.Id))
            return root;
        foreach (var sub in root.Folders) { var found = FindParent(sub, target); if (found != null) return found; }
        return null;
    }

    private long CalculateVaultSize()
    {
        long size = 0;
        CalculateSize(VaultData.Root, ref size);
        return size;
    }

    private static void CalculateSize(VirtualFolder folder, ref long size)
    {
        foreach (var file in folder.Files)
            size += file.Content?.Length ?? 0;
        foreach (var sub in folder.Folders)
            CalculateSize(sub, ref size);
    }

    private void Save()
    {
        _saveLock.Wait();
        try
        {
            _vault.Save(VaultData, _password);
        }
        finally
        {
            _saveLock.Release();
        }
    }

    private async Task SaveAsync()
    {
        await _saveLock.WaitAsync();
        try
        {
            await Task.Run(() => _vault.Save(VaultData, _password));
        }
        finally
        {
            _saveLock.Release();
        }
    }
}