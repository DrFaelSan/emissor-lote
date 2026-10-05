using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NEO_e.App.ViewModels;
using NEO_e.Application.Contracts;
using NEO_e.Application.UseCases;
using NEO_e.Infrastructure.DependencyInjection;
using System.Windows;

namespace NEO_e.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    private IHost? _host;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _host = Host.CreateDefaultBuilder()
            // Habilita a verificação automática de dependências no momento do Build
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

                // UseCases
                services.AddSingleton<DiscoverCertificatesUseCase>();
                services.AddSingleton<LoadCertificateUseCase>();
                services.AddSingleton<SincronizarEmpresaUseCase>();
                services.AddSingleton<SincronizarCarteiraUseCase>();

                // ViewModel e mapeamento do IProgressReporter
                services.AddSingleton<MainWindowViewModel>();
                services.AddSingleton<IProgressReporter>(sp => sp.GetRequiredService<MainWindowViewModel>());

                // Window
                services.AddSingleton<MainWindow>();
            })
            .Build();

        _host.Start();
        MainWindow = _host.Services.GetRequiredService<MainWindow>();
        MainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host?.StopAsync().GetAwaiter().GetResult();
        _host?.Dispose();
        base.OnExit(e);
    }
}