using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;
using UaeDocumentIntelligence.Api.Security;
using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Api.Tests;

public sealed class AuditOperationCoverageTests
{
    [Fact]
    public async Task Every_authorized_operation_has_a_distinct_safe_admission_name_and_reads_remain_unaudited()
    {
        var sink = new Capture();
        var audit = new SecurityAuditContext();
        audit.Bind(Guid.NewGuid(), Guid.NewGuid(), "Human", "operation-coverage");
        using var services = new ServiceCollection()
            .AddLogging().AddOptions()
            .AddSingleton(new EnterpriseSecurityOptions { Mode = "Entra" })
            .AddSingleton(audit).AddSingleton<ISecurityAuditSink>(sink).BuildServiceProvider();
        var reasons = new HashSet<string>();
        foreach (var (route, permission) in EnterprisePermissions.Routes)
        {
            sink.Events.Clear();
            var parts = route.Split(' ', 2);
            var http = new DefaultHttpContext { RequestServices = services, TraceIdentifier = "operation-coverage" };
            http.Request.Method = parts[0];
            http.Request.Path = "/private-client-path";
            http.Request.QueryString = new QueryString("?grant=private-grant");
            http.Request.Body = new UnreadableBody();
            http.Response.Body = new MemoryStream();
            var pattern = RoutePatternFactory.Parse(parts[1]);
            http.SetEndpoint(new RouteEndpoint(_ => Task.CompletedTask, pattern, 0, EndpointMetadataCollection.Empty, null));
            foreach (var parameter in pattern.Parameters)
                http.Request.RouteValues[parameter.Name] = "private-route-value";
            var reached = false;
            await SecurityAuditAdmission.InvokeAsync(http, _ =>
            {
                reached = true;
                return Task.CompletedTask;
            });
            Assert.True(reached, $"Missing audit classification: {route}");
            if (parts[0] == "GET" && permission != "SourceRead")
            {
                Assert.Empty(sink.Events);
                continue;
            }
            var item = Assert.Single(sink.Events);
            Assert.Equal("request.admitted", item.Action);
            Assert.Equal(audit.OrganizationId, item.OrganizationId);
            Assert.Equal(audit.ActorId, item.ActorId);
            Assert.Null(item.TargetId);
            Assert.StartsWith("authorized_", item.ReasonCode);
            Assert.DoesNotContain(item.ReasonCode, new[] { "authorized_get", "authorized_post", "authorized_put" });
            Assert.True(reasons.Add(item.ReasonCode), $"Ambiguous audit operation: {route}");
            Assert.DoesNotContain("private-", JsonSerializer.Serialize(item));
        }
        Assert.NotEmpty(reasons);
    }

    private sealed class Capture : ISecurityAuditSink
    {
        public List<SecurityAuditEvent> Events { get; } = [];
        public Task WriteAsync(SecurityAuditEvent item, CancellationToken ct)
        {
            Events.Add(item);
            return Task.CompletedTask;
        }
    }

    private sealed class UnreadableBody : MemoryStream
    {
        public override int Read(byte[] buffer, int offset, int count) => throw new InvalidOperationException("Body was read.");
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default) =>
            throw new InvalidOperationException("Body was read.");
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct) =>
            throw new InvalidOperationException("Body was read.");
    }
}
