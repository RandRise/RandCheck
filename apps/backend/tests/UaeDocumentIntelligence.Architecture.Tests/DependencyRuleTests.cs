using System.Reflection;
using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Domain.Model;
using UaeDocumentIntelligence.Infrastructure.Persistence;

namespace UaeDocumentIntelligence.Architecture.Tests;

public sealed class DependencyRuleTests
{
    private static readonly Assembly DomainAssembly = typeof(Document).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(IDocumentRepository).Assembly;
    private static readonly Assembly InfrastructureAssembly = typeof(AppDbContext).Assembly;

    [Fact]
    public void Domain_has_no_dependencies_on_outer_layers_or_persistence_frameworks()
    {
        AssertDoesNotReference(DomainAssembly,
            "UaeDocumentIntelligence.Application",
            "UaeDocumentIntelligence.Infrastructure",
            "UaeDocumentIntelligence.Api",
            "Microsoft.EntityFrameworkCore",
            "Npgsql",
            "Microsoft.AspNetCore");
    }

    [Fact]
    public void Application_has_no_dependencies_on_infrastructure_api_or_persistence_frameworks()
    {
        AssertDoesNotReference(ApplicationAssembly,
            "UaeDocumentIntelligence.Infrastructure",
            "UaeDocumentIntelligence.Api",
            "Microsoft.EntityFrameworkCore",
            "Npgsql",
            "Microsoft.AspNetCore");
    }

    [Fact]
    public void Infrastructure_does_not_depend_on_api()
    {
        AssertDoesNotReference(InfrastructureAssembly, "UaeDocumentIntelligence.Api");
    }

    [Fact]
    public void Domain_types_do_not_use_entity_framework_mapping_attributes()
    {
        var violations = DomainAssembly.GetTypes()
            .SelectMany(type => type.GetCustomAttributesData()
                .Concat(type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                    .SelectMany(property => property.GetCustomAttributesData()))
                .Concat(type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                    .SelectMany(field => field.GetCustomAttributesData()))
                .Select(attribute => $"{type.FullName}: {attribute.AttributeType.FullName}"))
            .Where(description => description.Contains("EntityFrameworkCore", StringComparison.Ordinal))
            .ToArray();

        Assert.True(violations.Length == 0,
            $"Domain types must not use Entity Framework attributes:{Environment.NewLine}{string.Join(Environment.NewLine, violations)}");
    }

    private static void AssertDoesNotReference(Assembly assembly, params string[] forbiddenPrefixes)
    {
        var violations = assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .Where(name => forbiddenPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal)))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.True(violations.Length == 0,
            $"{assembly.GetName().Name} has forbidden dependencies: {string.Join(", ", violations)}");
    }
}
