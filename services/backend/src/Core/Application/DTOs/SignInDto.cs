using Domain.Enums;

namespace Application.DTOs;

public record SignInDto(string Email, string Password, SignUpMethod SignUpMethod);