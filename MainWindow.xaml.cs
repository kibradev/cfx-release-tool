using System.Windows;
using System.Windows.Input;
using ReleaseTool.Desktop.Services;
using ReleaseTool.Desktop.ViewModels;

namespace ReleaseTool.Desktop;

public partial class MainWindow : Window
{
    private readonly ConfigService _config = new();

    public MainWindow()
    {
        InitializeComponent();
        AllowDrop = true;
        DragOver += MainWindow_DragOver;
        Drop += MainWindow_Drop;
        KeyDown += MainWindow_KeyDown;
        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        var cfg = _config.Load();

        if (cfg.WindowWidth >= MinWidth && cfg.WindowHeight >= MinHeight)
        {
            Width = cfg.WindowWidth;
            Height = cfg.WindowHeight;
        }

        if (cfg.WindowLeft is { } left && cfg.WindowTop is { } top &&
            IsOnScreen(left, top))
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = left;
            Top = top;
        }

        if (cfg.LeftPanelWidth >= LeftColumn.MinWidth)
            LeftColumn.Width = new GridLength(cfg.LeftPanelWidth);
    }

    private static bool IsOnScreen(double left, double top)
    {
        var virtualLeft = SystemParameters.VirtualScreenLeft;
        var virtualTop = SystemParameters.VirtualScreenTop;
        var virtualRight = virtualLeft + SystemParameters.VirtualScreenWidth;
        var virtualBottom = virtualTop + SystemParameters.VirtualScreenHeight;
        return left >= virtualLeft && top >= virtualTop &&
               left < virtualRight - 100 && top < virtualBottom - 100;
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        try
        {
            var cfg = _config.Load();
            if (WindowState == WindowState.Normal)
            {
                cfg.WindowWidth = Width;
                cfg.WindowHeight = Height;
                cfg.WindowLeft = Left;
                cfg.WindowTop = Top;
            }
            cfg.LeftPanelWidth = LeftColumn.ActualWidth > 0 ? LeftColumn.ActualWidth : cfg.LeftPanelWidth;
            _config.Save(cfg);
        }
        catch
        {
            // ayar kaydı başarısız — kapanışı engelleme
        }
    }

    private void MainWindow_KeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not MainViewModel vm)
            return;

        if (e.Key == Key.F5)
        {
            if (vm.RefreshResourcesCommand.CanExecute(null))
                vm.RefreshResourcesCommand.Execute(null);
            e.Handled = true;
            return;
        }

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
                    FileFilterBox.Focus();
                    FileFilterBox.SelectAll();
                    e.Handled = true;
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
            vm.SetDroppedPath(path);
    }
}
