namespace DidarCrm.Domain.Common;

public sealed class DomainValidationException : Exception
{
    public DomainValidationException(string message, string? parameterName = null)
        : base(message)
    {
        ParameterName = parameterName;
    }

    public string? ParameterName { get; }
}
