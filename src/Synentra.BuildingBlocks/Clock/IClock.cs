namespace Synentra.BuildingBlocks.Clock;

public interface IClock
{
    DateTime UtcNow { get; }
}