namespace FelderBot.Options;

public class OpenAIOptions
{
    public const string SectionName = "OpenAI";

    public string BaseUrl { get; set; } = "https://api.openai.com/v1";
    public string ApiKey { get; set; } = "";
    public string Model { get; set; } = "gpt-4o";
    public string InstructionsPath { get; set; } = "Prompts/system.md";
}
