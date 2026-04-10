namespace MapEditor.App;

internal static class TextureWorkflowHelp
{
    public const string Title = "Texture Workflow Shortcuts";

    public static string Message =>
        """
        Texture workflow

        1. Select a brush.
        2. Pick an active texture in the bottom Textures panel.
        3. Apply it to the whole brush or select faces and edit their mapping.

        Shortcuts

        T
            Toggle the texture browser panel.

        Shift+Left Click (Perspective)
            Select a face under the cursor.

        Ctrl+Shift+Left Click (Perspective)
            Add or remove a face from the current face selection.

        Escape
            Clear face selection and return to normal select mode.

        Ctrl+S / Ctrl+Z / Ctrl+Y
            Save, undo, redo.
        """;
}
