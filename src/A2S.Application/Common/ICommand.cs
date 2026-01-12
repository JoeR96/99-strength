using MediatR;

namespace A2S.Application.Common;

/// <summary>
/// Marker interface for commands (write operations) that return void.
/// </summary>
public interface ICommand : IRequest
{
}

/// <summary>
/// Marker interface for commands (write operations) that return a result.
/// </summary>
/// <typeparam name="TResponse">The response type</typeparam>
public interface ICommand<out TResponse> : IRequest<TResponse>
{
}
