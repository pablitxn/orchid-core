namespace WebApi.Configuration;

public class CreditSystemConfiguration
{
    public int TokensPerCredit { get; set; } = 100;
    public int MinimumCreditsPerMessage { get; set; } = 1;
}