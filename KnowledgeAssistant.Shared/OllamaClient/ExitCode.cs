namespace OllamaClients;

public static class ExitCode
{
    public const int Success = 0;
    public const int GeneralError = 1;
    public const int FileNotFound = 2;
    public const int AmbiguousMatch = 3;
    public const int ConfigurationError = 4;
    public const int LlmCallFailed = 5;
}