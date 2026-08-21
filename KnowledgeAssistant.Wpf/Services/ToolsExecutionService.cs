using KnowledgeAssistant.Contracts.Dto;
using KnowledgeAssistant.Wpf.Messages.Documentation;
using KnowledgeAssistant.Wpf.Messages.ToolsExecution;
using KnowledgeAssistant.Wpf.Messages.ToolsManagement;
using MessageServices;
using System.Diagnostics;
using System.IO;
using System.Text;
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
            using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
            string lastLine = string.Empty;
            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data is null) return;
                lastLine = e.Data.ToString();
                _messageService.Publish(new ToolExecutionOutputIntermediateEvent(request.ToolId));
            };

            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data is null) return;
                lastLine = e.Data.ToString();
                _messageService.Publish(new ToolExecutionOutputIntermediateEvent(request.ToolId));
            };

            try
            {
                process.Start();
                process.BeginOutputReadLine(); 
                process.BeginErrorReadLine();
                await process.WaitForExitAsync();

                var toolResult = JsonSerializer.Deserialize<ToolResult>(lastLine);
                _messageService.Publish(new ToolExecutionCompletedRequest(request.ToolId, toolResult));
                _messageService.Publish(new DocumentationReadyEvent(request.Path, Path.GetFileName(request.Path)));
            }
            catch (Exception ex)
            {
                TryKill(process);
                var errorResult = new ToolResult
                {
                    Status = "error",
                    Reason = "Execution failed",
                    Message = ex.Message
                };

                _messageService.Publish(new ToolExecutionCompletedRequest(request.ToolId, errorResult));
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