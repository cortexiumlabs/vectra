using Synentra.BuildingBlocks.Configuration.System.Storage.Cache;
using Synentra.BuildingBlocks.Configuration.System.Storage.Database;

namespace Synentra.BuildingBlocks.Configuration.System;

public class StorageConfiguration
{
    public DatabaseConfiguration Database { get; set; } = new();
    public CacheConfiguration Cache { get; set; } = new();
}