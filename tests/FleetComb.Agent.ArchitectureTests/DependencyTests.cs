using System.Reflection;
using FleetComb.Agent.Application;
using FleetComb.Agent.Domain;
using FleetComb.Agent.Infrastructure.Cloud;
using FleetComb.Agent.Infrastructure.Persistence;
using FleetComb.Agent.Infrastructure.Updates;
using FleetComb.Agent.Ui;
using Xunit;

namespace FleetComb.Agent.ArchitectureTests;

public sealed class DependencyTests
{
    [Fact]
    public void DomainHasNoAgentProjectDependencies() =>
        Assert.Empty(AgentReferences(typeof(AgentRegistration).Assembly));

    [Fact]
    public void ApplicationDependsOnlyOnDomain() =>
        Assert.Equal(
            ["FleetComb.Agent.Domain"],
            AgentReferences(typeof(EnrollmentService).Assembly));

    [Theory]
    [InlineData(typeof(AgentCloudClient))]
    [InlineData(typeof(FileAgentRegistrationStore))]
    [InlineData(typeof(StandardReleaseInstaller))]
    public void InfrastructureDependsOnlyInward(Type marker)
    {
        var references = AgentReferences(marker.Assembly);
        Assert.DoesNotContain("FleetComb.Agent.Api", references);
        Assert.DoesNotContain("FleetComb.Agent.Ui", references);
        Assert.All(references, reference => Assert.Contains(
            reference, new[] { "FleetComb.Agent.Application", "FleetComb.Agent.Domain" }));
    }

    [Fact]
    public void UiDoesNotDependOnInfrastructure() =>
        Assert.DoesNotContain(
            AgentReferences(typeof(UiAssemblyMarker).Assembly),
            reference => reference.Contains("Infrastructure", StringComparison.Ordinal));

    private static string[] AgentReferences(Assembly assembly) =>
        assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name!)
            .Where(name => name.StartsWith("FleetComb.Agent", StringComparison.Ordinal))
            .Order()
            .ToArray();
}
