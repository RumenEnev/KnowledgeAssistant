using KnowledgeAssistant.Wpf.Markdown;
using KnowledgeAssistant.Wpf.Messages;
using KnowledgeAssistant.Wpf.Messages.SpeechToText;
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
        private bool _isSpeaking;
        private bool _isPaused;
        private bool _speechControlsVisible;

        public UCChatMessage(MessageService? messageService)
        {
            InitializeComponent();
            DataContext = this;

            MarkdownContent.Pipeline = ChatMarkdownPipeline.Instance;

            _messageService = messageService;
            _messageService?.Subscribe<ChunkReceivedEvent>(this, ChunkReceivedEventReceived);
            _messageService?.Subscribe<TextToSpeechSupportEvent>(this, TextToSpeechSupportEventReceived);
            _messageService?.Subscribe<TextToSpeechSpeakEndEvent>(this, TextToSpeechSpeakEndEventReceived);
            _messageService?.Subscribe<TextToSpeechSpeakErrorEvent>(this, TextToSpeechSpeakErrorEventReceived);

            Unloaded += UCChatMessage_Unloaded;
        }

        private void TextToSpeechSpeakErrorEventReceived(MessageBase message)
        {
            if (message is TextToSpeechSpeakErrorEvent)
            {
                IsSpeaking = false;
                IsPaused = false;
            }
        }

        private void TextToSpeechSpeakEndEventReceived(MessageBase message)
        {
            if (message is TextToSpeechSpeakEndEvent)
            {
                IsSpeaking = false;
                IsPaused = false;
            }
        }

        public bool IsUserMessage
        {
            get => _isUserMessage;
            set
            {
                _isUserMessage = value;
                OnPropertyChanged(nameof(IsUserMessage));
                OnPropertyChanged(nameof(ChatMessageMargin));
                OnPropertyChanged(nameof(SpeechControlsVisible));
            }
        }


        public bool SpeechControlsVisible
        {
            get => _speechControlsVisible;
            private set
            {
                if (_speechControlsVisible != value)
                {
                    _speechControlsVisible = value;
                    OnPropertyChanged(nameof(SpeechControlsVisible));
                }
            }
        }   

        public bool IsSpeaking
        {
            get => _isSpeaking;
            private set
            {
                if (_isSpeaking != value)
                {
                    _isSpeaking = value;
                    OnPropertyChanged(nameof(IsSpeaking));
                    OnPropertyChanged(nameof(SpeechIconGlyph));
                }
            }
        }

        public bool IsPaused
        {
            get => _isPaused;
            private set
            {
                if (_isPaused != value)
                {
                    _isPaused = value;
                    OnPropertyChanged(nameof(IsPaused));
                    OnPropertyChanged(nameof(SpeechIconGlyph));
                }
            }
        }

        public string SpeechIconGlyph => IsSpeaking && !IsPaused ? "\uE769" : "\uE767";

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
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    Message += request.Content;
                });
            }
        }

        private void TextToSpeechSupportEventReceived(MessageBase message)
        {
            if (message is TextToSpeechSupportEvent request)
            {
                SpeechControlsVisible = request.IsSupported;
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

        private void ToggleSpeech_Click(object sender, RoutedEventArgs e)
        {
            if (IsSpeaking)
            {
                if (IsPaused)
                {
                    _messageService?.Publish(new TextToSpeechResumeRequest());
                    IsPaused = false;
                }
                else
                {
                    _messageService?.Publish(new TextToSpeechPauseRequest());
                    IsPaused = true;
                }
                return;
            }

            _messageService?.Publish(new TextToSpeechSpeakEvent(Message ?? string.Empty));
            IsSpeaking = true;
            IsPaused = false;
        }

        private void StopSpeech_Click(object sender, RoutedEventArgs e)
        {
            _messageService?.Publish(new TextToSpeechStopRequest());
            IsSpeaking = false;
            IsPaused = false;
        }

        private void UCChatMessage_Unloaded(object sender, RoutedEventArgs e)
        {
            if (IsSpeaking)
            {
                _messageService?.Publish(new TextToSpeechStopRequest());
            }
        }
    }
}