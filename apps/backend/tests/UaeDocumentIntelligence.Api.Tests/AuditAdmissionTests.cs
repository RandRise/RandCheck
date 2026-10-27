using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Api.Tests;

public sealed partial class EnterpriseSecurityTests
{
    private sealed class AdmissionAudit : ISecurityAuditSink
    {
        public ConcurrentQueue<SecurityAuditEvent> Events { get; } = new();
        public Task WriteAsync(SecurityAuditEvent item, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            Events.Enqueue(item);
            return Task.CompletedTask;
        }
    }

    [Theory]
    [InlineData("{", "application/json", 400)]
    [InlineData("null", "application/json", 400)]
    public async Task Admitted_mutation_is_audited_before_binding_failure(string body, string mediaType, int status)
    {
        var store = new ActorStore { Actor = new(Guid.NewGuid(), Guid.NewGuid(), OrganizationAccessRole.Admin) };
        var audit = new AdmissionAudit();
        using var host = Host(store).WithWebHostBuilder(b => b.ConfigureServices(s =>
            s.Replace(ServiceDescriptor.Singleton<ISecurityAuditSink>(audit))));
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", Token());
        client.DefaultRequestHeaders.Add("X-Request-ID", "admission-binding");
        client.DefaultRequestHeaders.Add("X-Organization-ID", Guid.NewGuid().ToString());
        using var response = await client.PostAsync("/api/organization/memberships?private-query=secret",
            new StringContent(body, Encoding.UTF8, mediaType));
        Assert.Equal(status, (int)response.StatusCode);
        var item = Assert.Single(audit.Events);
        Assert.Equal("request.admitted", item.Action);
        Assert.Equal("Succeeded", item.Outcome);
        Assert.Equal("authorized_membership-grant", item.ReasonCode);
        Assert.Equal(store.Actor!.OrganizationId, item.OrganizationId);
        Assert.Equal(store.Actor.ActorId, item.ActorId);
        Assert.Equal("admission-binding", item.RequestId);
        Assert.Equal("organization-access", item.TargetType);
        Assert.Null(item.BeforeState); Assert.Null(item.AfterState);
        var serialized = JsonSerializer.Serialize(item);
        Assert.DoesNotContain("secret", serialized);
        Assert.DoesNotContain("private-body", serialized);
        Assert.DoesNotContain(Token(), serialized);
    }

    [Fact]
    public async Task Admission_audit_outage_stops_before_request_body_is_read()
    {
        using var host = Host().WithWebHostBuilder(b => b.ConfigureServices(s =>
            s.Replace(ServiceDescriptor.Singleton<ISecurityAuditSink, FailingAudit>())));
        var body = new NeverReadBody();
        var response = await host.Server.SendAsync(context =>
        {
            context.Request.Method = "POST";
            context.Request.Path = "/api/cited-questions/reviewed";
            context.Request.Headers.Authorization = "Bearer " + Token();
            context.Request.ContentType = "application/json";
            context.Request.Body = body;
        });
        Assert.Equal(503, response.Response.StatusCode);
        Assert.Equal(0, body.Reads);
    }

    [Fact]
    public async Task Router_rejection_is_denied_without_an_admission_event()
    {
        var store = new ActorStore { Actor = new(Guid.NewGuid(), Guid.NewGuid(), OrganizationAccessRole.Admin) };
        var audit = new AdmissionAudit();
        using var host = Host(store).WithWebHostBuilder(b => b.ConfigureServices(s =>
            s.Replace(ServiceDescriptor.Singleton<ISecurityAuditSink>(audit))));
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", Token());
        using var response = await client.PostAsync("/api/organization/memberships",
            new StringContent("private-body", Encoding.UTF8, "text/plain"));
        // Unsupported media selects a framework rejection endpoint, which is not
        // an allowlisted business route. The existing authorization gate denies it.
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("access.denied", Assert.Single(audit.Events).Action);
    }

    [Fact]
    public async Task Denied_and_throttled_requests_do_not_create_admission_events()
    {
        var audit = new AdmissionAudit();
        using var host = Host().WithWebHostBuilder(b =>
        {
            b.UseSetting("RequestLimits:Enabled", "true").UseSetting("RequestLimits:ActorWorkRequests", "1");
            b.ConfigureServices(s => s.Replace(ServiceDescriptor.Singleton<ISecurityAuditSink>(audit)));
        });
        using var client = host.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/cited-questions/reviewed", null)).StatusCode);
        Assert.Equal("access.denied", Assert.Single(audit.Events).Action);
        audit.Events.Clear();
        client.DefaultRequestHeaders.Authorization = new("Bearer", Token());
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/api/organization/memberships", null)).StatusCode);
        Assert.Equal("access.denied", Assert.Single(audit.Events).Action);
        audit.Events.Clear();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/cited-questions/reviewed",
            new StringContent("{", Encoding.UTF8, "application/json"))).StatusCode);
        Assert.Single(audit.Events, e => e.Action == "request.admitted");
        audit.Events.Clear();
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.PostAsync("/api/cited-questions/reviewed", null)).StatusCode);
        Assert.Empty(audit.Events);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/documents")).StatusCode);
        Assert.Empty(audit.Events);
    }

    [Fact]
    public async Task Source_read_attempt_records_only_server_route_and_document_id()
    {
        var audit = new AdmissionAudit();
        using var host = Host().WithWebHostBuilder(b => b.ConfigureServices(s =>
            s.Replace(ServiceDescriptor.Singleton<ISecurityAuditSink>(audit))));
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", Token());
        var document = Guid.NewGuid();
        using var response = await client.GetAsync($"/api/documents/{document}/source?grant=private-grant");
        Assert.False(response.IsSuccessStatusCode);
        var item = Assert.Single(audit.Events, e => e.Action == "request.admitted");
        Assert.Equal(document, item.TargetId);
        Assert.Equal("authorized_source-download", item.ReasonCode);
        Assert.DoesNotContain("private-grant", JsonSerializer.Serialize(audit.Events));
    }
}
