namespace CampClotNot.Services;

/// <summary>
/// Which environment this is, shown as a pill on the header so nobody mistakes staging for the
/// real thing. Null in production. Resolved once at startup:
/// App:EnvironmentLabel if set ("none" hides it), else Railway's RAILWAY_ENVIRONMENT_NAME when it
/// isn't "production" (Railway sets it on every service), else "DEV" when running locally.
/// </summary>
public sealed record EnvironmentLabel(string? Text)
{
    public static EnvironmentLabel From(IConfiguration config, IHostEnvironment env)
    {
        var configured = config["App:EnvironmentLabel"];
        if (!string.IsNullOrWhiteSpace(configured))
            return new(configured.Trim().Equals("none", StringComparison.OrdinalIgnoreCase) ? null : configured.Trim().ToUpperInvariant());

        var railway = config["RAILWAY_ENVIRONMENT_NAME"];
        if (!string.IsNullOrWhiteSpace(railway))
            return new(railway.Trim().Equals("production", StringComparison.OrdinalIgnoreCase) ? null : railway.Trim().ToUpperInvariant());

        return new(env.IsDevelopment() ? "DEV" : null);
    }
}
