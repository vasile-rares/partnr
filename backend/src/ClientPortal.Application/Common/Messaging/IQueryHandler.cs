using ClientPortal.SharedKernel.Results;

namespace ClientPortal.Application.Common.Messaging;

public interface IQueryHandler<in TQuery, TResponse>
{
    Task<Result<TResponse>> HandleAsync(TQuery query, CancellationToken cancellationToken);
}
