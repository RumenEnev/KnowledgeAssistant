using KnowledgeAssistant.Wpf.Messages.ModelContextWindows;
using KnowledgeAssistant.Wpf.Models;
using MessageServices;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;

namespace KnowledgeAssistant.Wpf.Windows
{
    public partial class ModelContextWindowsWindow : Window, INotifyPropertyChanged, IMessageServiceSubscriber
    {
        private readonly MessageService _messageService;
        private string? _statusMessage;

        public ModelContextWindowsWindow(MessageService messageService)
        {
            InitializeComponent();
            DataContext = this;

            _messageService = messageService;
            _messageService.Subscribe<ModelContextWindowsUpdatedEvent>(this, ModelContextWindowsUpdatedEventReceived);
        }

        public ObservableCollection<ModelContextWindowDisplayModel> Models { get; } = new ObservableCollection<ModelContextWindowDisplayModel>();

        public string? StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(nameof(StatusMessage)); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void ModelContextWindowsWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _messageService.Publish(new GetModelContextWindowsRequest());
        }

        private void ModelContextWindowsUpdatedEventReceived(MessageBase message)
        {
            if (message is ModelContextWindowsUpdatedEvent @event)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Models.Clear();
                    foreach (var model in @event.Models)
                    {
                        Models.Add(new ModelContextWindowDisplayModel
                        {
                            Id = model.Id,
                            Name = model.Name,
                            Size = model.Size,
                            ContextLength = model.ContextLength,
                            Family = model.Family,
                            QuantizationLevel = model.QuantizationLevel,
                            ParameterSize = model.ParameterSize
                        });
                    }

                    StatusMessage = $"{Models.Count} model(s) loaded.";
                });
            }
        }
    }
}
