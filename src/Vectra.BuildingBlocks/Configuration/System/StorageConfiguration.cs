using Vectra.BuildingBlocks.Configuration.System.Storage.Cache;
using Vectra.BuildingBlocks.Configuration.System.Storage.Database;

namespace Vectra.BuildingBlocks.Configuration.System;

public class StorageConfiguration
{
    public DatabaseConfiguration Database { get; set; } = new();
    public CacheConfiguration Cache { get; set; } = new();
}