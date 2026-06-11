namespace Synentra.BuildingBlocks.Configuration.Policy;

public class OpaPolicyConfiguration
{
    public string BaseUrl { get; set; } = string.Empty;
    public string Path { get; set; } = "/v1/data/synentra/authz";
    public TimeSpan? Timeout { get; set; } = TimeSpan.FromSeconds(5);
}