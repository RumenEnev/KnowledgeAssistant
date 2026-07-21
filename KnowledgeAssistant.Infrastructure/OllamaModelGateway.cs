using KnowledgeAssistant.Application.Abstraction;
using KnowledgeAssistant.Domain.Conversation;
using KnowledgeAssistant.Infrastructure.Dto;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace KnowledgeAssistant.Infrastructure
{
    public class OllamaModelGateway : IModelGateway
    {
        private int _promptTokensCount;
        private int _responseTokensCount;

        private readonly HttpClient _httpClient;

        public OllamaModelGateway(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public (int, int) GetTokenConsumption()
        {
            return (_promptTokensCount, _responseTokensCount);
        }

        public async Task<string> GenerateAsync(string model, ChatMessage userMessage, ChatMessage systemMessage, CancellationToken cancellationToken)
        {
            var request = new
            {
                model,
                messages = new[]
                {
                    new { role = systemMessage.Role, content = systemMessage.Content },
                    new { role = userMessage.Role, content = userMessage.Content },
                },
                stream = false
            };

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/chat");
            httpRequest.Content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
            using var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            await EnsureSuccessAsync(response, model, cancellationToken);

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<OllamaChatResponseDto>(content);
            return result?.Message?.Content ?? string.Empty;
        }

        public async IAsyncEnumerable<string> StreamAsync(string model, List<ChatMessage> messages, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            _promptTokensCount = 0;
            _responseTokensCount = 0;
            var request = new
            {
                model,
                messages = messages.Select(m => new
                {
                    role = m.Role,
                    content = m.Content
                }),
                stream = true
            };

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/chat");
            httpRequest.Content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
            using var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            await EnsureSuccessAsync(response, model, cancellationToken);

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream);
            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                var chunk = JsonSerializer.Deserialize<OllamaChatResponseDto>(line);
                if (chunk?.Message?.Content != null)
                {
                    yield return chunk.Message.Content;
                }

                if (chunk?.Done == true)
                {
                    _promptTokensCount = chunk.PromptEvalCount;
                    _responseTokensCount = chunk.EvalCount;
                    yield break;
                }
            }
        }

        public async Task<float[]> GetEmbeddingAsync(string model, string text, CancellationToken cancellationToken)
        {
            var request = new
            {
                model,
                prompt = text
            };

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/embeddings");
            httpRequest.Content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
            using var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            await EnsureSuccessAsync(response, model, cancellationToken);

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<OllamaEmbeddingResponseDto>(content);
            return result?.Embedding ?? Array.Empty<float>();
        }

        /// <summary>
        /// Throws with the actual Ollama error body (e.g. "model 'llama3' not found") instead of the
        /// generic HttpRequestException message from EnsureSuccessStatusCode, to make failures diagnosable.
        /// </summary>
        private static async Task EnsureSuccessAsync(HttpResponseMessage response, string model, CancellationToken cancellationToken)
        {
            if (response.IsSuccessStatusCode)
            {
                return;
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var reason = body;
            try
            {
                var error = JsonSerializer.Deserialize<OllamaErrorResponseDto>(body);
                if (!string.IsNullOrWhiteSpace(error?.Error))
                {
                    reason = error.Error;
                }
            }
            catch (JsonException)
            {
                // Body wasn't the expected JSON shape; fall back to the raw text.
            }

            throw new InvalidOperationException(
                $"Ollama request for model '{model}' failed with status {(int)response.StatusCode} ({response.StatusCode}): {reason}");
        }
    }
}