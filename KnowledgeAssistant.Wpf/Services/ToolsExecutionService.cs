using KnowledgeAssistant.Contracts.Dto;
using KnowledgeAssistant.Wpf.Messages.Documentation;
using KnowledgeAssistant.Wpf.Messages.ToolsManagement;
using KnowledgeAssistant.Wpf.Models;
using MessageServices;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace KnowledgeAssistant.Wpf.Services;

public class ToolsExecutionService : IMessageServiceSubscriber
{
    private readonly MessageService _messageService;

    public ToolsExecutionService(MessageService messageService)
    {
        _messageService = messageService;

        _messageService.Subscribe<ExecuteToolRequest>(this, ExecuteToolRequestReceived);
    }

    private async void ExecuteToolRequestReceived(MessageBase message)
    {
        if (message is ExecuteToolRequest request)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = request.Path,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            startInfo.ArgumentList.Add(request.ArgumentsJson);
            using var process = new Process { StartInfo = startInfo };
            try
            {
                process.Start();
                var stdOutTask = process.StandardOutput.ReadToEndAsync();
                var stdErrTask = process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                var toolResult = JsonSerializer.Deserialize<ToolResult>(await stdOutTask);
                _messageService.Publish(new ToolExecutionCompletedRequest(request.ToolId, toolResult));
                _messageService.Publish(new DocumentationReadyEvent(toolResult.OutputPath, Path.GetFileName(request.Path)));
            }
            catch (Exception ex)
            {
                TryKill(process);
                _messageService.Publish(new ToolExecutionCompletedRequest(request.ToolId, new ToolResult { Reason = "Operation failed", Message = ex.Message }));
            }
        }
    }

    private void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception ex)
        {
            //_logger.LogWarning(ex, "Failed to kill process after timeout.");
        }
    }
}