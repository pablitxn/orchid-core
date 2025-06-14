using Application.Interfaces;
using Core.Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public class PersonalityTemplateRepository(ApplicationDbContext context) : IPersonalityTemplateRepository
{
    public async Task<List<PersonalityTemplateEntity>> ListAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var query = context.PersonalityTemplates.AsQueryable();
        
        if (!includeInactive)
        {
            query = query.Where(pt => pt.IsActive);
        }
        
        return await query
            .OrderBy(pt => pt.DisplayOrder)
            .ThenBy(pt => pt.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<PersonalityTemplateEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.PersonalityTemplates
            .FirstOrDefaultAsync(pt => pt.Id == id, cancellationToken);
    }

    public async Task<PersonalityTemplateEntity> CreateAsync(PersonalityTemplateEntity template, CancellationToken cancellationToken = default)
    {
        context.PersonalityTemplates.Add(template);
        await context.SaveChangesAsync(cancellationToken);
        return template;
    }

    public async Task<PersonalityTemplateEntity?> UpdateAsync(PersonalityTemplateEntity template, CancellationToken cancellationToken = default)
    {
        context.PersonalityTemplates.Update(template);
        await context.SaveChangesAsync(cancellationToken);
        return template;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var template = await context.PersonalityTemplates
            .FirstOrDefaultAsync(pt => pt.Id == id, cancellationToken);
            
        if (template == null)
            return false;
            
        // Don't allow deletion of system templates
        if (template.IsSystem)
            return false;
            
        context.PersonalityTemplates.Remove(template);
        await context.SaveChangesAsync(cancellationToken);
        
        return true;
    }
}