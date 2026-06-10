using System;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;
using Encryptum.Models;

namespace Encryptum.Services;

public interface IFilePreviewService
{
    Task PreviewAsync(VirtualFile file);
}

public class FilePreviewService : IFilePreviewService
{
    // Decrypted previews are written here so they can be wiped as a group.
    private static readonly string TempDir = Path.Combine(Path.GetTempPath(), "Encryptum");

    static FilePreviewService()
    {
        // Remove plaintext left behind by a previous (possibly crashed) session,
        // and make a best-effort wipe when this process exits normally.
        TrySweep();
        AppDomain.CurrentDomain.ProcessExit += (_, _) => TrySweep();
    }

    public async Task PreviewAsync(VirtualFile file)
    {
        Directory.CreateDirectory(TempDir);
        var tempPath = Path.Combine(TempDir, $"{Guid.NewGuid():N}_{file.Name}");

        var ext = Path.GetExtension(file.Name);
        var isText = FileTypes.IsTextFile(ext);

        // Write file
        if (isText)
        {
            await File.WriteAllTextAsync(tempPath, file.Content);
        }
        else
        {
            var bytes = Convert.FromBase64String(file.Content);
            await File.WriteAllBytesAsync(tempPath, bytes);
        }

        // Open with FileShare.Delete so we can delete while the external app holds it
        var stream = new FileStream(tempPath, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);

        // Start the external app FIRST, then mark file for deletion
        Process.Start(new ProcessStartInfo
        {
            FileName = tempPath,
            UseShellExecute = true
        });

        // Small delay to let the external app grab its own handle, then delete.
        // The file vanishes from the directory, but data stays alive while handles are open.
        _ = CleanupAsync(stream, tempPath);
    }

    private static async Task CleanupAsync(FileStream stream, string path)
    {
        // Wait for the external app to open its own file handle
        await Task.Delay(TimeSpan.FromSeconds(2));

        // Mark file for deletion — removes directory entry, data stays until all handles close
        try { File.Delete(path); } catch { }

        // Hold our handle a bit more, then release
        await Task.Delay(TimeSpan.FromSeconds(3));
        await stream.DisposeAsync();
    }

    private static void TrySweep()
    {
        // Best-effort: files still held open by an external app may survive until it closes.
        try
        {
            if (Directory.Exists(TempDir))
                Directory.Delete(TempDir, recursive: true);
        }
        catch { /* locked/in-use files are cleaned on the next sweep */ }
    }
}
