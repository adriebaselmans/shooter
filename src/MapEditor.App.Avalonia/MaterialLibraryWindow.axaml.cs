using MapEditor.App.ViewModels;

namespace MapEditor.App.Avalonia;

public partial class MaterialLibraryWindow : global::Avalonia.Controls.Window
{
    public MaterialLibraryWindow()
    {
        InitializeComponent();
    }

    public MaterialLibraryWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
