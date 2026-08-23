using ByeMoney.Application.Common.Behaviors;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace ByeMoney.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {

        services.AddMediatR(cfg =>
          {
              cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
              cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
          });

        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());
        ValidatorOptions.Global.DefaultRuleLevelCascadeMode = CascadeMode.Stop;
        ValidatorOptions.Global.DefaultClassLevelCascadeMode = CascadeMode.Stop;
        return services;
    }
}