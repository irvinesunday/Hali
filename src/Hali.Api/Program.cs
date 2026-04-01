using System;
using System.Text;
using Hali.Application.Auth;
using Hali.Application.Participation;
using Hali.Application.Signals;
using Hali.Application.Advisories;
using Hali.Infrastructure.Extensions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection("Auth"));
builder.Services.Configure<OtpOptions>(builder.Configuration.GetSection("Otp"));
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<IOtpService, OtpService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ISignalIngestionService, SignalIngestionService>();
builder.Services.AddScoped<IParticipationService, ParticipationService>();
builder.Services.AddScoped<IOfficialPostsService, OfficialPostsService>();

string jwtSecret = builder.Configuration["Auth:JwtSecret"]
    ?? throw new InvalidOperationException("Auth:JwtSecret is required");
string jwtIssuer = builder.Configuration["Auth:JwtIssuer"] ?? "hali";
string jwtAudience = builder.Configuration["Auth:JwtAudience"] ?? "hali";

builder.Services.AddAuthentication("Bearer").AddJwtBearer(opts =>
{
    opts.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
    };
});

builder.Services.AddAuthorization();
builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
