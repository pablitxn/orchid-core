using Application.Interfaces;
using Domain.Entities;
using Domain.Events;
using MediatR;

namespace Application.UseCases.Auth.Register;

public class RegisterHandler(
    IUserRepository userRepository,
    IRoleRepository roleRepository,
    IPasswordHasher passwordHasher,
    IEventPublisher eventPublisher)
    : IRequestHandler<RegisterCommand, RegisterResult>
{
    private readonly IPasswordHasher _passwordHasher = passwordHasher;
    private readonly IRoleRepository _roles = roleRepository;
    private readonly IUserRepository _users = userRepository;
    private readonly IEventPublisher _eventPublisher = eventPublisher;

    public async Task<RegisterResult> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        var existing = await _users.GetByEmailAsync(request.Email);
        if (existing != null!)
            return RegisterResult.Failure("An account with this email already exists");

        var newUser = new UserEntity
        {
            Email = request.Email,
            PasswordHash = _passwordHasher.HashPassword(request.Password),
            Name = $"user_{Guid.NewGuid()}"
        };
        var created = await _users.CreateAsync(newUser);

        if (request.Roles != null)
            foreach (var roleName in request.Roles)
            {
                // Only allow predefined roles to be assigned
                var role = await _roles.GetByNameAsync(roleName);
                if (role == null)
                    continue; // Skip unknown roles instead of creating them

                await _roles.AssignRoleToUserAsync(created.Id, role.Id);
            }

        // Publish user created event to trigger knowledge base seeding
        await _eventPublisher.PublishAsync(new UserCreatedEvent(created.Id, created.Email));

        return RegisterResult.Success(created);
    }
}