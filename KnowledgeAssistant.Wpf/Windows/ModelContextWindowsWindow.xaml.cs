using KnowledgeAssistant.Wpf.Messages.ModelContextWindows;
using KnowledgeAssistant.Wpf.Models;
using MessageServices;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

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
            _messageService.Subscribe<ModelContextWindowSaveResultEvent>(this, ModelContextWindowSaveResultEventReceived);
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
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    Models.Clear();
                    foreach (var model in @event.Models)
                    {
                        var displayModel = new ModelContextWindowDisplayModel
                        {
                            Id = model.Id,
                            Name = model.Name,
                            Size = model.Size,
                            ContextLength = model.ContextLength,
                            Family = model.Family,
                            QuantizationLevel = model.QuantizationLevel,
                            ParameterSize = model.ParameterSize,
                            InternalUseOnly = model.InternalUseOnly,
                            CanCallTools = model.CanCallTools
                        };

                        // Setting the properties above marks the model dirty; reset it since this is the initial load.
                        displayModel.IsDirty = false;
                        Models.Add(displayModel);
                    }

                    StatusMessage = $"{Models.Count} model(s) loaded.";
                });
            }
        }

        private void ModelContextWindowSaveResultEventReceived(MessageBase message)
        {
            if (message is ModelContextWindowSaveResultEvent @event)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    var model = Models.FirstOrDefault(m => m.Id == @event.Id);
                    if (model is null)
                    {
                        return;
                    }

                    model.IsSaving = false;

                    if (@event.Success)
                    {
                        model.IsDirty = false;
                        StatusMessage = $"Saved settings for {model.Name}.";
                    }
                    else
                    {
                        model.IsDirty = true;
                        StatusMessage = $"Failed to save settings for {model.Name}: {@event.ErrorMessage}";
                    }
                });
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not ModelContextWindowDisplayModel model)
            {
                return;
            }

            model.IsSaving = true;
            model.IsDirty = false;
            _messageService.Publish(new UpdateModelContextWindowRequest(model.Id, model.InternalUseOnly, model.CanCallTools));
        }
    }
}
