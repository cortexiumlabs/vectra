namespace Vectra.BuildingBlocks.Configuration.Semantic;

public class LocalOnnxConfiguration
{
    public string ModelPath { get; set; } = string.Empty;
    public string VocabPath { get; set; } = string.Empty;
    public string LabelsPath { get; set; } = string.Empty;
    public int? MaxLength { get; set; } = 128;
}