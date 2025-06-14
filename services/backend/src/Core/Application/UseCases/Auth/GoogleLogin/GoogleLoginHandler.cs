using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;

namespace Application.UseCases.Auth.GoogleLogin;

public class GoogleLoginHandler(IHttpContextAccessor httpContextAccessor)
    : IRequestHandler<GoogleLoginCommand, GoogleLoginResult>
{
    public async Task<GoogleLoginResult> Handle(GoogleLoginCommand request, CancellationToken cancellationToken)
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is null)
            return new GoogleLoginResult(false, string.Empty, "HttpContext not available.");

        var result = await httpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        if (!result.Succeeded)
            return new GoogleLoginResult(false, string.Empty, "Google authentication failed.");

        var email = result.Principal.FindFirst(ClaimTypes.Email)?.Value;
        if (string.IsNullOrEmpty(email))
            return new GoogleLoginResult(false, string.Empty, "Email claim missing.");

        // Example token generation (this should be replaced by your actual token generation logic)
        var token = Guid.NewGuid().ToString();

        return new GoogleLoginResult(true, token, string.Empty);
    }
}