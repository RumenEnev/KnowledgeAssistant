using KnowledgeAssistant.Wpf.Markdown;
using KnowledgeAssistant.Wpf.Messages;
using MessageServices;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace KnowledgeAssistant.Wpf.UserControls
{
    public partial class UCChatMessage : UserControl, INotifyPropertyChanged, IMessageServiceSubscriber
    {
        private readonly MessageService? _messageService;
        private bool _assistantMessageCompleted;
        private bool _isUserMessage;
        private string? _message;

        public UCChatMessage(MessageService? messageService)
        {
            InitializeComponent();
            DataContext = this;

            MarkdownContent.Pipeline = ChatMarkdownPipeline.Instance;

            _messageService = messageService;
            _messageService?.Subscribe<ChunkReceivedEvent>(this, ChunkReceivedEventReceived);
        }

        public bool IsUserMessage
        {
            get => _isUserMessage;
            set
            {
                _isUserMessage = value;
                OnPropertyChanged(nameof(IsUserMessage));
                OnPropertyChanged(nameof(ChatMessageMargin));
            }
        }

        public Thickness ChatMessageMargin =>
            IsUserMessage
                ? new Thickness(150, 2, 2, 2)
                : new Thickness(2, 2, 150, 2);

        public bool MessageCompleted
        {
            get => _assistantMessageCompleted;
            set
            {
                _assistantMessageCompleted = value;
                OnPropertyChanged(nameof(MessageCompleted));
            }
        }

        public string? Message
        {
            get => _message;
            set
            {
                _message = MathDelimiterNormalizer.Normalize(value);
                OnPropertyChanged(nameof(Message));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void ChunkReceivedEventReceived(MessageBase message)
        {
            if (message is ChunkReceivedEvent request && !MessageCompleted)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Message += request.Content;
                });
            }
        }

        private void UserControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (MainGrid.ActualWidth > 0)
            {
                MarkdownContent.MaxWidth = MainGrid.ActualWidth - 10; 
            }
        }

        private void Copy_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(Message ?? string.Empty);
        }
    }
}