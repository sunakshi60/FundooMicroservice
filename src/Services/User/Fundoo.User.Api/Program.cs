using Asp.Versioning;
using FluentValidation;
using Fundoo.User.Api.Validators;
using Fundoo.User.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

var builder = WebApplication.CreateBuilder(args);

var userConnectionString = builder.Configuration.GetConnectionString("UserDb")
    ?? throw new InvalidOperationException("UserConnnectionstring is missing");

builder.Services.AddHealthChecks()

    .AddCheck(
        "self",
        () => HealthCheckResult.Healthy(),
        tags: new[] { "live" }
        )

    .AddNpgSql(
        userConnectionString,
        name: "postgresql",
        tags: new[] { "ready" },
        timeout: TimeSpan.FromSeconds(3)
    );

// Add services to the container.
builder.Services.AddControllers();

// Register FluentValidation validators from the User API assembly.
builder.Services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>();

//configure API Versioining
builder.Services
    .AddApiVersioning(options =>
    {
        // The default version of the User API.
        options.DefaultApiVersion = new ApiVersion(1, 0);

        // Clients must explicitly request a version.
        options.AssumeDefaultVersionWhenUnspecified = false;

        // Include supported and deprecated versions in response headers.
        options.ReportApiVersions = true;

        // Read the version from the URL path.
        options.ApiVersionReader = new UrlSegmentApiVersionReader();
    })
    .AddMvc()
    .AddApiExplorer(options =>
    {
        // Swagger group names: v1, v2, etc.
        options.GroupNameFormat = "'v'VVV";

        // Replace the version placeholder in the URL with the actual version.
        options.SubstituteApiVersionInUrl = true;
    });


// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();

//Swagger implementation
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Fundoo User API",
        Version = "v1",
        Description = "Version 1 of the Fundoo User Service"
    });

    options.SwaggerDoc("v2", new OpenApiInfo
    {
        Title = "Fundoo User API",
        Version = "v2",
        Description = "Version 2 of the Fundoo User Service"
    });

    options.DocInclusionPredicate((documentName, apiDescription) =>
        string.Equals(
            apiDescription.GroupName,
            documentName,
            StringComparison.OrdinalIgnoreCase));

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter your JWT access token."
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Register the named HTTP client for Notification API.
builder.Services
    .AddHttpClient("NotificationService", client =>
{
    var baseUrl = builder.Configuration["NotificationService:BaseUrl"]
        ?? throw new InvalidOperationException(
            "Notification Service BaseUrl is not configured.");

    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(35);
})
    .AddStandardResilienceHandler(options =>
    {
        // Timeout Settings
        // Maximum time for the entire operation, including retries.
        options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(30);

        // Maximum time allowed for each individual HTTP attempt.
        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(10);

        // Retry configuration.
        options.Retry.MaxRetryAttempts = 2;
        options.Retry.Delay = TimeSpan.FromSeconds(1);
        options.Retry.BackoffType = Polly.DelayBackoffType.Exponential;
        options.Retry.UseJitter = true;

        // Log every retry attempt.
        //options.Retry.OnRetry = args =>
        //{
        //    Console.WriteLine(
        //        $"[HTTP RETRY] Attempt: {args.AttemptNumber}, " +
        //        $"Delay: {args.RetryDelay}, " +
        //        $"Status: {args.Outcome.Result?.StatusCode}, " +
        //        $"Exception: {args.Outcome.Exception?.Message}");

        //    return ValueTask.CompletedTask;
        //};

        // Do not automatically retry POST, PUT, PATCH,
        // DELETE, or CONNECT requests.
        options.Retry.DisableForUnsafeHttpMethods();

        // CONCURRENCY LIMIT / BULKHEAD
        options.RateLimiter.DefaultRateLimiterOptions.PermitLimit = 10;
        options.RateLimiter.DefaultRateLimiterOptions.QueueLimit = 0;

        // Circuit breaker configuration
        options.CircuitBreaker.FailureRatio = 0.5;
        options.CircuitBreaker.MinimumThroughput = 5;
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
        options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(15);
    });



////testing
//builder.Services
//    .AddHttpClient("ResilienceTest", client =>
//    {
//        client.BaseAddress = new Uri("https://localhost:7001/");
//    })
//    .AddStandardResilienceHandler(options =>
//    {
//        options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(10);
//        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(3);

//        options.Retry.MaxRetryAttempts = 2;
//        options.Retry.Delay = TimeSpan.FromSeconds(1);
//        options.Retry.BackoffType = Polly.DelayBackoffType.Exponential;
//        options.Retry.UseJitter = true;

//        options.Retry.OnRetry = args =>
//        {
//            Console.WriteLine(
//                $"[HTTP RETRY] Attempt: {args.AttemptNumber}, " +
//                $"Status: {args.Outcome.Result?.StatusCode}");

//            return ValueTask.CompletedTask;
//        };
//    });

builder.Services.AddUserInfrastructure(builder.Configuration);

builder.Services.AddOpenIddict()
    .AddCore(options =>
    {
        options.UseEntityFrameworkCore()
               .UseDbContext<UserDbContext>();
    });

var signingKey = builder.Configuration["Jwt:SigningKey"]
    ?? throw new InvalidOperationException(
        "JWT signing key is not configured.");

var issuer = builder.Configuration["Jwt:Issuer"]
    ?? throw new InvalidOperationException(
        "JWT issuer is not configured.");

var audience = builder.Configuration["Jwt:Audience"]
    ?? throw new InvalidOperationException(
        "JWT audience is not configured.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = issuer,

            ValidateAudience = true,
            ValidAudience = audience,

            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(signingKey)),

            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    })
    // Temporary cookie for Google authentication
    .AddCookie("External")

    // Google OpenID Connect authentication
    .AddOpenIdConnect("Google", options =>
    {
        options.Authority = "https://accounts.google.com";

        options.ClientId =
            builder.Configuration["Authentication:Google:ClientId"]!;

        options.ClientSecret =
            builder.Configuration["Authentication:Google:ClientSecret"]!;

        options.MapInboundClaims = false;
        options.UsePkce = true;

        options.SignInScheme = "External";

        options.CallbackPath = "/signin-google";

        options.ResponseType = "code";

        options.Scope.Add("email");
        options.Scope.Add("profile");

        options.SaveTokens = false;
    });


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();

    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint(
            "/swagger/v1/swagger.json",
            "Fundoo User API v1");

        options.SwaggerEndpoint(
            "/swagger/v2/swagger.json",
            "Fundoo User API v2");
    });
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

//Health check
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live")
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

app.Run();
