using KnowledgeAssistant.Wpf.Messages.Documents;
using MessageServices;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;

namespace KnowledgeAssistant.Wpf.Windows
{
    /// <summary>Simple option shown in the topic list; Id is null for the "No topic" entry.</summary>
    public class TopicOption
    {
        public int? Id { get; set; }

        public string Name { get; set; } = string.Empty;
    }

    public partial class TopicSelectionWindow : Window, INotifyPropertyChanged, IMessageServiceSubscriber
    {
        private readonly MessageService _messageService;
        private readonly int? _currentTopicId;
        private TopicOption? _selectedTopic;

        public TopicSelectionWindow(MessageService messageService, int? currentTopicId)
        {
            InitializeComponent();
            DataContext = this;

            _messageService = messageService;
            _currentTopicId = currentTopicId;
            _messageService.Subscribe<TopicsUpdatedEvent>(this, TopicsUpdatedEventReceived);
        }

        public ObservableCollection<TopicOption> Topics { get; } = new ObservableCollection<TopicOption>();

        public TopicOption? SelectedTopic
        {
            get => _selectedTopic;
            set { _selectedTopic = value; OnPropertyChanged(nameof(SelectedTopic)); }
        }

        public bool Confirmed { get; private set; }

        public int? SelectedTopicId { get; private set; }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void TopicSelectionWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _messageService.Publish(new GetTopicsRequest());
        }

        private void TopicsUpdatedEventReceived(MessageBase message)
        {
            if (message is TopicsUpdatedEvent @event)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    Topics.Clear();
                    Topics.Add(new TopicOption { Id = null, Name = "No topic" });
                    foreach (var topic in @event.Topics)
                    {
                        Topics.Add(new TopicOption { Id = topic.Id, Name = topic.Name });
                    }

                    SelectedTopic = Topics.FirstOrDefault(t => t.Id == _currentTopicId) ?? Topics.First();
                });
            }
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            SelectedTopicId = SelectedTopic?.Id;
            Confirmed = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Confirmed = false;
            Close();
        }
    }
}
