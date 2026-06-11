namespace Vectra.BuildingBlocks.Clock;

public interface IClock
{
    DateTime UtcNow { get; }
}