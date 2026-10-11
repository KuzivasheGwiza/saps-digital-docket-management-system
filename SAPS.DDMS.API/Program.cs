using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using QuestPDF.Infrastructure;
using SAPS.DDMS.API.Data;
using SAPS.DDMS.API.Helpers;
using SAPS.DDMS.API.Repositories;
using SAPS.DDMS.API.Services;
using SAPS.DDMS.API.Security;
using Microsoft.Data.SqlClient;

var builder = WebApplication.CreateBuilder(args);

QuestPDF.Settings.License = LicenseType.Community; // Team must confirm that this project qualifies for the QuestPDF Community licence.

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Add services to the container
builder.Services.AddControllers();
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var errors = context.ModelState
            .Where(item => item.Value?.Errors.Count > 0)
            .ToDictionary(
                item => item.Key,
                item => item.Value!.Errors
                    .Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage)
                        ? "The supplied value is invalid."
                        : error.ErrorMessage)
                    .ToArray());
        return new BadRequestObjectResult(new
        {
            success = false,
            message = "Please correct the highlighted information and try again.",
            errors
        });
    };
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHttpClient();
builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddAuthorization(options =>
{
    foreach (var permission in PermissionCatalog.All)
    {
        options.AddPolicy(permission.Name, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.AddRequirements(new PermissionRequirement(permission.Name));
        });
    }
});

// Configure Swagger with JWT support
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "SAPS DDMS API",
        Version = "v1",
        Description = "SAPS Digital Docket Management System "
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter 'Bearer' followed by your JWT token"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
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

// Configure Database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions => sqlOptions.CommandTimeout(60)));

// Configure JWT Authentication
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"];

if (string.IsNullOrEmpty(secretKey) || secretKey.Length < 32)
{
    throw new InvalidOperationException("JWT SecretKey must be at least 32 characters long.");
}

var key = Encoding.UTF8.GetBytes(secretKey);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false;
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidateIssuer = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwtSettings["Audience"],
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var token = context.Request.Headers["Authorization"].FirstOrDefault();
                if (!string.IsNullOrEmpty(token) && token.StartsWith("Bearer "))
                {
                    context.Token = token.Substring("Bearer ".Length);
                }
                return Task.CompletedTask;
            },
            OnTokenValidated = async context =>
            {
                var authorization = context.Request.Headers.Authorization.ToString();
                var rawToken = authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                    ? authorization["Bearer ".Length..].Trim()
                    : string.Empty;
                if (string.IsNullOrWhiteSpace(rawToken))
                {
                    context.Fail("Bearer token is missing.");
                    return;
                }

                var db = context.HttpContext.RequestServices.GetRequiredService<ApplicationDbContext>();
                var tokenHash = PasswordHelper.HashToken(rawToken);
                var session = await db.Sessions.FirstOrDefaultAsync(item =>
                    item.TokenHash == tokenHash &&
                    item.Logout == null &&
                    item.ExpiryTime > DateTime.UtcNow);
                if (session == null)
                {
                    context.Fail("Session has expired or was revoked.");
                    return;
                }

                if (!session.LastActivity.HasValue || session.LastActivity.Value < DateTime.UtcNow.AddMinutes(-1))
                {
                    var now = DateTime.UtcNow;
                    var timeoutValue = await db.SystemSettings
                        .AsNoTracking()
                        .Where(setting => setting.SettingKey == "SESSION_TIMEOUT_MINUTES")
                        .Select(setting => setting.SettingValue)
                        .FirstOrDefaultAsync();
                    var timeoutMinutes = int.TryParse(timeoutValue, out var configuredTimeout)
                        ? Math.Clamp(configuredTimeout, 1, 120)
                        : 60;
                    session.LastActivity = now;
                    session.ExpiryTime = now.AddMinutes(timeoutMinutes);
                    await db.SaveChangesAsync();
                }
            }
        };
    });

// Configure CORS
var configuredOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins")
    .Get<string[]>()
    ?.Where(origin => !string.IsNullOrWhiteSpace(origin))
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray();
var allowedCorsOrigins = configuredOrigins is { Length: > 0 }
    ? configuredOrigins
    : new[] { "http://localhost:4200", "https://localhost:4200" };

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngularApp", policy =>
    {
        policy.WithOrigins(allowedCorsOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Register Helpers
builder.Services.AddScoped<JwtHelper>();

// Register Repositories
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IDocketRepository, DocketRepository>();
builder.Services.AddScoped<IIncidentRepository, IncidentRepository>();
builder.Services.AddScoped<IPersonRepository, PersonRepository>();
builder.Services.AddScoped<IInvestigationRepository, InvestigationRepository>();
builder.Services.AddScoped<IEvidenceRepository, EvidenceRepository>();
builder.Services.AddScoped<INotificationRepository, NotificationRepository>();

// Register Services
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IDatabaseBackupService, DatabaseBackupService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<ISmsService, SmsService>();
builder.Services.AddScoped<ISystemConfigService, SystemConfigService>();
builder.Services.AddScoped<IReportChartService, ReportChartService>();
builder.Services.AddScoped<IReportPdfService, ReportPdfService>();
builder.Services.AddScoped<IFileStorageService, FileStorageService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IDocketService, DocketService>();
builder.Services.AddScoped<IIncidentService, IncidentService>();
builder.Services.AddScoped<IEvidenceService, EvidenceService>();
builder.Services.AddScoped<IPersonService, PersonService>();
builder.Services.AddScoped<IInvestigationNoteService, InvestigationNoteService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IDataExchangeService, DataExchangeService>();

// Register AI service: deterministic local fallback. If you provide Gemini API key, wire a Gemini provider here.
builder.Services.AddScoped<IAiService, LocalAiService>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// The Angular development server proxies to the local HTTP endpoint. Avoid a
// local certificate handshake/redirect on every cold proxy connection; keep
// HTTPS redirection enabled outside Development.
if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();

// Create wwwroot folder for file uploads
var wwwrootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
if (!Directory.Exists(wwwrootPath))
    Directory.CreateDirectory(wwwrootPath);

var uploadsPath = Path.Combine(wwwrootPath, "uploads");
if (!Directory.Exists(uploadsPath))
    Directory.CreateDirectory(uploadsPath);

app.UseCors("AllowAngularApp");
app.UseMiddleware<ApiExceptionMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Ensure database is created with diagnostics and clearer error handling
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    // Read configuration & environment for diagnostics (do not log secrets)
    var configuration = app.Configuration;
    var envName = app.Environment.EnvironmentName;
    var connString = configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
    string dataSource;
    try
    {
        dataSource = new SqlConnectionStringBuilder(connString).DataSource;
    }
    catch
    {
        dataSource = "(unable to parse DataSource)";
    }

    app.Logger.LogInformation("Starting database initialization. Environment={env}, DataSource={ds}", envName, dataSource);

    try
    {
        if (!await dbContext.Database.CanConnectAsync())
            throw new InvalidOperationException("The SAPS DDMS database is not reachable.");
        if (!await DatabaseBootstrapper.IsOperationalSchemaCurrentAsync(dbContext))
            await DatabaseBootstrapper.EnsureOperationalSchemaAsync(dbContext);
        await DatabaseBootstrapper.EnsureOperationalReferenceDataAsync(dbContext);
        // Compile the exact login query and load a real active row. A guaranteed
        // miss compiles the query but does not warm the related Role/Station data
        // pages, which made the first real officer login noticeably slower.
        var warmupUsername = await dbContext.Users
            .AsNoTracking()
            .Where(u => u.IsActive && u.IsActivated && !u.IsLocked)
            .OrderBy(u => u.UserID)
            .Select(u => u.Username)
            .FirstOrDefaultAsync();
        if (!string.IsNullOrWhiteSpace(warmupUsername))
        {
            _ = await dbContext.Users
                .AsNoTracking()
                .Include(u => u.Role)
                .Include(u => u.Station)
                .FirstOrDefaultAsync(u => u.Username == warmupUsername);

            _ = await dbContext.SystemSettings
                .AsNoTracking()
                .Where(s => s.SettingKey == "TWO_FACTOR_REQUIRED" ||
                            s.SettingKey == "SESSION_TIMEOUT_MINUTES")
                .Select(s => s.SettingValue)
                .ToListAsync();
        }
        app.Logger.LogInformation("Database connection and operational schema verified.");
    }
    catch (SqlException sqlEx)
    {
        app.Logger.LogError(sqlEx, "SQL error while ensuring database. Environment={env}, DataSource={ds}", envName, dataSource);
        throw new InvalidOperationException($"Could not connect to SQL Server instance '{dataSource}'. Make sure the instance is running, reachable, and the connection string is correct. See inner exception for details.", sqlEx);
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Unexpected error while ensuring database. Environment={env}, DataSource={ds}", envName, dataSource);
        throw;
    }
}

// Warm the two heaviest read-only navbar queries after the HTTP listeners are
// available. This keeps launch responsive while ensuring Dashboard, Dockets,
// and Calendar do not incur one-time EF/SQL compilation delays on first use.
app.Lifetime.ApplicationStarted.Register(() =>
{
    _ = Task.Run(async () =>
    {
        try
        {
            using var warmupScope = app.Services.CreateScope();
            var warmupDb = warmupScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var warmupUserId = await warmupDb.Users
                .AsNoTracking()
                .Where(u => u.IsActive && u.IsActivated && !u.IsLocked)
                .OrderBy(u => u.UserID)
                .Select(u => (int?)u.UserID)
                .FirstOrDefaultAsync();
            if (!warmupUserId.HasValue) return;

            var dashboard = warmupScope.ServiceProvider.GetRequiredService<IDashboardService>();
            await dashboard.GetDashboardStatsAsync(warmupUserId.Value);

            var dockets = warmupScope.ServiceProvider.GetRequiredService<IDocketService>();
            await dockets.GetDocketsByUserAsync(warmupUserId.Value, null!);
            app.Logger.LogInformation("Navbar data queries warmed successfully.");
        }
        catch (Exception ex)
        {
            app.Logger.LogWarning(ex, "Navbar query warm-up did not complete; requests will continue normally.");
        }
    });
});

// Anonymous readiness endpoint for load balancers and deployment smoke checks.
// It is mapped only after startup has verified the operational database schema.
app.MapGet("/health", () => Results.Ok(new
{
    status = "Healthy",
    service = "SAPS DDMS API"
})).AllowAnonymous();

app.Run();
