using ClientPortal.SharedKernel.Results;

namespace ClientPortal.Application.Common.Messaging;

public interface ICommandHandler<in TCommand, TResponse>
{
    Task<Result<TResponse>> HandleAsync(TCommand command, CancellationToken cancellationToken);
}
