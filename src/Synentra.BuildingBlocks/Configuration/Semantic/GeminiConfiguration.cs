namespace Vectra.BuildingBlocks.Configuration.Semantic;

public class GeminiConfiguration
{
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gemini-2.0-flash";
    public double? Temperature { get; set; } = 0.0;
    public int? MaxTokens { get; set; } = 256;
}
