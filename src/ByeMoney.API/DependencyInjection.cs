using System.Text;
using ByeMoney.API.Authentication;
using ByeMoney.API.Authorization;
using ByeMoney.API.Resources;
using ByeMoney.API.Services;
using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Domain.Modules.Identity.Permissions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

namespace ByeMoney.API;

public static class DependencyInjection
{
    public static IServiceCollection AddPresentationServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var strapiJwtSecret = configuration["Strapi:JwtSecret"];
        if (string.IsNullOrEmpty(strapiJwtSecret))
        {
            throw new InvalidOperationException(ApiErrors.Auth_JwtSecretMissing);
        }

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(strapiJwtSecret)),
                    ClockSkew = TimeSpan.FromMinutes(2),
                    NameClaimType = "id"
                };
            });

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IClaimsTransformation, RbacClaimsTransformation>();
        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();

        services.AddAuthorization(options =>
        {
            options.AddPolicy("RequireNoorInject", policy =>
                policy.Requirements.Add(new PermissionRequirement(Permissions.Noor.Inject)));

            options.AddPolicy("RequireTopUpReview", policy =>
                policy.Requirements.Add(new PermissionRequirement(Permissions.TopUp.Review)));
        });

        services.AddControllers();
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Description = "JWT Authorization header. Enter: **Bearer {token}**",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.Http,
                Scheme = "Bearer",
                BearerFormat = "JWT"
            });

            options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Bearer", document)] = []
            });
        });

        return services;
    }
}
