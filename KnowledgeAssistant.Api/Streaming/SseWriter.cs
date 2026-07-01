using System.Text.Json;

namespace KnowledgeAssistant.Api.Streaming
{
    public sealed class SseWriter
    {
        private readonly HttpResponse _response;

        public SseWriter(HttpResponse response)
        {
            _response = response;

            _response.Headers.Append("Content-Type", "text/event-stream");
            _response.Headers.Append("Cache-Control", "no-cache");
            _response.Headers.Append("Connection", "keep-alive");
        }

        public async Task WriteAsync(string eventName, object? data, CancellationToken ct)
        {
            if (ct.IsCancellationRequested)
                return;

            var json = data is null
                ? "{}"
                : JsonSerializer.Serialize(data);

            await _response.WriteAsync($"event: {eventName}\n", ct);
            await _response.WriteAsync($"data: {json}\n\n", ct);

            await _response.Body.FlushAsync(ct);
        }
    }
}