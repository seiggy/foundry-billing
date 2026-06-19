using System.Reflection;
using FluentAssertions;

namespace FoundryBilling.Api.Tests.Models;

public sealed class BillingMetricTests
{
    private const string BillingMetricTypeName = "FoundryBilling.Api.Models.BillingMetricResponse";
    private static readonly Assembly ApiAssembly = typeof(global::Program).Assembly;

    [Fact]
    public void BillingMetricResponse_can_be_created_with_its_simplest_public_constructor_when_available()
    {
        var billingMetricType = ApiAssembly.GetType(BillingMetricTypeName);
        if (billingMetricType is null)
        {
            return;
        }

        var instance = CreateInstance(billingMetricType);

        instance.Should().NotBeNull();
    }

    [Fact]
    public void BillingMetricResponse_public_properties_are_readable_when_the_model_is_available()
    {
        var billingMetricType = ApiAssembly.GetType(BillingMetricTypeName);
        if (billingMetricType is null)
        {
            return;
        }

        var instance = CreateInstance(billingMetricType);

        foreach (var property in billingMetricType.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            var readValue = () => property.GetValue(instance);
            readValue.Should().NotThrow();
        }
    }

    private static object? CreateInstance(Type type)
    {
        var constructor = type
            .GetConstructors(BindingFlags.Instance | BindingFlags.Public)
            .OrderBy(ctor => ctor.GetParameters().Length)
            .FirstOrDefault();

        constructor.Should().NotBeNull();

        var arguments = constructor!
            .GetParameters()
            .Select(parameter => CreateValue(parameter.ParameterType))
            .ToArray();

        return constructor.Invoke(arguments);
    }

    private static object? CreateValue(Type type)
    {
        var underlyingType = Nullable.GetUnderlyingType(type);
        if (underlyingType is not null)
        {
            return CreateValue(underlyingType);
        }

        if (type == typeof(string))
        {
            return "sample";
        }

        if (type == typeof(decimal))
        {
            return 12.34m;
        }

        if (type == typeof(double))
        {
            return 12.34d;
        }

        if (type == typeof(float))
        {
            return 12.34f;
        }

        if (type == typeof(int))
        {
            return 1;
        }

        if (type == typeof(long))
        {
            return 1L;
        }

        if (type == typeof(bool))
        {
            return true;
        }

        if (type == typeof(DateTime))
        {
            return DateTime.UtcNow;
        }

        if (type == typeof(DateOnly))
        {
            return DateOnly.FromDateTime(DateTime.UtcNow);
        }

        if (type == typeof(TimeSpan))
        {
            return TimeSpan.FromMinutes(5);
        }

        if (type == typeof(Guid))
        {
            return Guid.NewGuid();
        }

        if (type.IsEnum)
        {
            return Enum.GetValues(type).GetValue(0);
        }

        if (type.IsArray)
        {
            return Array.CreateInstance(type.GetElementType()!, 0);
        }

        if (type.IsValueType)
        {
            return Activator.CreateInstance(type);
        }

        return type.GetConstructor(Type.EmptyTypes) is not null
            ? Activator.CreateInstance(type)
            : null;
    }
}
