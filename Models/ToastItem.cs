using CommunityToolkit.Mvvm.ComponentModel;

namespace ReleaseTool.Desktop.Models;

public enum ToastKind
{
    Info,
    Success,
    Warning,
    Error
}

public partial class ToastItem : ObservableObject
{
    public required string Message { get; init; }
    public ToastKind Kind { get; init; } = ToastKind.Info;
    public Guid Id { get; } = Guid.NewGuid();
}
