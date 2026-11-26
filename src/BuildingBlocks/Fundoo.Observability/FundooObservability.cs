using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace Fundoo.Observability;

public static class FundooObservability
{
    public const string MessagingActivitySourceName = "Fundoo.Messaging";

    private static readonly ActivitySource MessagingActivitySource =
        new(MessagingActivitySourceName, "1.0.0");

    public static IServiceCollection AddFundooObservability(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment,
        string serviceName)
    {
        if (!configuration.GetValue("OpenTelemetry:Enabled", true))
        {
            return services;
        }

        var endpoint = configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]
            ?? configuration["OpenTelemetry:Endpoint"]
            ?? "http://localhost:4317";
        var endpointUri = new Uri(endpoint, UriKind.Absolute);
        var protocol = string.Equals(
            configuration["OTEL_EXPORTER_OTLP_PROTOCOL"],
            "http/protobuf",
            StringComparison.OrdinalIgnoreCase)
                ? OtlpExportProtocol.HttpProtobuf
                : OtlpExportProtocol.Grpc;

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(serviceName)
                .AddAttributes(new Dictionary<string, object>
                {
                    ["deployment.environment.name"] = environment.EnvironmentName
                }))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation(options => options.RecordException = false)
                .AddHttpClientInstrumentation(options => options.RecordException = false)
                .AddNpgsql()
                .AddSource(MessagingActivitySourceName)
                .AddProcessor(new SensitiveTelemetryProcessor())
                .AddOtlpExporter(options =>
                {
                    options.Endpoint = endpointUri;
                    options.Protocol = protocol;
                }))
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddMeter("Npgsql")
                .AddOtlpExporter(options =>
                {
                    options.Endpoint = endpointUri;
                    options.Protocol = protocol;
                }));

        return services;
    }

    public static Activity? StartKafkaPublish(string topic)
    {
        var activity = MessagingActivitySource.StartActivity(
            "kafka publish",
            ActivityKind.Producer);
        activity?.SetTag("messaging.system", "kafka");
        activity?.SetTag("messaging.destination.name", topic);
        activity?.SetTag("messaging.operation.type", "publish");
        return activity;
    }

    public static Activity? StartKafkaProcess(string topic, ActivityContext parentContext)
    {
        var activity = MessagingActivitySource.StartActivity(
            "kafka process",
            ActivityKind.Consumer,
            parentContext);
        activity?.SetTag("messaging.system", "kafka");
        activity?.SetTag("messaging.destination.name", topic);
        activity?.SetTag("messaging.operation.type", "process");
        return activity;
    }

    public static Activity? StartSmtpSend()
    {
        var activity = MessagingActivitySource.StartActivity(
            "smtp send",
            ActivityKind.Client);
        activity?.SetTag("peer.service", "smtp");
        return activity;
    }

    public static Activity? StartPasswordVerify()
    {
        return MessagingActivitySource.StartActivity(
            "password verify",
            ActivityKind.Internal);
    }

    public static Activity? StartUserLookup()
    {
        return MessagingActivitySource.StartActivity(
            "user lookup",
            ActivityKind.Internal);
    }

    public static void InjectTraceContext<TCarrier>(
        Activity? activity,
        TCarrier carrier,
        Action<TCarrier, string, string> setter)
    {
        var context = activity?.Context ?? Activity.Current?.Context ?? default;
        Propagators.DefaultTextMapPropagator.Inject(
            new PropagationContext(context, Baggage.Current),
            carrier,
            setter);
    }

    public static ActivityContext ExtractTraceContext<TCarrier>(
        TCarrier carrier,
        Func<TCarrier, string, IEnumerable<string>?> getter)
    {
        return Propagators.DefaultTextMapPropagator
            .Extract(default, carrier, getter)
            .ActivityContext;
    }

    public static void RecordFailure(Activity? activity, Exception exception)
    {
        activity?.SetStatus(ActivityStatusCode.Error);
        activity?.SetTag("error.type", exception.GetType().FullName);
    }

    private sealed class SensitiveTelemetryProcessor : BaseProcessor<Activity>
    {
        public override void OnEnd(Activity activity)
        {
            // Remove URL paths from span names.
            if (activity.Source.Name == "Microsoft.AspNetCore")
            {
                activity.DisplayName = "HTTP Server Request";
            }
            else if (activity.Source.Name == "System.Net.Http")
            {
                activity.DisplayName = "HTTP Client Request";
            }

            // Remove sensitive attributes.
            foreach (var tag in activity.TagObjects)
            {
                if (ShouldRemove(tag.Key))
                {
                    activity.SetTag(tag.Key, null);
                }
            }
        }

        private static bool ShouldRemove(string key)
        {
            var normalized = key.ToLowerInvariant();
            return normalized is "url.full" or "url.path" or "url.query" or "http.route"
                or "http.url" or "http.target" or "http.request.body"
                or "http.response.body" or "db.statement" or "db.query.text"
                or "db.npgsql.data_source" or "db.connection_string"
                || normalized.StartsWith("http.request.header.", StringComparison.Ordinal)
                || normalized.StartsWith("http.response.header.", StringComparison.Ordinal)
                || normalized.StartsWith("db.query.", StringComparison.Ordinal)
                || normalized.StartsWith("enduser.", StringComparison.Ordinal)
                || normalized.StartsWith("user.", StringComparison.Ordinal)
                || normalized.Contains("password", StringComparison.Ordinal)
                || normalized.Contains("token", StringComparison.Ordinal)
                || normalized.Contains("authorization", StringComparison.Ordinal)
                || normalized.Contains("cookie", StringComparison.Ordinal)
                || normalized.Contains("email", StringComparison.Ordinal)
                || normalized.Contains("secret", StringComparison.Ordinal)
                || normalized.Contains("note.content", StringComparison.Ordinal);
        }
    }
}
