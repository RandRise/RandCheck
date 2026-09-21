using UaeDocumentIntelligence.Application.Abstractions;

namespace UaeDocumentIntelligence.Api;

public static class CapacityResponse
{
    public static IResult Problem(OrganizationCapacityException error) => Results.Problem(
        statusCode: error.Reason == CapacityFailure.Unconfigured ? 503 : 409,
        title: error.Reason switch
        {
            CapacityFailure.Queue => "organization_queue_capacity_exceeded",
            CapacityFailure.Storage => "organization_storage_capacity_exceeded",
            _ => "organization_capacity_unconfigured"
        },
        detail: error.Reason switch
        {
            CapacityFailure.Queue => "The organization has reached its active processing capacity. Try again after work finishes.",
            CapacityFailure.Storage => "The organization has reached its source storage capacity. Remove eligible documents or contact the service administrator.",
            _ => "Organization capacity must be configured by the service administrator."
        });
}
