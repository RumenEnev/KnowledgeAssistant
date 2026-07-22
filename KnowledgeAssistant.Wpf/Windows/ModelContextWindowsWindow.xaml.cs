using KnowledgeAssistant.Wpf.Messages.ModelContextWindows;
using KnowledgeAssistant.Wpf.Models;
using MessageServices;
using MessageServices.Messages;
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
            _messageService.Subscribe<ModelContextWindowUpdatedEvent>(this, ModelContextWindowUpdatedEventReceived);
            _messageService.Subscribe<UserMessage>(this, UserMessageReceived);
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
                            ContextWindowTokens = model.ContextWindowTokens
                        });
                    }

                    StatusMessage = $"{Models.Count} model(s) loaded.";
                });
            }
        }

        private void ModelContextWindowUpdatedEventReceived(MessageBase message)
        {
            if (message is ModelContextWindowUpdatedEvent @event)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var model = Models.FirstOrDefault(m => m.Id == @event.ModelId);
                    if (model != null)
                    {
                        model.IsSaving = false;
                    }

                    StatusMessage = $"Context window saved for '{@event.ModelName}'.";
                });
            }
        }

        private void UserMessageReceived(MessageBase message)
        {
            if (message is UserMessage { Title: "Save Model Context Window Failed" })
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    foreach (var model in Models)
                    {
                        model.IsSaving = false;
                    }
                });
            }
        }

        private void SaveModelContextWindow_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement { Tag: ModelContextWindowDisplayModel model })
            {
                return;
            }

            if (model.ContextWindowTokens.HasValue && model.ContextWindowTokens.Value <= 0)
            {
                MessageBox.Show("Context window tokens must be greater than zero.", "Model Context Windows", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            model.IsSaving = true;
            _messageService.Publish(new UpdateModelContextWindowRequest(model.Id, model.Name, model.ContextWindowTokens));
        }
    }
}
