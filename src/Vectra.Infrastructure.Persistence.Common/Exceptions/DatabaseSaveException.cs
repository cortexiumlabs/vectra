using Vectra.Infrastructure.Persistence.Abstractions.Errors;

namespace Vectra.Infrastructure.Persistence.Abstractions.Exceptions;

public sealed class DatabaseSaveException : PersistenceException
{
    public DatabaseSaveException(Exception exception)
        : base(
            PersistenceErrorCodes.DatabaseSaveData,
            "Failed to save changes to the database.",
            exception
        )
    {
    }
}