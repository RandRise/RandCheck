namespace UaeDocumentIntelligence.Application.Abstractions;

public enum CapacityFailure { Queue, Storage, Unconfigured }
public sealed class OrganizationCapacityException(CapacityFailure reason) : Exception("Organization capacity is unavailable.")
{
    public CapacityFailure Reason { get; } = reason;
}
