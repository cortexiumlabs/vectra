namespace Vectra.Application.Abstractions.Serializations;

public interface IDeserializer
{
    T Deserialize<T>(string? input) where T : class;
    Task<object> DeserializeDynamicAsync(string json);
    Task<bool> TryDeserializeAsync<T>(string json, out T result) where T : class;
}