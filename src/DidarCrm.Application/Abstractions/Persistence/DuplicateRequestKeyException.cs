namespace DidarCrm.Application.Abstractions.Persistence;

public sealed class DuplicateRequestKeyException : Exception
{
    public DuplicateRequestKeyException(Exception? innerException = null)
        : base("The request key already exists for this tenant.", innerException)
    {
    }
}
