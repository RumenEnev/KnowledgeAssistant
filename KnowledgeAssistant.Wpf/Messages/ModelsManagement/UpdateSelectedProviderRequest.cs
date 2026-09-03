using MessageServices;

namespace KnowledgeAssistant.Wpf.Messages.ModelsManagement;

public record UpdateSelectedProviderRequest : MessageBase
{
    public UpdateSelectedProviderRequest(string selectedProvider)
    {
        SelectedProvider = selectedProvider;
    }

    public string SelectedProvider { get; }
}