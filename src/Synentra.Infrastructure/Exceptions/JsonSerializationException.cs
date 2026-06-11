using Vectra.BuildingBlocks.Errors;

namespace Vectra.Infrastructure.Exceptions;

public sealed class JsonSerializationException : SerializationException
{
    public JsonSerializationException(string message)
    : base(
        VectraErrors.SerializationFailed,
        message
    )
    {
    }

    public JsonSerializationException(string message, Exception ex)
        : base(
            VectraErrors.SerializationFailed,
            message,
            ex
        )
    {
    }
}