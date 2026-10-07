// Smart Solar Microgrid Trading System - application composition root.
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using SolarGridX.Api.Configuration;
using SolarGridX.Api.Infrastructure;
using SolarGridX.Api.Integrations;
using SolarGridX.Api.Middleware;
using SolarGridX.Api.Repositories;
using SolarGridX.Api.Services;


DotEnvConfiguration.Load();
var builder = WebApplication.CreateBuilder(args);
builder.Services.Configure<MongoDbOptions>(builder.Configuration.GetSection(MongoDbOptions.SectionName));
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<SeedOptions>(builder.Configuration.GetSection("Seed"));

var jwt =
    builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? new JwtOptions();
if (string.IsNullOrWhiteSpace(jwt.Secret) || jwt.Secret.Length < 32)
{
    throw new InvalidOperationException("Configure Jwt:Secret with at least 32 characters through user secrets or environment variables.");
}

builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter())
);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc(
        "v1",
        new OpenApiInfo
        {
            Title = "SolarGridX API",
            Version = "v1",
        }
    );
    options.AddSecurityDefinition(
        "Bearer",
        new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
        }
    );
    // The Authorize button in Swagger UI allows entering the JWT Bearer token once for all requests.
});
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Secret)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
        };
        options.Events = new JwtBearerEvents
        {
            OnChallenge = context =>
            {
                context.HandleResponse();
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return context.Response.WriteAsJsonAsync(
                    new SolarGridX.Api.DTOs.Responses.ErrorResponse(
                        StatusCodes.Status401Unauthorized,
                        "Authentication is required or the access token is invalid.",
                        null,
                        DateTime.UtcNow
                    )
                );
            },
            OnForbidden = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return context.Response.WriteAsJsonAsync(
                    new SolarGridX.Api.DTOs.Responses.ErrorResponse(
                        StatusCodes.Status403Forbidden,
                        "You do not have permission to perform this operation.",
                        null,
                        DateTime.UtcNow
                    )
                );
            },
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddCors(options =>
{
    options.AddPolicy(
        "WebClient",
        policy =>
        {
            var configuredOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
            var origins = new HashSet<string>(configuredOrigins, StringComparer.OrdinalIgnoreCase)
            {
                "https://solargridx-web.vercel.app",
                "http://localhost:5173",
                "http://localhost:3000"
            };

            policy
                .WithOrigins(origins.ToArray())
                .AllowAnyHeader()
                .AllowAnyMethod();
        }
    );
});
builder.Services.AddSingleton<MongoContext>();
builder.Services.AddScoped<IPasswordService, PasswordService>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<StaffService>();
builder.Services.AddScoped<StationSlotService>();
// Member 2: station repository, FAT service, and cross-member integration seam.
builder.Services.AddScoped<IStationRepository, StationRepository>();
builder.Services.AddScoped<IStationService, StationService>();
builder.Services.AddScoped<IReservationLookupService, ReservationLookupService>();
builder.Services.AddScoped<ReservationService>();
builder.Services.AddScoped<InitialBackofficeSeeder>();

var app = builder.Build();

var dbOptions = app.Configuration.GetSection(MongoDbOptions.SectionName).Get<MongoDbOptions>();
var jwtOpts = app.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>();
var corsAllowed = string.Join(", ", app.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? []);

if (app.Environment.IsDevelopment())
{
    app.Logger.LogInformation("--- DEVELOPMENT ENVIRONMENT ---");
    app.Logger.LogInformation("Loaded JWT Secret: {Secret}", jwtOpts?.Secret);
    app.Logger.LogInformation("Loaded CORS Origins: {Cors}", corsAllowed);
    
    app.UseSwagger();
    app.UseSwaggerUI();
}
else if (app.Environment.IsProduction())
{
    app.Logger.LogInformation("--- PRODUCTION ENVIRONMENT ---");
    app.Logger.LogInformation("Loaded DB Name: {DbName}", dbOptions?.DatabaseName);
    app.Logger.LogInformation("Loaded JWT Issuer: {Issuer}", jwtOpts?.Issuer);
    app.Logger.LogInformation("Loaded CORS Origins: {Cors}", corsAllowed);
}

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseHttpsRedirection();
app.UseCors("WebClient");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet(
        "/health",
        async (MongoContext db, CancellationToken ct) =>
        {
            await db.Database.RunCommandAsync<MongoDB.Bson.BsonDocument>(
                new MongoDB.Bson.BsonDocument("ping", 1),
                cancellationToken: ct
            );

            return Results.Ok(new { status = "Healthy", timestamp = DateTime.UtcNow });
        }
    )
    .AllowAnonymous();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    await services.GetRequiredService<MongoContext>().MigrateLegacyProsumerAccountsAsync();
    await services.GetRequiredService<MongoContext>().InitializeIndexesAsync();
    await services.GetRequiredService<InitialBackofficeSeeder>().SeedAsync();
}

app.Run();

public partial class Program { }
