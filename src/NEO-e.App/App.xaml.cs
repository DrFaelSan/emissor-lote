namespace NEO_e.App;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NEO_e.App.ViewModels;
using NEO_e.Application.Contracts;
using NEO_e.Application.UseCases;
using NEO_e.Infrastructure.DependencyInjection;
using System;
using System.IO;
using System.Windows;

public partial class App : System.Windows.Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ShutdownMode = ShutdownMode.OnMainWindowClose;

        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        try
        {
            _host = Host.CreateDefaultBuilder(e.Args)
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
                    services.AddInfrastructure(context.Configuration);
                    services.AddAppSettingsValidation();

                    services.AddSingleton<DiscoverCertificatesUseCase>();
                    services.AddSingleton<LoadCertificateUseCase>();
                    services.AddSingleton<SincronizarEmpresaUseCase>();
                    services.AddSingleton<SincronizarCarteiraUseCase>();

                    services.AddSingleton(sp => new Lazy<SincronizarCarteiraUseCase>(() => sp.GetRequiredService<SincronizarCarteiraUseCase>()));
                    services.AddSingleton(sp => new Lazy<ResetNsuUseCase>(() => sp.GetRequiredService<ResetNsuUseCase>()));

                    services.AddSingleton<MainWindowViewModel>();
                    services.AddSingleton<IProgressReporter>(sp => sp.GetRequiredService<MainWindowViewModel>());
                    services.AddSingleton<MainWindow>();
                })
                .Build();

            var logger = _host.Services.GetRequiredService<NEO_e.Application.Contracts.ILogger>();
            logger.LogInformation("Sistema NEO-e iniciando... Ambiente base: {BaseDirectory}", AppContext.BaseDirectory);
            logger.LogInformation("Arquivo de configuracao carregado em {ConfigPath}", Path.Combine(AppContext.BaseDirectory, "appsettings.json"));

            await _host.StartAsync();

            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            MainWindow = mainWindow;
            mainWindow.Show();

            logger.LogInformation("MainWindow exibida com sucesso.");
        }
        catch (Exception ex)
        {
            ShowStartupError(ex);
            Shutdown();
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        try
        {
            if (_host is not null)
            {
                var logger = _host.Services.GetService<NEO_e.Application.Contracts.ILogger>();
                logger?.LogInformation("Sistema NEO-e encerrando...");
                await _host.StopAsync();
                _host.Dispose();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Falha ao encerrar o host: {ex}");
        }
        finally
        {
            AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
            DispatcherUnhandledException -= OnDispatcherUnhandledException;
            TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
            base.OnExit(e);
        }
    }

    private static void ShowStartupError(Exception ex)
    {
        var details = ex.InnerException is null
            ? ex.Message
            : $"{ex.Message}\n\nInnerException: {ex.InnerException.Message}";

        MessageBox.Show(
            $"Erro na inicialização do aplicativo.\n\n{details}\n\nStackTrace:\n{ex.StackTrace}",
            "NEO-e - Erro de inicialização",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            ShowStartupError(ex);
        }
    }

    private static void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        ShowStartupError(e.Exception);
        e.Handled = true;
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        ShowStartupError(e.Exception);
        e.SetObserved();
    }
}