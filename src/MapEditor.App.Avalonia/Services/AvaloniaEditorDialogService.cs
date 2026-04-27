using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Layout;
using Avalonia.Media;
using MapEditor.App.Services;

namespace MapEditor.App.Avalonia.Services;

public sealed class AvaloniaEditorDialogService : IEditorFileDialogService, IEditorMessageService
{
    private Window? _owner;

    public void Attach(Window owner)
    {
        _owner = owner;
    }

    public async Task<string?> ShowOpenMapDialogAsync()
    {
        if (_owner is null)
        {
            return null;
        }

        var files = await _owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = false,
            Title = "Open Map",
            FileTypeFilter =
            [
                new FilePickerFileType("MapEditor Map") { Patterns = ["*.shmap"] }
            ]
        });

        return files.Count == 0 ? null : files[0].TryGetLocalPath();
    }

    public async Task<string?> ShowSaveMapDialogAsync(string suggestedFileName)
    {
        if (_owner is null)
        {
            return null;
        }

        var file = await _owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Map",
            SuggestedFileName = suggestedFileName,
            DefaultExtension = "shmap",
            FileTypeChoices =
            [
                new FilePickerFileType("MapEditor Map") { Patterns = ["*.shmap"] }
            ]
        });

        return file?.TryGetLocalPath();
    }

    public async Task<string?> ShowOpenFolderDialogAsync(string title)
    {
        if (_owner is null)
        {
            return null;
        }

        var folders = await _owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            AllowMultiple = false,
            Title = title
        });

        return folders.Count == 0 ? null : folders[0].TryGetLocalPath();
    }

    public Task ShowErrorAsync(string title, string message) =>
        ShowDialogAsync(title, message, ["OK"]).ContinueWith(_ => { });

    public async Task<bool> ConfirmDiscardAsync(string title, string message) =>
        string.Equals(await ShowDialogAsync(title, message, ["Discard", "Cancel"]), "Discard", StringComparison.Ordinal);

    public async Task<EditorUnsavedChangesResult> ConfirmUnsavedChangesAsync(string title, string message)
    {
        return await ShowDialogAsync(title, message, ["Save", "Discard", "Cancel"]) switch
        {
            "Save" => EditorUnsavedChangesResult.Save,
            "Discard" => EditorUnsavedChangesResult.Discard,
            _ => EditorUnsavedChangesResult.Cancel
        };
    }

    private async Task<string> ShowDialogAsync(string title, string message, IReadOnlyList<string> buttons)
    {
        if (_owner is null)
        {
            return buttons[^1];
        }

        var completion = new TaskCompletionSource<string>();
        var buttonRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        var container = new StackPanel
        {
            Margin = new global::Avalonia.Thickness(16),
            Spacing = 12,
            Children =
            {
                new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, Width = 420 },
                buttonRow
            }
        };

        var dialog = new Window
        {
            Title = title,
            Width = 480,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = container
        };

        foreach (var label in buttons)
        {
            var button = new Button { Content = label, MinWidth = 88 };
            button.Click += (_, _) =>
            {
                completion.TrySetResult(label);
                dialog.Close();
            };
            buttonRow.Children.Add(button);
        }

        await dialog.ShowDialog(_owner);
        return await completion.Task;
    }
}