namespace Vectra.BuildingBlocks.Configuration.Features.Policy;

public class OpaPolicyConfiguration
{
    public string BaseUrl { get; set; } = string.Empty;
    public string Path { get; set; } = "/v1/data/vectra/authz";
    public int TimeoutMilliseconds { get; set; } = 5000;
}