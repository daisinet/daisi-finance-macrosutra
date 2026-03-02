using Daisi.SDK.Clients.V1.Orc;
using Daisi.SDK.Models;

namespace MacroSutra.Web.Services;

/// <summary>
/// Validates the x-daisi-client-key header (user's SSO clientKey) for API requests.
/// In Development mode, requests are allowed through without validation.
/// The validated clientKey is the user's own key, so any downstream inference calls bill the user.
/// </summary>
public class ApiKeyAuthFilter(AuthClientFactory authClientFactory, IWebHostEnvironment env) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;

        if (env.IsDevelopment())
        {
            httpContext.Items["accountId"] = httpContext.Request.Headers["x-account-id"].FirstOrDefault() ?? "dev";
            return await next(context);
        }

        var clientKey = httpContext.Request.Headers["x-daisi-client-key"].FirstOrDefault();

        if (string.IsNullOrEmpty(clientKey))
            return Results.Unauthorized();

        try
        {
            var client = authClientFactory.Create();
            var response = client.ValidateClientKey(new Daisi.Protos.V1.ValidateClientKeyRequest
            {
                SecretKey = DaisiStaticSettings.SecretKey ?? "",
                ClientKey = clientKey
            });

            if (response?.IsValid != true)
                return Results.Unauthorized();

            httpContext.Items["accountId"] = response.UserAccountId;
            if (response.HasUserId)
                httpContext.Items["userId"] = response.UserId;
            if (response.HasUserName)
                httpContext.Items["userName"] = response.UserName;

            return await next(context);
        }
        catch
        {
            return Results.Unauthorized();
        }
    }
}
