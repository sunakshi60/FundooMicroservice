using System.Text.Json;
//using System.Threading.RateLimiting;
//using Microsoft.AspNetCore.RateLimiting;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Load Ocelot configuration
builder.Configuration.AddJsonFile(
    "ocelot.json",
    optional: false,
    reloadOnChange: true);

// Register Ocelot
builder.Services.AddOcelot(builder.Configuration);

//// ASP.NET Core built-in rate limiting
//builder.Services.AddRateLimiter(options =>
//{
//    options.GlobalLimiter =
//        PartitionedRateLimiter.Create<HttpContext, string>(context =>
//        {
//            // 1. Get request path
//            string path =
//                context.Request.Path.Value?.ToLowerInvariant() ?? "";

//            // 2. Get client IP
//            string clientIp =
//                context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

//            // 3. Identify rate-limit group and its limit
//            string group;
//            int limit;

//            if (path == "/api/auth/login")
//            {
//                group = "login";
//                limit = 5;
//            }
//            else if (path == "/api/auth/register")
//            {
//                group = "register";
//                limit = 3;
//            }
//            else if (path == "/api/auth/forgot-password")
//            {
//                group = "forgot-password";
//                limit = 2;
//            }
//            else
//            {
//                group = "other";
//                limit = 120;
//            }

//            // 4. Create a unique partition for each IP and group
//            string partitionKey = $"{clientIp}:{group}";

//            // 5. Apply a separate fixed-window limiter to each partition
//            return RateLimitPartition.GetFixedWindowLimiter(
//                partitionKey: partitionKey,
//                factory: _ => new FixedWindowRateLimiterOptions
//                {
//                    PermitLimit = limit,
//                    Window = TimeSpan.FromMinutes(1),
//                    QueueLimit = 0,
//                    AutoReplenishment = true
//                });
//        });

    // Response when the rate limit is exceeded
//    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

//    options.OnRejected = async (context, cancellationToken) =>
//    {
//        context.HttpContext.Response.ContentType = "application/json";

//        await context.HttpContext.Response.WriteAsync(
//            JsonSerializer.Serialize(new
//            {
//                message = "Too many requests. Please try again later."
//            }),
//            cancellationToken);
//    };
//});

var app = builder.Build();

//Health endpoint in asp .net core
//app.MapGet("/health", () =>
//{
//    return Results.Json(new
//    {
//        status = "Healthy",
//        service = "Fundoo.ApiGateway",
//        timestampUtc = DateTime.UtcNow
//    });
//});

// Health endpoint in ocelot
app.Use(async (context, next) =>
{
    if (context.Request.Path.Equals(
        "/health",
        StringComparison.OrdinalIgnoreCase))
    {
        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = "application/json";

        await context.Response.WriteAsync(
            JsonSerializer.Serialize(new
            {
                status = "Healthy",
                service = "Fundoo.ApiGateway",
                timestampUtc = DateTime.UtcNow
            }));

        return;
    }

    await next();
});

// Apply ASP.NET Core rate limiter
//app.UseRateLimiter();

// Forward requests through Ocelot
await app.UseOcelot();


app.Run();
