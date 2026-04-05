namespace Vectra.Application.Abstractions.Dispatchers;

public interface IDispatcher
{
    Task<TResult> Dispatch<TResult>(IAction<TResult> request, CancellationToken cancellationToken = default);
}