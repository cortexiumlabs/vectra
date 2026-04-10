using Vectra.BuildingBlocks.Configuration.System.Server;

namespace Vectra.BuildingBlocks.Configuration.System;

public class SystemConfiguration
{
    public ServerConfiguration Server { get; set; } = new();
    public StorageConfiguration Storage { get; set; } = new();
}