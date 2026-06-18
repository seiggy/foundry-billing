using FluentAssertions;

namespace FoundryBilling.Api.Tests.Services;

public sealed class ServiceContractTests
{
    [Theory]
    [InlineData("FoundryBilling.Api.Services.IBillingService")]
    [InlineData("FoundryBilling.Api.Services.IProjectService")]
    public void Service_contracts_are_interfaces_when_available(string contractTypeName)
    {
        var serviceContractType = typeof(global::Program).Assembly.GetType(contractTypeName);
        if (serviceContractType is null)
        {
            return;
        }

        serviceContractType.IsInterface.Should().BeTrue();
    }
}
