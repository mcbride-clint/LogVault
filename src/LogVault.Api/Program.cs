using LogVault.Application;
using LogVault.Application.Workers;
using LogVault.Api.Configuration;
using LogVault.Api.Endpoints;
using LogVault.Api.Hubs;
using LogVault.Api.Middleware;
using LogVault.Domain.Services;
using LogVault.Infrastructure;
using LogVault.Infrastructure.Data;
using LogVault.Infrastructure.HealthChecks;
using LogVault.Infrastructure.Mail;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

// ----- Configuration Validation -----
builder.Services.AddOptions<ActiveDirectoryOptions>()
    .BindConfiguration(ActiveDirectoryOptions.Section)
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<EmailOptions>()
    .BindConfiguration(EmailOptions.Section)
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<RetentionOptions>()
    .BindConfiguration(RetentionOptions.Section)
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<IngestionOptions>()
    .BindConfiguration(IngestionOptions.Section)
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<ApiKeyOptions>()
    .BindConfiguration(ApiKeyOptions.Section)
    .ValidateDataAnnotations()
    .ValidateOnStart();

// ----- Infrastructure -----
builder.Services.AddLogVaultInfrastructure(config);
builder.Services.AddLogVaultMail();

// ----- Application -----
builder.Services.AddLogVaultApplication(config);

// ----- SignalR Hub Notifier (Api implements Domain interface) -----
builder.Services.AddSignalR();
builder.Services.AddSingleton<ILogHubNotifier, LogHubNotifier>();

// ----- Authentication -----
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(opt =>
    {
        opt.LoginPath = "/login";
        opt.LogoutPath = "/api/auth/logout";
        opt.Events.OnRedirectToLogin = ctx =>
        {
            if (ctx.Request.Path.StartsWithSegments("/api"))
            {
                ctx.Response.StatusCode = 401;
                return Task.CompletedTask;
            }
            ctx.Response.Redirect(ctx.RedirectUri);
            return Task.CompletedTask;
        };
        opt.Events.OnRedirectToAccessDenied = ctx =>
        {
            if (ctx.Request.Path.StartsWithSegments("/api"))
            {
                ctx.Response.StatusCode = 403;
                return Task.CompletedTask;
            }
            ctx.Response.Redirect(ctx.RedirectUri);
            return Task.CompletedTask;
        };
    });

builder.Services.AddAuthorization(o =>
{
    o.AddPolicy("RequireUser", p => p.RequireRole("User", "Admin"));
    o.AddPolicy("RequireAdmin", p => p.RequireRole("Admin"));
    o.AddPolicy("CanIngest", p => p.RequireAssertion(ctx =>
        ctx.User.IsInRole("Admin") ||
        ctx.User.IsInRole("User") ||
        ctx.User.HasClaim("auth_method", "ApiKey")));
});

// ----- API Key Middleware -----
builder.Services.AddScoped<ApiKeyMiddleware>();

// ----- Swagger -----
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "LogVault API", Version = "v1" });
});

// ----- Health Checks -----
builder.Services.AddHealthChecks()
    .AddDbContextCheck<LogVaultDbContext>("database")
    .AddCheck<SmtpHealthCheck>("smtp")
    .AddCheck<LdapHealthCheck>("ldap");

// ----- CORS (for WASM client) -----
builder.Services.AddCors(opt =>
    opt.AddDefaultPolicy(p => p
        .WithOrigins(config["AllowedOrigins"] ?? "http://localhost:5000", "https://localhost:5001")
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()));

var app = builder.Build();

// ----- DB Initialization -----
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<LogVaultDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<LogVaultDbContext>>();
    await DbInitializer.InitializeAsync(db, logger);
}

// ----- Middleware Pipeline -----
app.UseExceptionHandler(errApp =>
{
    errApp.Run(async ctx =>
    {
        ctx.Response.ContentType = "application/json";
        ctx.Response.StatusCode = 500;
        await ctx.Response.WriteAsJsonAsync(new { error = "An unexpected error occurred." });
    });
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseAuthentication();
app.UseMiddleware<ApiKeyMiddleware>();
app.UseAuthorization();

// ----- Health -----
app.MapHealthChecks("/health", new HealthCheckOptions { AllowCachingResponses = false });

// ----- SignalR Hub -----
app.MapHub<LogHub>("/hubs/logs");

// ----- API Endpoints -----
app.MapIngestEndpoints();
app.MapLogQueryEndpoints();
app.MapAlertEndpoints();
app.MapAdminEndpoints();
app.MapAuthEndpoints();

app.Run();

public partial class Program { } // for WebApplicationFactory in tests
