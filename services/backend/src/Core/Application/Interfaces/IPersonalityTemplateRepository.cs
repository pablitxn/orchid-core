using Core.Domain.Entities;

namespace Application.Interfaces;

public interface IPersonalityTemplateRepository
{
    Task<List<PersonalityTemplateEntity>> ListAsync(bool includeInactive = false, CancellationToken cancellationToken = default);
    Task<PersonalityTemplateEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PersonalityTemplateEntity> CreateAsync(PersonalityTemplateEntity template, CancellationToken cancellationToken = default);
    Task<PersonalityTemplateEntity?> UpdateAsync(PersonalityTemplateEntity template, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}