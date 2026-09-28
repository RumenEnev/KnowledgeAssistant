using KnowledgeAssistant.Wpf.Messages.SpeechToText;
using MessageServices;
using System.Speech.Synthesis;
using System.Windows.Threading;

namespace KnowledgeAssistant.Wpf.Services.Speech
{
    public sealed class SystemTextToSpeechService : IMessageServiceSubscriber
    {
        private readonly MessageService _messageService;
        private readonly Dispatcher _dispatcher;
        private readonly SpeechSynthesizer? _synthesizer;
        private Action? _currentOnEnd;
        private Action<string>? _currentOnError;
        private bool _isSpeaking;
        private bool _isPaused;
        private bool _disposed;

        public SystemTextToSpeechService(MessageService messageService)
        {
            _messageService = messageService;
            _dispatcher = System.Windows.Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
            try
            {
                var synthesizer = new SpeechSynthesizer();
                synthesizer.SpeakStarted += OnSpeakStarted;
                synthesizer.SpeakCompleted += OnSpeakCompleted;
                _synthesizer = synthesizer;
                IsSupported = true;
            }
            catch
            {
                _synthesizer = null;
                IsSupported = false;
            }

            _messageService.Subscribe<TextToSpeechResumeRequest>(this, TextToSpeechResumeRequestReceived);
            _messageService.Subscribe<TextToSpeechPauseRequest>(this, TextToSpeechPauseRequestReceived);
            _messageService.Subscribe<TextToSpeechSpeakEvent>(this, TextToSpeechSpeakEventReceived);

            _messageService.Publish(new TextToSpeechSupportEvent(IsSupported));
        }

        public bool IsSupported { get; }

        public bool IsSpeaking => _isSpeaking;

        public bool IsPaused => _isPaused;

        private void TextToSpeechSpeakEventReceived(MessageBase message)
        {
            if (message is TextToSpeechSpeakEvent speakEvent)
            {
                Speak(speakEvent.Text, 
                    onEnd: () => _messageService.Publish(new TextToSpeechSpeakEndEvent()), 
                    onError: (error) => _messageService.Publish(new TextToSpeechSpeakErrorEvent()));
            }
        }

        private void TextToSpeechResumeRequestReceived(MessageBase message)
        {
            if (message is TextToSpeechResumeRequest)
            {
                Resume();
            }
        }

        private void TextToSpeechPauseRequestReceived(MessageBase message)
        {
            if (message is TextToSpeechPauseRequest)
            {
                Pause();
            }
        }

        public void Speak(string text, Action? onEnd = null, Action<string>? onError = null)
        {
            if (_disposed)
            {
                return;
            }

            if (_synthesizer == null)
            {
                onError?.Invoke("Voice output is not supported on this system.");
                return;
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            Stop();

            _currentOnEnd = onEnd;
            _currentOnError = onError;

            try
            {
                _synthesizer.SpeakAsync(text);
            }
            catch (Exception ex)
            {
                _isSpeaking = false;
                _isPaused = false;
                var failedOnError = _currentOnError;
                _currentOnEnd = null;
                _currentOnError = null;
                failedOnError?.Invoke($"Voice output error: {ex.Message}");
            }
        }

        public void Pause()
        {
            if (_synthesizer == null || !_isSpeaking || _isPaused)
            {
                return;
            }

            try
            {
                _synthesizer.Pause();
                _isPaused = true;
            }
            catch
            {
                // Can race with natural completion; safe to ignore.
            }
        }

        public void Resume()
        {
            if (_synthesizer == null || !_isPaused)
            {
                return;
            }

            try
            {
                _synthesizer.Resume();
                _isPaused = false;
            }
            catch
            {
                // Can race with natural completion; safe to ignore.
            }
        }

        public void Stop()
        {
            var previousOnEnd = _currentOnEnd;
            _currentOnEnd = null;
            _currentOnError = null;

            if (_synthesizer == null)
            {
                previousOnEnd?.Invoke();
                return;
            }

            try
            {
                _synthesizer.SpeakAsyncCancelAll();
            }
            catch
            {
                // Best-effort.
            }

            _isSpeaking = false;
            _isPaused = false;

            previousOnEnd?.Invoke();
        }

        private void OnSpeakStarted(object? sender, SpeakStartedEventArgs e)
        {
            RunOnUiThread(() =>
            {
                _isSpeaking = true;
                _isPaused = false;
            });
        }

        private void OnSpeakCompleted(object? sender, SpeakCompletedEventArgs e)
        {
            RunOnUiThread(() =>
            {
                _isSpeaking = false;
                _isPaused = false;

                var onEnd = _currentOnEnd;
                var onError = _currentOnError;
                _currentOnEnd = null;
                _currentOnError = null;

                if (e.Cancelled)
                {
                    return;
                }

                if (e.Error != null)
                {
                    onError?.Invoke($"Voice output error: {e.Error.Message}");
                    return;
                }

                onEnd?.Invoke();
            });
        }

        private void RunOnUiThread(Action action)
        {
            if (_dispatcher.CheckAccess())
            {
                action();
            }
            else
            {
                _dispatcher.Invoke(action);
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;

            if (_synthesizer == null)
            {
                return;
            }

            _synthesizer.SpeakStarted -= OnSpeakStarted;
            _synthesizer.SpeakCompleted -= OnSpeakCompleted;
            try
            {
                _synthesizer.SpeakAsyncCancelAll();
            }
            catch
            {
                // Best-effort during teardown.
            }
            _synthesizer.Dispose();
        }
    }
}