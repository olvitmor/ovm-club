using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace WebApi.Configuration;

public static class SwaggerConfiguration
{
    private const string BearerScheme = "Bearer";

    public static void AddBearerAuthentication(this SwaggerGenOptions options)
    {
        options.AddSecurityDefinition(BearerScheme, new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "accessToken from POST /auth/login"
        });

        options.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = BearerScheme
                    }
                },
                Array.Empty<string>()
            }
        });
    }
}
