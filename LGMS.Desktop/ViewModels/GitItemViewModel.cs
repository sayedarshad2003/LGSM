using LGMS.Desktop.Common;
using LGMS.Models;

namespace LGMS.Desktop.ViewModels;

public class GitChangeItemViewModel : ObservableObject
{
    private bool _isSelected = true;

    public GitChange Model { get; }

    public GitChangeItemViewModel(GitChange change, bool isSelected = true)
    {
        Model = change;
        _isSelected = isSelected;
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public string Path => Model.Path;
    public string? OriginalPath => Model.OriginalPath;
    public bool IsStaged => Model.IsStaged;
    public string StatusCode => Model.StatusCode;
    public ChangeStatus Status => Model.Status;

    public string DisplayName => string.IsNullOrEmpty(OriginalPath) ? Path : $"{OriginalPath} → {Path}";
}

public class DiffLineItem
{
    public int? OldLineNumber { get; set; }
    public int? NewLineNumber { get; set; }
    public string Text { get; set; } = string.Empty;
    public string LineType { get; set; } = "context"; // add, delete, header, context
}
