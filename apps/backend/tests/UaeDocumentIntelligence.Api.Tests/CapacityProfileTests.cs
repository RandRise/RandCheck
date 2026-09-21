using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using UaeDocumentIntelligence.Infrastructure.Persistence;

namespace UaeDocumentIntelligence.Api.Tests;

public sealed partial class EnterpriseSecurityTests
{
    [Theory]
    [InlineData("Production", "false", true)]
    [InlineData("Staging", "false", true)]
    [InlineData("Development", "false", false)]
    [InlineData("Testing", "true", true)]
    public void Enterprise_profiles_require_capacity_policy_while_local_profiles_may_omit_it(string environment, string configured, bool expected)
    {
        using var host = Host(mode: "Locked", environment: environment).WithWebHostBuilder(b => b.UseSetting("Capacity:RequirePolicy", configured));
        Assert.Equal(expected, host.Services.GetRequiredService<IOptions<CapacityOptions>>().Value.RequirePolicy);
    }
}
