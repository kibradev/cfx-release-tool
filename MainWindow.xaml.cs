using System.Windows;
using System.Windows.Input;
using ReleaseTool.Desktop.ViewModels;

namespace ReleaseTool.Desktop;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        AllowDrop = true;
        DragOver += MainWindow_DragOver;
        Drop += MainWindow_Drop;
        KeyDown += MainWindow_KeyDown;
    }

    private void MainWindow_KeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not MainViewModel vm)
            return;

        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            switch (e.Key)
            {
                case Key.R:
                    if (vm.CreateReleaseCommand.CanExecute(null))
                        vm.CreateReleaseCommand.Execute(null);
                    e.Handled = true;
                    break;
                case Key.B:
                    if (vm.BumpPatchCommand.CanExecute(null))
                        vm.BumpPatchCommand.Execute(null);
                    e.Handled = true;
                    break;
                case Key.F:
                    // focus search - handled by default tab
                    break;
                case Key.S:
                    vm.OpenAppSettingsCommand.Execute(null);
                    e.Handled = true;
                    break;
            }
        }
    }

    private void MainWindow_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void MainWindow_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            return;

        if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths || paths.Length == 0)
            return;

        var path = paths[0];
        if (!Directory.Exists(path))
            return;

        if (DataContext is MainViewModel vm)
            vm.SetResourcesFolderFromDrop(path);
    }
}
