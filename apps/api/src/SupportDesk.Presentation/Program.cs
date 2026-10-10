using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SupportDesk.Application;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Infrastructure;
using SupportDesk.Infrastructure.Data;
using SupportDesk.Presentation.Auth;
using SupportDesk.Presentation.Extensions;
using SupportDesk.Presentation.Filters;
using SupportDesk.Presentation.Middleware;

var builder = WebApplication.CreateBuilder(args);
const string FrontendCorsPolicy = "frontend";

builder.Services.AddControllers(options => options.Filters.Add<ValidationFilter>())
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddCors(options => options.AddPolicy(FrontendCorsPolicy, policy => policy
    .WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
    .AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(jwtKey) || Encoding.UTF8.GetByteCount(jwtKey) < 32)
    throw new InvalidOperationException("Jwt:Key must be supplied via Jwt__Key and contain at least 32 UTF-8 bytes.");
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "NorthwindSupport";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "NorthwindSupportWeb";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true, ValidIssuer = jwtIssuer,
            ValidateAudience = true, ValidAudience = jwtAudience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateLifetime = true, ClockSkew = TimeSpan.Zero
        };
        // If an agent has become inactive, their existing JWT is immediately denied.
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async ctx =>
            {
                var rawId = ctx.Principal?.FindFirstValue(JwtRegisteredClaimNames.Sub);
                if (!int.TryParse(rawId, out var agentId))
                {
                    ctx.Fail("Invalid identity.");
                    return;
                }
                var db = ctx.HttpContext.RequestServices.GetRequiredService<SupportDbContext>();
                if (!await db.Set<Agent>().AsNoTracking()
                    .AnyAsync(a => a.Id == agentId && a.IsActive, ctx.HttpContext.RequestAborted))
                    ctx.Fail("Agent is inactive or missing.");
            }
        };
    });

builder.Services.AddAuthorization(options =>
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser().Build());

builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();
app.UseMiddleware<ExceptionHandlingMiddleware>();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    await app.MigrateAndSeedAsync();
}
app.UseCors(FrontendCorsPolicy);
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
