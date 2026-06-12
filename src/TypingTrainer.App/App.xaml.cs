using Microsoft.UI.Xaml;
using TypingTrainer.App.Services;

namespace TypingTrainer.App;

public partial class App : Application
{
    private Window? _window;

    public static AppServices Services { get; private set; } = null!;

    public static Window? MainWindowInstance { get; private set; }

    public App()
    {
        StartupExceptionLogger.RegisterGlobalHandlers();

        try
        {
            InitializeComponent();
        }
        catch (Exception ex)
        {
            StartupExceptionLogger.Log("App.InitializeComponent", ex);
            throw;
        }
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            UnhandledException += App_UnhandledException;

            Services = await AppServices.CreateAsync();
            _window = new MainWindow();
            MainWindowInstance = _window;
            _window.Closed += Window_Closed;
            _window.Activate();
        }
        catch (Exception ex)
        {
            StartupExceptionLogger.Log("App.OnLaunched", ex);
            throw;
        }
    }

    private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        StartupExceptionLogger.Log("Application.UnhandledException", e.Exception);
    }

    private async void Window_Closed(object sender, WindowEventArgs args)
    {
        try
        {
            await Services.SessionPersistenceQueue.FlushAsync();
        }
        catch
        {
            // Persistence errors are surfaced through LastError and should not crash shutdown.
        }
    }
}
