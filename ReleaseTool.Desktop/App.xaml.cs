using System.Windows;

namespace ReleaseTool.Desktop;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        if (e.Args.Length > 0 && e.Args[0].StartsWith('-'))
        {
            var code = Services.CliRunner.Run(e.Args);
            Shutdown(code);
            return;
        }

        base.OnStartup(e);
    }
}

