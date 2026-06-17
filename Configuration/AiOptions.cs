namespace PMG201c.Backend.Configuration;

public class AiOptions
{
    public const string SectionName = "AI";

    public string Provider { get; set; } = "Mock";
    public OpenRouterOptions OpenRouter { get; set; } = new();
}

public class OpenRouterOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://openrouter.ai/api/v1";
    public string Model { get; set; } = "google/gemini-2.0-flash-exp:free";
    public int MaxTokens { get; set; } = 4096;
    public double Temperature { get; set; } = 0.2;
    public int TimeoutSeconds { get; set; } = 120;
}
