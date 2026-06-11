namespace Vectra.BuildingBlocks.Clock;

public class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}