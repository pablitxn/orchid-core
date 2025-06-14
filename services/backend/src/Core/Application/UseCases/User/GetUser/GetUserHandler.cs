using Application.Interfaces;
using Domain.Entities;
using MediatR;

namespace Application.UseCases.User.GetUser;

public class GetUserHandler(IUserRepository userRepository) : IRequestHandler<GetUserCommand, UserEntity>
{
    private readonly IUserRepository _userRepository = userRepository;

    public async Task<UserEntity> Handle(GetUserCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByEmailAsync(request.Email);
        return user;
    }
}