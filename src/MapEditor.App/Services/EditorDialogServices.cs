namespace MapEditor.App.Services;

public enum EditorUnsavedChangesResult
{
    Save,
    Discard,
    Cancel
}

public interface IEditorFileDialogService
{
    Task<string?> ShowOpenMapDialogAsync();
    Task<string?> ShowSaveMapDialogAsync(string suggestedFileName);
    Task<string?> ShowOpenFolderDialogAsync(string title);
}

public interface IEditorMessageService
{
    Task ShowErrorAsync(string title, string message);
    Task<bool> ConfirmDiscardAsync(string title, string message);
    Task<EditorUnsavedChangesResult> ConfirmUnsavedChangesAsync(string title, string message);
}