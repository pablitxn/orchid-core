using Application.Interfaces;
using Application.UseCases.User.Common;
using Domain.Entities;
using MediatR;

namespace Application.UseCases.User.CreateUser;

public class CreateUserCommandHandler(IUserRepository userRepository, IRoleRepository roleRepository)
    : IRequestHandler<CreateUserCommand, UserDto>
{
    private readonly IRoleRepository _roleRepository = roleRepository;
    private readonly IUserRepository _userRepository = userRepository;

    public async Task<UserDto> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        var existingUser = await _userRepository.GetByEmailAsync(request.Email);
        if (existingUser != null) return UserDto.FromEntity(existingUser);

        var randomName = $"user_{Guid.NewGuid()}";

        var newUser = new UserEntity
        {
            Email = request.Email,
            Name = randomName
        };

        var created = await _userRepository.CreateAsync(newUser);

        if (request.Roles != null)
            foreach (var roleName in request.Roles)
            {
                var role = await _roleRepository.GetByNameAsync(roleName) ??
                           await _roleRepository.CreateAsync(new RoleEntity { Name = roleName });
                await _roleRepository.AssignRoleToUserAsync(created.Id, role.Id);
            }

        // Reload user to get roles
        var userWithRoles = await _userRepository.GetByEmailAsync(request.Email);
        return UserDto.FromEntity(userWithRoles!);
    }
}
