using System.Windows;

namespace ReleaseTool.Desktop.Views;

public partial class TextEditorDialog : Window
{
    public TextEditorDialog()
    {
        InitializeComponent();
    }

    public string EditorTitle
    {
        get => Title;
        set => Title = value;
    }

    public string Body
    {
        get => EditorBox.Text;
        set => EditorBox.Text = value;
    }

    public bool IsReadOnly
    {
        get => EditorBox.IsReadOnly;
        set
        {
            EditorBox.IsReadOnly = value;
            SaveButton.Visibility = value ? Visibility.Collapsed : Visibility.Visible;
        }
    }

    public static bool? ShowEdit(string title, ref string content, Window? owner = null)
    {
        var dlg = new TextEditorDialog
        {
            Owner = owner ?? Application.Current.MainWindow,
            EditorTitle = title,
            Body = content,
            IsReadOnly = false
        };

        if (dlg.ShowDialog() != true)
            return false;

        content = dlg.Body;
        return true;
    }

    public static void ShowPreview(string title, string content, Window? owner = null)
    {
        var dlg = new TextEditorDialog
        {
            Owner = owner ?? Application.Current.MainWindow,
            EditorTitle = title,
            Body = content,
            IsReadOnly = true
        };
        dlg.ShowDialog();
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
