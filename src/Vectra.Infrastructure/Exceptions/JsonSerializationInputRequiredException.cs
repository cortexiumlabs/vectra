using Vectra.Infrastructure.Errors;

namespace Vectra.Infrastructure.Exceptions;

public sealed class JsonSerializationInputRequiredException : SerializationException
{
    public JsonSerializationInputRequiredException(Type type)
        : base(
            InfrastructureErrorCodes.JsonSerializationInputRequired,
            $"Input JSON string is null or empty for type {type.Name}."
        )
    {
    }
}