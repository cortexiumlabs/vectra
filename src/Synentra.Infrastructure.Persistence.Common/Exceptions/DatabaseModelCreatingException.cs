using Vectra.Infrastructure.Persistence.Common.Errors;

namespace Vectra.Infrastructure.Persistence.Common.Exceptions;

public sealed class DatabaseModelCreatingException : PersistenceException
{
    public DatabaseModelCreatingException(Exception exception)
        : base(
            PersistenceErrorCodes.DatabaseModelCreating,
            "An error occurred while building the EF Core model.",
            exception
        )
    {
    }
}