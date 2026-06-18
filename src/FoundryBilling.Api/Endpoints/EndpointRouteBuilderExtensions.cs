namespace FoundryBilling.Api.Endpoints;

public static class EndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapFoundryBillingEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api");

        api.MapBillingEndpoints();
        api.MapProjectEndpoints();

        return app;
    }
}
