using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Core.Application.Interfaces;
using Core.Domain.Entities;
using Domain.Entities;
using Infrastructure.Persistence;

namespace Infrastructure.Repositories;

public sealed class UserWorkflowRepository : IUserWorkflowRepository
{
    private readonly ApplicationDbContext _context;

    public UserWorkflowRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<UserWorkflowEntity> CreateAsync(UserWorkflowEntity userWorkflow,
        CancellationToken cancellationToken = default)
    {
        _context.UserWorkflows.Add(userWorkflow);
        await _context.SaveChangesAsync(cancellationToken);
        return userWorkflow;
    }

    public async Task<UserWorkflowEntity?> GetByIdAsync(Guid userId, Guid workflowId,
        CancellationToken cancellationToken = default)
    {
        return await _context.UserWorkflows
            .Include(uw => uw.Workflow)
            .FirstOrDefaultAsync(uw => uw.UserId == userId && uw.WorkflowId == workflowId, cancellationToken);
    }

    public async Task<List<UserWorkflowEntity>> GetByUserIdAsync(Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await _context.UserWorkflows
            .Include(uw => uw.Workflow)
            .Where(uw => uw.UserId == userId)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<UserWorkflowEntity>> GetByWorkflowIdAsync(Guid workflowId,
        CancellationToken cancellationToken = default)
    {
        return await _context.UserWorkflows
            .Include(uw => uw.User)
            .Where(uw => uw.WorkflowId == workflowId)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsAsync(Guid userId, Guid workflowId, CancellationToken cancellationToken = default)
    {
        return await _context.UserWorkflows
            .AnyAsync(uw => uw.UserId == userId && uw.WorkflowId == workflowId, cancellationToken);
    }

    public async Task DeleteAsync(Guid userId, Guid workflowId, CancellationToken cancellationToken = default)
    {
        var userWorkflow = await GetByIdAsync(userId, workflowId, cancellationToken);
        if (userWorkflow != null)
        {
            _context.UserWorkflows.Remove(userWorkflow);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}