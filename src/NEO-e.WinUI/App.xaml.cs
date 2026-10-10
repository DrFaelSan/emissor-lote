using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Diagnostics;
using System.IO;
using NEO_e.Application.Contracts;
using NEO_e.Application.UseCases;
using NEO_e.Infrastructure.DependencyInjection;
using NEO_e.WinUI.Converters;
using NEO_e.WinUI.Services;
using NEO_e.WinUI.ViewModels;

namespace NEO_e.WinUI;

public partial class App : Microsoft.UI.Xaml.Application
{
    private const int MinimumSplashMilliseconds = 800;

    private IHost? _host;
    private SplashScreenWindow? _splash;
    private MainWindow? _mainWindow;

    public App()
    {
        NativeCrashDump.Install();
        InitializeComponent();
        RequestedTheme = ApplicationTheme.Light;
        UnhandledException += OnUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
            DiagLog("AppDomain.UnhandledException: " + eventArgs.ExceptionObject);
        TaskScheduler.UnobservedTaskException += (_, eventArgs) =>
            DiagLog("UnobservedTaskException: " + eventArgs.Exception);
        AppDomain.CurrentDomain.FirstChanceException += (_, eventArgs) =>
            DiagLog("FirstChance hr=0x" + eventArgs.Exception.HResult.ToString("X8") + " " + eventArgs.Exception);
    }

    private static void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        DiagLog("Application.UnhandledException: " + e.Exception);
        e.Handled = true;
    }

    internal static void DiagLog(string message)
    {
        try
        {
            var dir = Path.Combine(AppContext.BaseDirectory, "logs");
            Directory.CreateDirectory(dir);
            File.AppendAllText(
                Path.Combine(dir, "diag.log"),
                "[" + DateTimeOffset.Now.ToString("O") + "]" + Environment.NewLine + message + Environment.NewLine);
        }
        catch
        {
        }
    }

    protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        var bootTimer = Stopwatch.StartNew();
        _splash = new SplashScreenWindow();
        _splash.Activate();

        try
        {
            var dispatcherQueue = DispatcherQueue.GetForCurrentThread();
            _host = await Task.Run(() => Host.CreateDefaultBuilder()
                .UseDefaultServiceProvider(options =>
                {
                    options.ValidateOnBuild = true;
                    options.ValidateScopes = true;
                })
                .ConfigureAppConfiguration((_, configuration) =>
                {
                    configuration.SetBasePath(AppContext.BaseDirectory);
                    configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                })
                .ConfigureServices((context, services) =>
                {
                    services.AddSingleton(dispatcherQueue);
                    services.AddSingleton<PickerService>();
                    services.AddSingleton<IIslandNotifier, IslandNotificationService>();

                    services.AddInfrastructure(context.Configuration);
                    services.AddAppSettingsValidation();

                    services.AddSingleton<DiscoverCertificatesUseCase>();
                    services.AddSingleton<LoadCertificateUseCase>();
                    services.AddSingleton<SincronizarEmpresaUseCase>();
                    services.AddSingleton<SincronizarCarteiraUseCase>();
                    services.AddSingleton<SimularCarteiraUseCase>();

                    services.AddSingleton(sp => new Lazy<SincronizarCarteiraUseCase>(() => sp.GetRequiredService<SincronizarCarteiraUseCase>()));
                    services.AddSingleton(sp => new Lazy<SimularCarteiraUseCase>(() => sp.GetRequiredService<SimularCarteiraUseCase>()));
                    services.AddSingleton(sp => new Lazy<ResetNsuUseCase>(() => sp.GetRequiredService<ResetNsuUseCase>()));

                    services.AddSingleton<MainWindowViewModel>();
                    services.AddSingleton<IProgressReporter>(sp => sp.GetRequiredService<MainWindowViewModel>());
                    services.AddSingleton<MainWindow>();
                })
                .Build());

            var logger = _host.Services.GetRequiredService<NEO_e.Application.Contracts.ILogger>();
            logger.LogInformation("Sistema NEO-e iniciando. Diretorio base: {BaseDirectory}", AppContext.BaseDirectory);
            logger.LogInformation("Configuracao carregada de {ConfigPath}", Path.Combine(AppContext.BaseDirectory, "appsettings.json"));

            await _host.StartAsync();

            var remainingSplashTime = MinimumSplashMilliseconds - bootTimer.ElapsedMilliseconds;
            if (remainingSplashTime > 0)
                await Task.Delay((int)remainingSplashTime);

            _mainWindow = _host.Services.GetRequiredService<MainWindow>();
            _mainWindow.Closed += OnMainWindowClosed;
            _mainWindow.Activate();
            CloseSplash();

            logger.LogInformation("MainWindow WinUI exibida com sucesso.");
        }
        catch (Exception exception)
        {
            await ShowStartupErrorAsync(exception);
            CloseSplash();
            await StopHostAsync();
            Exit();
        }
    }

    private void OnMainWindowClosed(object sender, WindowEventArgs args)
    {
        _ = StopHostAsync();
    }

    private async Task StopHostAsync()
    {
        var host = _host;
        if (host is null)
            return;

        _host = null;
        var logger = host.Services.GetService<NEO_e.Application.Contracts.ILogger>();

        try
        {
            logger?.LogInformation("Sistema NEO-e encerrando.");
            await host.StopAsync();
        }
        catch (Exception exception)
        {
            logger?.LogError("Falha ao encerrar o host. Tipo: {ExceptionType}", exception.GetType().Name);
            DiagLog("Host shutdown failed. Type: " + exception.GetType().Name);
        }
        finally
        {
            host.Dispose();
        }
    }

    private async Task ShowStartupErrorAsync(Exception exception)
    {
        var exceptionType = exception.GetType().Name;
        DiagLog("Application startup failed. Type: " + exceptionType);

        var logger = _host?.Services.GetService<NEO_e.Application.Contracts.ILogger>();
        logger?.LogError("Falha na inicializacao do aplicativo. Tipo: {ExceptionType}", exceptionType);

        if (_splash?.Content?.XamlRoot is not { } xamlRoot)
            return;

        var dialog = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = "Falha ao iniciar o NEO-e",
            Content = exception is FileNotFoundException
                ? "Nao foi possivel carregar um arquivo necessario. Verifique a configuracao e tente novamente."
                : $"A inicializacao nao foi concluida. {exception.Message}\nTipo: {exceptionType}",
            CloseButtonText = "Fechar",
            DefaultButton = ContentDialogButton.Close
        };

        await dialog.ShowAsync();
    }

    private void CloseSplash()
    {
        _splash?.Close();
        _splash = null;
    }
}
