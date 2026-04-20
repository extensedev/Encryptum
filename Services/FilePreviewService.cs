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
    public async Task PreviewAsync(VirtualFile file)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"encryptum_{Guid.NewGuid():N}_{file.Name}");

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

}