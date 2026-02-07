using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Options;
using AreWeDoomd.Infrastructure.Authentication.ClientTokens;
using AreWeDoomd.Infrastructure.Authentication.Jwt;
using AreWeDoomd.Infrastructure.Authentication.Options;
using AreWeDoomd.Infrastructure.Common.Email;
using AreWeDoomd.Infrastructure.Common.Options;
using AreWeDoomd.Infrastructure.Common.Persistence;
using AreWeDoomd.Infrastructure.Common.Repositories;
using AreWeDoomd.Infrastructure.Common.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace AreWeDoomd.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddDbContext<AreWeDoomdDbContext>(options =>
            {
                options.UseSqlServer(
                    configuration.GetConnectionString("AreWeDoomdSql"),
                    sql =>
                    {
                        sql.EnableRetryOnFailure(
                            maxRetryCount: 5,
                            maxRetryDelay: TimeSpan.FromSeconds(10),
                            errorNumbersToAdd: null);
                    });
            });

            services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AreWeDoomdDbContext>());
            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<IPasswordResetRequestRepository, PasswordResetRequestRepository>();
            services.AddScoped<IDateTimeProvider, SystemDateTimeProvider>();
            services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
            services.AddSingleton<IPasswordResetCodeGenerator, NumericPasswordResetCodeGenerator>();
            services.AddScoped<IUserTokenFactory, JwtUserTokenFactory>();
            services.AddSingleton<IEmailSender, SmtpEmailSender>();
            services.AddSingleton<IPasswordResetSettings, PasswordResetSettings>();
            services.AddHttpContextAccessor();
            services.AddScoped<IClientContextAccessor, ClientContextAccessor>();
            services.AddScoped<IClientTokenValidator, ClientTokenValidator>();

            services.Configure<PasswordResetOptions>(configuration.GetSection("PasswordReset"));
            services.Configure<SmtpOptions>(configuration.GetSection(SmtpOptions.SectionName));
            services.Configure<UserJwtOptions>(configuration.GetSection(UserJwtOptions.SectionName));
            services.Configure<ClientAuthenticationOptions>(configuration.GetSection(ClientAuthenticationOptions.SectionName));

            var jwtOptions = configuration.GetSection(UserJwtOptions.SectionName).Get<UserJwtOptions>() ?? new UserJwtOptions();
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.IncludeErrorDetails = true;
                options.SaveToken = true;
                options.MapInboundClaims = false;
                options.TokenValidationParameters = CreateTokenValidationParameters(jwtOptions);
            });

            return services;
        }

        private static TokenValidationParameters CreateTokenValidationParameters(UserJwtOptions options)
        {
            var rsa = RsaKeyLoader.LoadPublicKey(options.PublicKey);

            return new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = options.Issuer,
                ValidateAudience = true,
                ValidAudience = options.Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new RsaSecurityKey(rsa),
                ValidAlgorithms = [options.Algorithm],
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30)
            };
        }
    }
}

