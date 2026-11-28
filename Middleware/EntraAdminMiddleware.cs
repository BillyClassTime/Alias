namespace aliasbillyclasstime.Middleware;

public class EntraAdminMiddleware
{
    private readonly RequestDelegate _next;

    public EntraAdminMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/admin"))
        {
            var principal =
                context.Request.Headers["X-MS-CLIENT-PRINCIPAL"]
                    .FirstOrDefault();

            if (string.IsNullOrWhiteSpace(principal))
            {
                var returnUrl = Uri.EscapeDataString(
                    context.Request.Path +
                    context.Request.QueryString);

                context.Response.Redirect(
                    $"/.auth/login/aad?post_login_redirect_uri={returnUrl}");

                return;
            }
        }

        await _next(context);
    }
}