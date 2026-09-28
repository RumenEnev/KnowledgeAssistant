using KnowledgeAssistant.Wpf.Messages.SpeechToText;
using MessageServices;
using System.Speech.Recognition;
using System.Windows.Threading;

namespace KnowledgeAssistant.Wpf.Services.Speech
{
    public sealed class SystemSpeechToTextService : IMessageServiceSubscriber
    {
        private readonly MessageService _messageService;
        private readonly Dispatcher _dispatcher;
        private SpeechRecognitionEngine? _engine;
        private bool _isListening;
        private bool _disposed;

        public SystemSpeechToTextService(MessageService messageService)
        {
            _messageService = messageService;
            _dispatcher = System.Windows.Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;

            try
            {
                IsSupported = SpeechRecognitionEngine.InstalledRecognizers().Count > 0;
            }
            catch
            {
                IsSupported = false;
            }
        }

        public bool IsSupported { get; }

        public bool IsListening => _isListening;


        public void Start()
        {
            if (_disposed)
            {
                return;
            }

            if (!IsSupported)
            {
                _messageService.Publish(new SpeechToTextErrorRequest("Voice input is not supported on this system. No speech recognizer is installed."));
                return;
            }

            if (_isListening)
            {
                return;
            }

            try
            {
                var engine = new SpeechRecognitionEngine();
                engine.LoadGrammar(new DictationGrammar());
                engine.SetInputToDefaultAudioDevice();
                engine.SpeechHypothesized += OnSpeechHypothesized;
                engine.SpeechRecognized += OnSpeechRecognized;
                engine.RecognizeCompleted += OnRecognizeCompleted;

                _engine = engine;
                _isListening = true;
                engine.RecognizeAsync(RecognizeMode.Multiple);
            }
            catch (InvalidOperationException)
            {
                CleanupEngine();
                _messageService.Publish(new SpeechToTextErrorRequest("No microphone was found, or microphone access was denied. Please connect a microphone, " +
                                                            "check Windows microphone privacy settings, and try again."));
            }
            catch (Exception ex)
            {
                CleanupEngine();
                _messageService.Publish(new SpeechToTextErrorRequest($"Failed to start voice input: {ex.Message}"));
            }
        }

        public void Stop()
        {
            var engine = _engine;
            if (engine == null)
            {
                return;
            }

            try
            {
                engine.RecognizeAsyncStop();
            }
            catch
            {
                // Best-effort; if this fails there is nothing more useful we can do here.
            }
        }

        private void OnSpeechHypothesized(object? sender, SpeechHypothesizedEventArgs e)
        {
            var text = e.Result?.Text;
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            RunOnUiThread(() => _messageService.Publish(new SpeechToTextResultRequest(text, string.Empty, false)));
        }

        private void OnSpeechRecognized(object? sender, System.Speech.Recognition.SpeechRecognizedEventArgs e)
        {
            var text = e.Result?.Text;
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            RunOnUiThread(() => _messageService.Publish(new SpeechToTextResultRequest(text, string.Empty, false)));
        }

        private void OnRecognizeCompleted(object? sender, RecognizeCompletedEventArgs e)
        {
            RunOnUiThread(() =>
            {
                CleanupEngine();

                if (e.Error != null)
                {
                    _messageService.Publish(new SpeechToTextErrorRequest($"Voice input error: {e.Error.Message}"));
                }

                _messageService?.Publish(new SpeachToTextStopRequest());
            });
        }

        private void CleanupEngine()
        {
            _isListening = false;
            var engine = _engine;
            _engine = null;
            if (engine == null)
            {
                return;
            }

            engine.SpeechHypothesized -= OnSpeechHypothesized;
            engine.SpeechRecognized -= OnSpeechRecognized;
            engine.RecognizeCompleted -= OnRecognizeCompleted;
            engine.Dispose();
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

            var engine = _engine;
            _engine = null;
            _isListening = false;
            if (engine == null)
            {
                return;
            }

            try
            {
                engine.RecognizeAsyncCancel();
            }
            catch
            {
                // Best-effort during teardown.
            }

            engine.SpeechHypothesized -= OnSpeechHypothesized;
            engine.SpeechRecognized -= OnSpeechRecognized;
            engine.RecognizeCompleted -= OnRecognizeCompleted;
            engine.Dispose();
        }
    }
}