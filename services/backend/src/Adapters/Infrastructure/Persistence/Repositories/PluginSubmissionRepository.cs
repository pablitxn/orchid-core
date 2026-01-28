using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public class PluginSubmissionRepository(ApplicationDbContext dbContext) : IPluginSubmissionRepository
{
    private readonly ApplicationDbContext _dbContext = dbContext;

    public async Task<PluginSubmissionEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.PluginSubmissions
            .Include(s => s.Publisher)
            .Include(s => s.Plugin)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public async Task<List<PluginSubmissionEntity>> ListByPublisherAsync(Guid publisherId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.PluginSubmissions
            .Include(s => s.Plugin)
            .Where(s => s.PublisherId == publisherId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<PluginSubmissionEntity>> ListByStatusAsync(SubmissionStatus status, CancellationToken cancellationToken = default)
    {
        return await _dbContext.PluginSubmissions
            .Include(s => s.Publisher)
            .Include(s => s.Plugin)
            .Where(s => s.Status == status)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<PluginSubmissionEntity>> ListPendingAsync(CancellationToken cancellationToken = default)
    {
        return await ListByStatusAsync(SubmissionStatus.Pending, cancellationToken);
    }

    public async Task<bool> ExistsBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        return await _dbContext.PluginSubmissions
            .AnyAsync(s => s.Slug == slug, cancellationToken);
    }

    public async Task CreateAsync(PluginSubmissionEntity submission, CancellationToken cancellationToken = default)
    {
        _dbContext.PluginSubmissions.Add(submission);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(PluginSubmissionEntity submission, CancellationToken cancellationToken = default)
    {
        submission.UpdatedAt = DateTime.UtcNow;
        _dbContext.PluginSubmissions.Update(submission);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
