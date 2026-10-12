using Dapper;
using KnowledgeAssistant.Application.Services;
using KnowledgeAssistant.Eval;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RagEvaluation.Desktop.Services;
using RagEvaluation.Desktop.ViewModels;
using RagEvaluation.Services;
using Serilog;
using System.IO;
using System.Runtime.Versioning;
using System.Windows;

namespace RagEvaluation.Desktop;

[SupportedOSPlatform("windows7.0")]
public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var logDirectory = Path.Combine(AppContext.BaseDirectory, "logs");
        Directory.CreateDirectory(logDirectory);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Error()
            .Enrich.FromLogContext()
            .WriteTo.File(
                Path.Combine(logDirectory, "rageval-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                shared: true)
            .CreateLogger();

        AppDomain.CurrentDomain.UnhandledException += (_, args) => Log.Fatal(args.ExceptionObject as Exception, "Unhandled AppDomain exception");
        DispatcherUnhandledException += (_, args) =>
        {
            Log.Error(args.Exception, "Unhandled UI dispatcher exception");
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Log.Error(args.Exception, "Unobserved task exception");
            args.SetObserved();
        };

        SqlMapper.AddTypeHandler(new VectorTypeHandler());

        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        var services = new ServiceCollection();
        Program.ConfigureServices(services, config);

        services.AddLogging(builder => builder.AddSerilog());
        services.AddSingleton<Windows.MainWindow>();
        services.AddTransient<Pages.DocumentsPage>();
        services.AddTransient<Pages.TestSetPage>();
        services.AddTransient<Pages.RunEvalPage>();
        services.AddTransient<Pages.RunsPage>();
        services.AddTransient<Pages.MetricsPage>();
        services.AddTransient<Pages.TestSetPage>();
        services.AddSingleton<PipelineStatusService>();
        services.AddSingleton<DocumentsViewModel>();
        services.AddSingleton<ShellViewModel>();
        services.AddSingleton<TestSetViewModel>();
        services.AddSingleton<RunViewModel>();
        services.AddSingleton<RunsViewModel>();
        services.AddSingleton<MetricsViewModel>();
        services.AddSingleton<LlmCatalogService>();
        services.AddScoped<TestSetHealthService>();

        _serviceProvider = services.BuildServiceProvider();
        var mainWindow = _serviceProvider.GetRequiredService<Windows.MainWindow>();
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.CloseAndFlush();
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }
}