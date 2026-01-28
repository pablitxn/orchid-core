using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Application.UseCases.User.Common;
using Domain.Entities;
using FluentAssertions;
using Infrastructure.Auth;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Tests.Auth;

public class JwtTokenServiceTests
{
    private readonly JwtTokenService _service;
    private readonly IConfiguration _configuration;

    public JwtTokenServiceTests()
    {
        var configValues = new Dictionary<string, string?>
        {
            { "Jwt:Key", "this-is-a-super-secret-key-for-testing-purposes-only-32-chars" },
            { "Jwt:Issuer", "test-issuer" },
            { "Jwt:Audience", "test-audience" },
            { "Jwt:ExpireHours", "24" }
        };

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();

        _service = new JwtTokenService(_configuration);
    }

    [Fact]
    public void Constructor_Should_ThrowException_When_JwtKeyNotConfigured()
    {
        // Arrange
        var configValues = new Dictionary<string, string?>
        {
            { "Jwt:Issuer", "test-issuer" },
            { "Jwt:Audience", "test-audience" }
        };

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();

        // Act
        var act = () => new JwtTokenService(config);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Jwt:Key*");
    }

    [Fact]
    public void Constructor_Should_ThrowException_When_IssuerNotConfigured()
    {
        // Arrange
        var configValues = new Dictionary<string, string?>
        {
            { "Jwt:Key", "this-is-a-super-secret-key-for-testing-purposes-only" },
            { "Jwt:Audience", "test-audience" }
        };

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();

        // Act
        var act = () => new JwtTokenService(config);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Jwt:Issuer*");
    }

    [Fact]
    public void Constructor_Should_ThrowException_When_AudienceNotConfigured()
    {
        // Arrange
        var configValues = new Dictionary<string, string?>
        {
            { "Jwt:Key", "this-is-a-super-secret-key-for-testing-purposes-only" },
            { "Jwt:Issuer", "test-issuer" }
        };

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();

        // Act
        var act = () => new JwtTokenService(config);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Jwt:Audience*");
    }

    [Fact]
    public void Constructor_Should_UseDefaultExpireHours_When_NotConfigured()
    {
        // Arrange
        var configValues = new Dictionary<string, string?>
        {
            { "Jwt:Key", "this-is-a-super-secret-key-for-testing-purposes-only-32-chars" },
            { "Jwt:Issuer", "test-issuer" },
            { "Jwt:Audience", "test-audience" }
        };

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();

        // Act - Should not throw
        var service = new JwtTokenService(config);
        var user = new UserEntity { Id = Guid.NewGuid(), Email = "test@example.com" };
        var token = service.GenerateToken(user, new[] { "User" });

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);
        var expiry = jwtToken.ValidTo;

        // Should expire in approximately 24 hours (default)
        expiry.Should().BeCloseTo(DateTime.UtcNow.AddHours(24), TimeSpan.FromMinutes(5));
    }

    [Fact]
    public void GenerateToken_WithUserEntity_Should_ReturnValidJwtToken()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new UserEntity
        {
            Id = userId,
            Email = "test@example.com"
        };
        var roles = new[] { "User", "Admin" };

        // Act
        var token = _service.GenerateToken(user, roles);

        // Assert
        token.Should().NotBeNullOrEmpty();

        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        jwtToken.Issuer.Should().Be("test-issuer");
        jwtToken.Audiences.Should().Contain("test-audience");
    }

    [Fact]
    public void GenerateToken_WithUserEntity_Should_IncludeUserIdClaim()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new UserEntity
        {
            Id = userId,
            Email = "test@example.com"
        };

        // Act
        var token = _service.GenerateToken(user, new[] { "User" });

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        var userIdClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
        userIdClaim.Should().NotBeNull();
        userIdClaim!.Value.Should().Be(userId.ToString());
    }

    [Fact]
    public void GenerateToken_WithUserEntity_Should_IncludeEmailClaim()
    {
        // Arrange
        var user = new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com"
        };

        // Act
        var token = _service.GenerateToken(user, new[] { "User" });

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        var emailClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email);
        emailClaim.Should().NotBeNull();
        emailClaim!.Value.Should().Be("test@example.com");
    }

    [Fact]
    public void GenerateToken_WithUserEntity_Should_IncludeRoleClaims()
    {
        // Arrange
        var user = new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com"
        };
        var roles = new[] { "User", "Admin", "Moderator" };

        // Act
        var token = _service.GenerateToken(user, roles);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        var roleClaims = jwtToken.Claims.Where(c => c.Type == ClaimTypes.Role).ToList();
        roleClaims.Should().HaveCount(3);
        roleClaims.Select(c => c.Value).Should().BeEquivalentTo(roles);
    }

    [Fact]
    public void GenerateToken_WithUserEntity_Should_IncludeJtiClaim()
    {
        // Arrange
        var user = new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com"
        };

        // Act
        var token = _service.GenerateToken(user, new[] { "User" });

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        var jtiClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti);
        jtiClaim.Should().NotBeNull();
        Guid.TryParse(jtiClaim!.Value, out _).Should().BeTrue();
    }

    [Fact]
    public void GenerateToken_WithUserEntity_Should_SetCorrectExpiration()
    {
        // Arrange
        var user = new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com"
        };

        // Act
        var token = _service.GenerateToken(user, new[] { "User" });

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        jwtToken.ValidTo.Should().BeCloseTo(DateTime.UtcNow.AddHours(24), TimeSpan.FromMinutes(5));
    }

    [Fact]
    public void GenerateToken_WithUserDto_Should_ReturnValidJwtToken()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new UserDto
        {
            Id = userId,
            Email = "dto@example.com"
        };
        var roles = new[] { "User" };

        // Act
        var token = _service.GenerateToken(user, roles);

        // Assert
        token.Should().NotBeNullOrEmpty();

        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        var userIdClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
        userIdClaim.Should().NotBeNull();
        userIdClaim!.Value.Should().Be(userId.ToString());

        var emailClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email);
        emailClaim.Should().NotBeNull();
        emailClaim!.Value.Should().Be("dto@example.com");
    }

    [Fact]
    public void GenerateToken_WithUserDto_Should_IncludeAllRoles()
    {
        // Arrange
        var user = new UserDto
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com"
        };
        var roles = new[] { "Admin", "SuperUser" };

        // Act
        var token = _service.GenerateToken(user, roles);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        var roleClaims = jwtToken.Claims.Where(c => c.Type == ClaimTypes.Role).ToList();
        roleClaims.Should().HaveCount(2);
        roleClaims.Select(c => c.Value).Should().BeEquivalentTo(roles);
    }

    [Fact]
    public void GenerateServiceAccountToken_Should_ReturnValidJwtToken()
    {
        // Arrange
        var serviceAccount = new ServiceAccountEntity
        {
            Id = Guid.NewGuid(),
            ClientId = "test-client-id",
            Name = "Test Service",
            Scopes = new[] { "read", "write" }
        };

        // Act
        var token = _service.GenerateServiceAccountToken(serviceAccount);

        // Assert
        token.Should().NotBeNullOrEmpty();

        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        jwtToken.Issuer.Should().Be("test-issuer");
        jwtToken.Audiences.Should().Contain("test-audience");
    }

    [Fact]
    public void GenerateServiceAccountToken_Should_IncludeServiceAccountSubject()
    {
        // Arrange
        var serviceAccount = new ServiceAccountEntity
        {
            Id = Guid.NewGuid(),
            ClientId = "my-service-client",
            Name = "Test Service",
            Scopes = new[] { "read" }
        };

        // Act
        var token = _service.GenerateServiceAccountToken(serviceAccount);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        var subClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub);
        subClaim.Should().NotBeNull();
        subClaim!.Value.Should().Be("service:my-service-client");
    }

    [Fact]
    public void GenerateServiceAccountToken_Should_IncludeTypeClaim()
    {
        // Arrange
        var serviceAccount = new ServiceAccountEntity
        {
            Id = Guid.NewGuid(),
            ClientId = "test-client",
            Name = "Test Service",
            Scopes = new[] { "read" }
        };

        // Act
        var token = _service.GenerateServiceAccountToken(serviceAccount);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        var typeClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "type");
        typeClaim.Should().NotBeNull();
        typeClaim!.Value.Should().Be("service_account");
    }

    [Fact]
    public void GenerateServiceAccountToken_Should_IncludeClientIdClaim()
    {
        // Arrange
        var serviceAccount = new ServiceAccountEntity
        {
            Id = Guid.NewGuid(),
            ClientId = "unique-client-id",
            Name = "Test Service",
            Scopes = new[] { "read" }
        };

        // Act
        var token = _service.GenerateServiceAccountToken(serviceAccount);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        var clientIdClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "client_id");
        clientIdClaim.Should().NotBeNull();
        clientIdClaim!.Value.Should().Be("unique-client-id");
    }

    [Fact]
    public void GenerateServiceAccountToken_Should_IncludeScopeClaims()
    {
        // Arrange
        var serviceAccount = new ServiceAccountEntity
        {
            Id = Guid.NewGuid(),
            ClientId = "test-client",
            Name = "Test Service",
            Scopes = new[] { "read", "write", "delete" }
        };

        // Act
        var token = _service.GenerateServiceAccountToken(serviceAccount);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        var scopeClaims = jwtToken.Claims.Where(c => c.Type == "scope").ToList();
        scopeClaims.Should().HaveCount(3);
        scopeClaims.Select(c => c.Value).Should().BeEquivalentTo(new[] { "read", "write", "delete" });
    }

    [Fact]
    public void GenerateServiceAccountToken_Should_IncludeScopesJsonClaim()
    {
        // Arrange
        var serviceAccount = new ServiceAccountEntity
        {
            Id = Guid.NewGuid(),
            ClientId = "test-client",
            Name = "Test Service",
            Scopes = new[] { "read", "write" }
        };

        // Act
        var token = _service.GenerateServiceAccountToken(serviceAccount);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        var scopesClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "scopes");
        scopesClaim.Should().NotBeNull();
        scopesClaim!.Value.Should().Contain("read").And.Contain("write");
    }

    [Fact]
    public void GenerateServiceAccountToken_Should_ExpireInOneHour()
    {
        // Arrange
        var serviceAccount = new ServiceAccountEntity
        {
            Id = Guid.NewGuid(),
            ClientId = "test-client",
            Name = "Test Service",
            Scopes = new[] { "read" }
        };

        // Act
        var token = _service.GenerateServiceAccountToken(serviceAccount);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        jwtToken.ValidTo.Should().BeCloseTo(DateTime.UtcNow.AddHours(1), TimeSpan.FromMinutes(5));
    }

    [Fact]
    public void GenerateServiceAccountToken_Should_IncludeJtiClaim()
    {
        // Arrange
        var serviceAccount = new ServiceAccountEntity
        {
            Id = Guid.NewGuid(),
            ClientId = "test-client",
            Name = "Test Service",
            Scopes = new[] { "read" }
        };

        // Act
        var token = _service.GenerateServiceAccountToken(serviceAccount);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        var jtiClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti);
        jtiClaim.Should().NotBeNull();
        Guid.TryParse(jtiClaim!.Value, out _).Should().BeTrue();
    }

    [Fact]
    public void GenerateToken_Should_GenerateUniqueJtiForEachToken()
    {
        // Arrange
        var user = new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com"
        };

        // Act
        var token1 = _service.GenerateToken(user, new[] { "User" });
        var token2 = _service.GenerateToken(user, new[] { "User" });

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jti1 = handler.ReadJwtToken(token1).Claims.First(c => c.Type == JwtRegisteredClaimNames.Jti).Value;
        var jti2 = handler.ReadJwtToken(token2).Claims.First(c => c.Type == JwtRegisteredClaimNames.Jti).Value;

        jti1.Should().NotBe(jti2);
    }

    [Fact]
    public void GenerateToken_WithEmptyRoles_Should_NotIncludeRoleClaims()
    {
        // Arrange
        var user = new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com"
        };

        // Act
        var token = _service.GenerateToken(user, Enumerable.Empty<string>());

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        var roleClaims = jwtToken.Claims.Where(c => c.Type == ClaimTypes.Role).ToList();
        roleClaims.Should().BeEmpty();
    }
}
