using Fundoo.Notification.Api.Services;
using Fundoo.Notification.Application.Abstractions;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Register controllers.
builder.Services.AddControllers();

// Register Swagger/OpenAPI.
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc(
        "v1",
        new Microsoft.OpenApi.Models.OpenApiInfo
        {
            Title = "Fundoo Notification API",
            Version = "v1",
            Description = "API for sending Fundoo email notifications"
        });
});

// Register Notification Service.
builder.Services.AddTransient<INotificationService, SmtpEmailService>();

// NEW CODE: Configure rate limiting for email endpoints.
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("NotificationEmail", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString()
                          ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));

    // Return a clear response when the limit is exceeded.
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode =
            StatusCodes.Status429TooManyRequests;

        context.HttpContext.Response.Headers.RetryAfter = "60";

        await context.HttpContext.Response.WriteAsJsonAsync(
            new
            {
                isSuccess = false,
                message = "Too many notification requests. Please try again later."
            },
            cancellationToken);
    };
});

var app = builder.Build();

// Configure Swagger.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// NEW CODE: Enable endpoint routing and rate limiting.
app.UseRouting();
app.UseRateLimiter();

app.UseAuthorization();

// Map controller routes.
app.MapControllers();

app.Run();