namespace DidarCrm.Application.Abstractions.Persistence;

public sealed class PersistenceConcurrencyException(Exception innerException)
    : Exception("A concurrent persistence update was detected.", innerException);
