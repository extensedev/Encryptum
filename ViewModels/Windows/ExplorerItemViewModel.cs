using System;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Encryptum.Models;
using Encryptum.Services;
using Encryptum.ViewModels;

namespace Encryptum.ViewModels.Windows;

public partial class ExplorerItemViewModel : ViewModelBase
{
    [ObservableProperty]
    private VirtualNode _node;

    [ObservableProperty]
    private bool _isSelected;

    public bool IsFolder => Node is VirtualFolder;

    public string Name => Node.Name;

    public Guid Id => Node.Id;

    public string InfoText
    {
        get
        {
            if (Node is VirtualFolder folder)
            {
                var size = CalculateFolderSize(folder);
                return FormatSize(size);
            }

            if (Node is VirtualFile file)
            {
                var bytes = CalculateFileSize(file);
                return FormatSize(bytes);
            }

            return "";
        }
    }

    private static long CalculateFileSize(VirtualFile file)
    {
        var ext = Path.GetExtension(file.Name);
        if (FileTypes.IsTextFile(ext))
            return System.Text.Encoding.UTF8.GetByteCount(file.Content);
        else
        {
            try { return Convert.FromBase64String(file.Content).Length; }
            catch { return System.Text.Encoding.UTF8.GetByteCount(file.Content); }
        }
    }

    private static long CalculateFolderSize(VirtualFolder folder)
    {
        var filesSize = folder.Files.Sum(f => CalculateFileSize(f));
        var subFoldersSize = folder.Folders.Sum(f => CalculateFolderSize(f));
        return filesSize + subFoldersSize;
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024.0):F1} MB";
    }

    public string FileIconKind
    {
        get
        {
            if (IsFolder) return "Folder";
            var ext = Path.GetExtension(Node.Name).ToLowerInvariant();
            return ext switch
            {
                ".md" or ".markdown" => "FileText",
                ".json" => "Braces",
                ".py" => "FileCode",
                ".js" or ".ts" => "FileCode",
                ".cs" => "FileCode",
                ".html" or ".htm" => "FileCode",
                ".css" => "FileCode",
                ".yaml" or ".yml" => "FileText",
                ".xml" => "FileCode",
                ".sh" or ".bat" or ".ps1" => "Terminal",
                ".sql" => "Database",
                ".env" => "FileKey",
                ".toml" or ".cfg" or ".ini" => "Settings",
                ".csv" => "FileSpreadsheet",
                ".log" => "FileText",
                ".svg" => "Image",
                _ => "FileText"
            };
        }
    }

    public ExplorerItemViewModel(VirtualNode node)
    {
        _node = node;
    }
}