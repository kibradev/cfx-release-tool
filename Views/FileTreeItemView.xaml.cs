using System.Windows;
using System.Windows.Controls;
using ReleaseTool.Desktop.Models;
using ReleaseTool.Desktop.ViewModels;

namespace ReleaseTool.Desktop.Views;

public partial class FileTreeItemView : UserControl
{
    public static readonly DependencyProperty NodeProperty = DependencyProperty.Register(
        nameof(Node), typeof(FileTreeNode), typeof(FileTreeItemView),
        new PropertyMetadata(null, OnNodeChanged));

    public FileTreeNode? Node
    {
        get => (FileTreeNode?)GetValue(NodeProperty);
        set => SetValue(NodeProperty, value);
    }

    public FileTreeItemView()
    {
        InitializeComponent();
    }

    private static void OnNodeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is FileTreeItemView view)
            view.DataContext = e.NewValue;
    }

    private void ExpandButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is FileTreeNode node && node.IsDirectory)
            node.IsExpanded = !node.IsExpanded;

        e.Handled = true;
    }

    private void CheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox checkBox || Node == null)
            return;

        if (DataContext is not FileTreeNode node)
            return;

        var vm = Window.GetWindow(this)?.DataContext as MainViewModel;
        vm?.OnFileCheckChanged(node, checkBox.IsChecked);
        e.Handled = true;
    }
}
