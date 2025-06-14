using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Core.Domain.Entities;

namespace Core.Application.Interfaces;

public interface ILoginHistoryRepository
{
    Task<LoginHistoryEntity> CreateAsync(LoginHistoryEntity loginHistory, CancellationToken cancellationToken = default);
    Task<List<LoginHistoryEntity>> GetRecentByUserIdAsync(Guid userId, int count, CancellationToken cancellationToken = default);
    Task<int> GetFailedAttemptsCountAsync(Guid userId, DateTime since, CancellationToken cancellationToken = default);
    Task<LoginHistoryEntity?> GetLastSuccessfulLoginAsync(Guid userId, CancellationToken cancellationToken = default);
}