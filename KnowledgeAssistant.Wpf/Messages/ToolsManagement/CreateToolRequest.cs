using MessageServices;

namespace KnowledgeAssistant.Wpf.Messages.ToolsManagement
{
    public record CreateToolRequest : MessageBase
    {
        public CreateToolRequest(string name, string description, string parametersJsonSchema, bool isEnabled, string? endpointUrl, string httpMethod, string? authLoginUrl, string? authUsername, string? authPassword)
        {
            Name = name;
            Description = description;
            ParametersJsonSchema = parametersJsonSchema;
            IsEnabled = isEnabled;
            EndpointUrl = endpointUrl;
            HttpMethod = httpMethod;
            AuthLoginUrl = authLoginUrl;
            AuthUsername = authUsername;
            AuthPassword = authPassword;
        }

        public string Name { get; }

        public string Description { get; }

        public string ParametersJsonSchema { get; }

        public bool IsEnabled { get; }

        public string? EndpointUrl { get; }

        public string HttpMethod { get; }

        public string? AuthLoginUrl { get; }

        public string? AuthUsername { get; }

        public string? AuthPassword { get; }
    }
}
