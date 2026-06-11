using Vectra.Infrastructure.Persistence.Common.Errors;

namespace Vectra.Infrastructure.Persistence.Common.Exceptions;

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