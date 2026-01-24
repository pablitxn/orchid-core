using Application.Interfaces;
using Application.UseCases.User.Common;
using MediatR;

namespace Application.UseCases.User.GetUser;

public class GetUserHandler(IUserRepository userRepository) : IRequestHandler<GetUserQuery, UserDto?>
{
    private readonly IUserRepository _userRepository = userRepository;

    public async Task<UserDto?> Handle(GetUserQuery request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByEmailAsync(request.Email);
        return user != null ? UserDto.FromEntity(user) : null;
    }
}
