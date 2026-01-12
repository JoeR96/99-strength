using MediatR;

namespace A2S.Application.Common;

/// <summary>
/// Marker interface for queries (read operations).
/// Queries should always return a result.
/// </summary>
/// <typeparam name="TResponse">The response type</typeparam>
public interface IQuery<out TResponse> : IRequest<TResponse>
{
}
