using Synentra.Infrastructure.Persistence.Common.Errors;

namespace Synentra.Infrastructure.Persistence.Common.Exceptions;

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