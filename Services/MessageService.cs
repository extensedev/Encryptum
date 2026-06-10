using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using ShadowUI;

namespace Encryptum.Services;

public interface IMessageService
{
    void Success(string title, string? content = null);
    void Error(string title, string? content = null);
    void Warning(string title, string? content = null);
    void Info(string title, string? content = null);
}

public class MessageService : IMessageService
{
    private static Avalonia.Visual? Anchor =>
        (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

    private static void Show(string title, string? content, ToastType type)
    {
        if (Anchor is { } anchor)
            Toast.Show(anchor, title, content, type);
    }

    public void Success(string title, string? content = null) => Show(title, content, ToastType.Success);
    public void Error(string title, string? content = null) => Show(title, content, ToastType.Error);
    public void Warning(string title, string? content = null) => Show(title, content, ToastType.Warning);
    public void Info(string title, string? content = null) => Show(title, content, ToastType.Info);
}
