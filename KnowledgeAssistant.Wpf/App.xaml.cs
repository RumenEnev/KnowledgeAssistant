using CommunicationsClients;
using KnowledgeAssistant.Wpf.Services;
using MessageServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using System.Windows;

namespace KnowledgeAssistant.Wpf
{
    public partial class App : System.Windows.Application
    {
        private MessageService? _messageService;

        public static IHost? AppHost { get; private set; }

        public App()
        {
            AppHost = Host.CreateDefaultBuilder()
                        .UseContentRoot(AppContext.BaseDirectory)
                        .ConfigureAppConfiguration(config =>
                        {
                            config.AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true);
                        })
                        .UseSerilog()
                        .ConfigureServices((hostContext, services) =>
                        {
                            services.AddSingleton<MainWindow>();
                            services.AddSingleton<MessageService>();
                            services.AddSingleton<CommunicationsService>();
                            services.AddSingleton<ConversationsService>();
                            services.AddSingleton<ToolsExecutionService>();
                        })
                        .Build();
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            _ = AppHost!.RunAsync();
            _messageService = AppHost!.Services.GetRequiredService<MessageService>();

            var configuration = AppHost!.Services.GetRequiredService<IConfiguration>();
            await _messageService.AttachSignalRClient(new MultiTopicSignalRClient()
            {
                Host = configuration["SignalR:Host"] ?? "localhost",
                Port = configuration.GetValue<ushort?>("SignalR:Port") ?? 5243,
                Path = configuration["SignalR:Path"] ?? "Hub",
                UseHttps = configuration.GetValue<bool?>("SignalR:UseHttps") ?? false,
                ListeningQueue = "ReceiveMessage",
                ListeningQueues = ["ReceiveMessage", "ReceiveLogData", "ReceiveWebPageChanged", "ReceiveMqttMessage", "ReceiveFileChangedChanged"]
            });

            var startupForm = AppHost!.Services.GetRequiredService<MainWindow>();
            CreateServices();

            startupForm.Show();
            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            AppHost!.StopAsync();
            base.OnExit(e);
        }

        private void CreateServices()
        {
            AppHost!.Services.GetRequiredService<CommunicationsService>();
            AppHost!.Services.GetRequiredService<ConversationsService>();
            AppHost!.Services.GetRequiredService<ToolsExecutionService>();
        }
    }
}