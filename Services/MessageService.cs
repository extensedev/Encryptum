using System;
using ShadUI;

namespace Encryptum.Services;

public interface IMessageService
{
    ToastManager ToastManager { get; }
    void Success(string title, string? content = null);
    void Error(string title, string? content = null);
    void Warning(string title, string? content = null);
    void Info(string title, string? content = null);
}

public class MessageService : IMessageService
{
    public ToastManager ToastManager { get; } = new();

    public void Success(string title, string? content = null)
    {
        var builder = ToastManager.CreateToast(title);
        if (content != null) builder = builder.WithContent(content);
        builder.ShowSuccess();
    }

    public void Error(string title, string? content = null)
    {
        var builder = ToastManager.CreateToast(title);
        if (content != null) builder = builder.WithContent(content);
        builder.ShowError();
    }

    public void Warning(string title, string? content = null)
    {
        var builder = ToastManager.CreateToast(title);
        if (content != null) builder = builder.WithContent(content);
        builder.ShowWarning();
    }

    public void Info(string title, string? content = null)
    {
        var builder = ToastManager.CreateToast(title);
        if (content != null) builder = builder.WithContent(content);
        builder.ShowInfo();
    }
}