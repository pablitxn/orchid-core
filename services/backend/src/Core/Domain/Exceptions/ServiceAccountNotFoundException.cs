namespace Domain.Exceptions;

public class ServiceAccountNotFoundException : EntityNotFoundException
{
    public ServiceAccountNotFoundException(string clientId)
        : base("ServiceAccount", clientId)
    {
        ClientId = clientId;
    }

    public string ClientId { get; }
}
