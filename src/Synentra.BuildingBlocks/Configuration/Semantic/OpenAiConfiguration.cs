namespace Vectra.BuildingBlocks.Configuration.Semantic;

public class OpenAiConfiguration
{
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gpt-4o-mini";
    public double? Temperature { get; set; } = 0.0;
    public int? MaxTokens { get; set; } = 256;
}
