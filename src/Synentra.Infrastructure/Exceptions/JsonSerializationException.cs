using Synentra.BuildingBlocks.Errors;

namespace Synentra.Infrastructure.Exceptions;

public sealed class JsonSerializationException : SerializationException
{
    public JsonSerializationException(string message)
    : base(
        SynentraErrors.SerializationFailed,
        message
    )
    {
    }

    public JsonSerializationException(string message, Exception ex)
        : base(
            SynentraErrors.SerializationFailed,
            message,
            ex
        )
    {
    }
}