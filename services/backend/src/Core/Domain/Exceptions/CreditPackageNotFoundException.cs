namespace Domain.Exceptions;

public class CreditPackageNotFoundException : EntityNotFoundException
{
    public CreditPackageNotFoundException(Guid packageId)
        : base("CreditPackage", packageId)
    {
        PackageId = packageId;
    }

    public Guid PackageId { get; }
}
