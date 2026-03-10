using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace AdminInterface;

public class AuthorizationMiddleware(RequestDelegate next)
{
    private readonly RequestDelegate _next = next;
    private static readonly HashSet<string> AdminOnlyPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/manage",
        "/import-cmms-mappings"
    };

    public async Task InvokeAsync(HttpContext context)
    {
        // Get the request path
        var path = context.Request.Path.Value?.TrimEnd('/') ?? "/";

        // Check if this is an admin-only route
        if (AdminOnlyPaths.Contains(path))
        {
            var user = context.User;

            // Check if user is authenticated
            if (user.Identity?.IsAuthenticated != true)
            {
                // User is not authenticated at all
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "text/html; charset=utf-8";
                await context.Response.WriteAsync(GetUnauthorizedHtml());
                return;
            }

            // Check if user has Admin role
            if (!user.HasClaim(ClaimTypes.Role, "Admin"))
            {
                // User is authenticated but doesn't have the Admin role
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "text/html; charset=utf-8";
                await context.Response.WriteAsync(GetForbiddenHtml(user));
                return;
            }
        }

        // Continue to the next middleware
        await _next(context);
    }

    public static string GetUnauthorizedHtml()
    {
        return @"
<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>401 Unauthorized</title>
    <style>
        body {
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif;
            display: flex;
            align-items: center;
            justify-content: center;
            min-height: 100vh;
            margin: 0;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
        }
        .container {
            background: white;
            padding: 40px;
            border-radius: 8px;
            box-shadow: 0 10px 25px rgba(0, 0, 0, 0.2);
            max-width: 500px;
            text-align: center;
        }
        h1 {
            color: #d32f2f;
            margin: 0 0 10px 0;
            font-size: 48px;
        }
        h2 {
            color: #333;
            font-size: 20px;
            margin: 0 0 20px 0;
            font-weight: 500;
        }
        p {
            color: #666;
            line-height: 1.6;
            margin: 0 0 20px 0;
        }
        a {
            display: inline-block;
            background: #667eea;
            color: white;
            padding: 10px 20px;
            border-radius: 4px;
            text-decoration: none;
            font-weight: 500;
            transition: background 0.3s;
        }
        a:hover {
            background: #764ba2;
        }
    </style>
</head>
<body>
    <div class=""container"">
        <h1>401</h1>
        <h2>Unauthorized</h2>
        <p>Your account was not found in the Authorization table.</p>
        <a href=""/"">Return to Home</a>
    </div>
</body>
</html>";
    }

    public static string GetForbiddenHtml(ClaimsPrincipal user)
    {
        var userName = user.FindFirst(ClaimTypes.Name)?.Value ?? "User";
        return $@"
<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>403 Forbidden</title>
    <style>
        body {{
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif;
            display: flex;
            align-items: center;
            justify-content: center;
            min-height: 100vh;
            margin: 0;
            background: linear-gradient(135deg, #f093fb 0%, #f5576c 100%);
        }}
        .container {{
            background: white;
            padding: 40px;
            border-radius: 8px;
            box-shadow: 0 10px 25px rgba(0, 0, 0, 0.2);
            max-width: 500px;
            text-align: center;
        }}
        h1 {{
            color: #f5576c;
            margin: 0 0 10px 0;
            font-size: 48px;
        }}
        h2 {{
            color: #333;
            font-size: 20px;
            margin: 0 0 20px 0;
            font-weight: 500;
        }}
        p {{
            color: #666;
            line-height: 1.6;
            margin: 0 0 20px 0;
        }}
        .user-info {{
            background: #f5f5f5;
            padding: 15px;
            border-radius: 4px;
            margin-bottom: 20px;
            font-size: 14px;
            color: #555;
        }}
        a {{
            display: inline-block;
            background: #f5576c;
            color: white;
            padding: 10px 20px;
            border-radius: 4px;
            text-decoration: none;
            font-weight: 500;
            transition: background 0.3s;
        }}
        a:hover {{
            background: #f093fb;
        }}
    </style>
</head>
<body>
    <div class=""container"">
        <h1>403</h1>
        <h2>Forbidden</h2>
        <p>Admin privileges are required for this page.</p>
        <div class=""user-info"">
            Logged in as: <strong>{userName}</strong>
        </div>
        <a href=""/"">Return to Home</a>
    </div>
</body>
</html>";
    }
}

public static class AuthorizationMiddlewareExtensions
{
    public static IApplicationBuilder UseAuthorizationMiddleware(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<AuthorizationMiddleware>();
    }
}
