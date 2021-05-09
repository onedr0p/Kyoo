﻿using System;
using System.Collections.Generic;
using IdentityServer4.Extensions;
using IdentityServer4.Services;
using Kyoo.Authentication.Models;
using Kyoo.Authentication.Views;
using Kyoo.Controllers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Logging;

namespace Kyoo.Authentication
{
	/// <summary>
	/// A module that enable OpenID authentication for Kyoo.
	/// </summary>
	public class AuthenticationModule : IPlugin
	{
		/// <inheritdoc />
		public string Slug => "auth";
		
		/// <inheritdoc />
		public string Name => "Authentication";
		
		/// <inheritdoc />
		public string Description => "Enable OpenID authentication for Kyoo.";
		
		/// <inheritdoc />
		public ICollection<Type> Provides => ArraySegment<Type>.Empty;
		
		/// <inheritdoc />
		public ICollection<ConditionalProvide> ConditionalProvides => ArraySegment<ConditionalProvide>.Empty;
		
		/// <inheritdoc />
		public ICollection<Type> Requires => new []
		{
			typeof(IUserRepository)
		};
		
		
		/// <summary>
		/// The configuration to use.
		/// </summary>
		private readonly IConfiguration _configuration;

		/// <summary>
		/// A logger factory to allow IdentityServer to log things.
		/// </summary>
		private readonly ILoggerFactory _loggerFactory;

		/// <summary>
		/// The environment information to check if the app runs in debug mode
		/// </summary>
		private readonly IWebHostEnvironment _environment;


		/// <summary>
		/// Create a new authentication module instance and use the given configuration and environment.
		/// </summary>
		/// <param name="configuration">The configuration to use</param>
		/// <param name="loggerFactory">The logger factory to allow IdentityServer to log things</param>
		/// <param name="environment">The environment information to check if the app runs in debug mode</param>
		public AuthenticationModule(IConfiguration configuration,
			ILoggerFactory loggerFactory, 
			IWebHostEnvironment environment)
		{
			_configuration = configuration;
			_loggerFactory = loggerFactory;
			_environment = environment;
		}

		/// <inheritdoc />
		public void Configure(IServiceCollection services, ICollection<Type> availableTypes)
		{
			string publicUrl = _configuration.GetValue<string>("public_url").TrimEnd('/');

			if (_environment.IsDevelopment())
				IdentityModelEventSource.ShowPII = true;

			services.AddControllers();
			
			// TODO handle direct-videos with bearers (probably add a ?token query param and a app.Use to translate that for videos)
			
			// TODO Check if tokens should be stored.
			
			// TODO remove unused/commented code, add documentation.

			services.Configure<PermissionOption>(_configuration.GetSection(PermissionOption.Path));
			services.Configure<CertificateOption>(_configuration.GetSection(CertificateOption.Path));
			services.Configure<AuthenticationOption>(_configuration.GetSection(AuthenticationOption.Path));
			CertificateOption certificateOptions = new();
			_configuration.GetSection(CertificateOption.Path).Bind(certificateOptions);

			services.AddIdentityServer(options =>
				{
					options.IssuerUri = publicUrl;
					options.UserInteraction.LoginUrl = $"{publicUrl}/login";
					options.UserInteraction.ErrorUrl = $"{publicUrl}/error";
					options.UserInteraction.LogoutUrl = $"{publicUrl}/logout";
				})
				.AddInMemoryIdentityResources(IdentityContext.GetIdentityResources())
				.AddInMemoryApiScopes(IdentityContext.GetScopes())
				.AddInMemoryApiResources(IdentityContext.GetApis())
				.AddInMemoryClients(IdentityContext.GetClients())
				.AddInMemoryClients(_configuration.GetSection("authentication:clients"))
				.AddProfileService<AccountApi>()
				.AddSigninKeys(certificateOptions);
			// TODO split scopes (kyoo.read should be task.read, video.read etc)
			
			services.AddAuthentication()
				.AddJwtBearer(options =>
				{
					options.Authority = publicUrl;
					options.Audience = "kyoo";
					options.RequireHttpsMetadata = false;
				});
			
			services.AddAuthorization(options =>
			{
				AuthorizationPolicyBuilder scheme = new(JwtBearerDefaults.AuthenticationScheme);
				options.DefaultPolicy = scheme.RequireAuthenticatedUser().Build();
			
				string[] permissions = {"Read", "Write", "Play", "Admin"};
				foreach (string permission in permissions)
				{
					options.AddPolicy(permission, policy =>
					{
						policy.AuthenticationSchemes.Add(JwtBearerDefaults.AuthenticationScheme);
						policy.AddRequirements(new AuthRequirement(permission));
						// Scopes are disables to support default permissions.
						// To enable them, use the following line: policy.RequireScope($"kyoo.{permission.ToLower()}");
					});
				}
			});
			services.AddSingleton<IAuthorizationHandler, AuthorizationValidatorHandler>();

			DefaultCorsPolicyService cors = new(_loggerFactory.CreateLogger<DefaultCorsPolicyService>())
			{
				AllowedOrigins = {new Uri(publicUrl).GetLeftPart(UriPartial.Authority)}
			}; 
			services.AddSingleton<ICorsPolicyService>(cors);
		}

		/// <inheritdoc />
		public void ConfigureAspNet(IApplicationBuilder app)
		{
			app.UseCookiePolicy(new CookiePolicyOptions
			{
				MinimumSameSitePolicy = SameSiteMode.Strict
			});
			app.UseAuthentication();
			app.Use((ctx, next) =>
			{
				ctx.SetIdentityServerOrigin(_configuration.GetValue<string>("public_url"));
				return next();
			});
			app.UseIdentityServer();
			app.UseAuthorization();
		}
	}
}