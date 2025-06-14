using Application.DTOs;

namespace Infrastructure.Auth;

public class GoogleAuthenticationService
{
    public static Task<TokenDto> AuthenticateWithGoogleAsync(string accessToken, string idToken)
    {
        TokenDto res = new($"{accessToken} ${idToken}", DateTime.Now);

        return Task.FromResult(res);
    }
}