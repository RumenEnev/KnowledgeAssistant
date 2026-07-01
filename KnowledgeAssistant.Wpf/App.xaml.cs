using CommunicationsClients;
using KnowledgeAssistant.Wpf.Services;
using MessageServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using System.Windows;

namespace KnowledgeAssistant.Wpf
{
    public partial class App : Application
    {
        private MessageService? _messageService;

        public static IHost? AppHost { get; private set; }

        public App()
        {
            AppHost = Host.CreateDefaultBuilder()
                        .UseSerilog()
                        .ConfigureServices((hostContext, services) =>
                        {
                            services.AddSingleton<MainWindow>();
                            services.AddSingleton<MessageService>();
                            services.AddSingleton<CommunicationsService>();
                            services.AddSingleton<ConversationsService>();
                        })
                        .Build();
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            _ = AppHost!.RunAsync();
            _messageService = AppHost!.Services.GetRequiredService<MessageService>();
            await _messageService.AttachSignalRClient(new MultiTopicSignalRClient()
            {
                Host = "192.168.0.200",
                Port = 5243,
                Path = "Hub",
                UseHttps = false,
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
        }
    }
}