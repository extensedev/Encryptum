using System;
using System.Collections.Generic;

namespace Encryptum.Services;

public static class FileTypes
{
    public static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".md", ".markdown", ".json", ".py", ".js", ".ts", ".cs", ".html", ".htm",
        ".css", ".scss", ".less", ".yaml", ".yml", ".xml", ".log", ".ini", ".cfg", ".csv",
        ".toml", ".sh", ".bat", ".ps1", ".sql", ".env", ".gitignore", ".dockerignore",
        ".editorconfig", ".tsx", ".jsx", ".vue", ".svelte", ".rb", ".go", ".rs", ".java",
        ".kt", ".swift", ".c", ".cpp", ".h", ".hpp", ".cmake", ".gradle", ".properties",
        ".conf", ".sln", ".csproj", ".razor", ".svg", ".xaml", ".resx", ".asmx",
        "", ".makefile", ".dockerfile"
    };

    public static bool IsTextFile(string extension)
    {
        return TextExtensions.Contains(extension) || string.IsNullOrEmpty(extension);
    }
}