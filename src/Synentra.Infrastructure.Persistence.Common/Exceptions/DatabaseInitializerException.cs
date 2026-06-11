using Vectra.Infrastructure.Persistence.Common.Errors;

namespace Vectra.Infrastructure.Persistence.Common.Exceptions;

public sealed class DatabaseInitializerException : PersistenceException
{
    public DatabaseInitializerException(Exception exception)
        : base(
            PersistenceErrorCodes.DatabaseInitializer,
            $"Error occurred while connecting the application database: {exception.Message}",
            exception
        )
    {
    }
}