using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

namespace ByeMoney.Infrastructure.Modules.Identity.Persistence;

public static class IdentityModule
{
    public static IServiceCollection AddIdentityModule(this IServiceCollection services)
    {
        //افزودن ریپازیتوری‌ها و سرویس‌های مرتبط با ماژول Identity
       // services.AddScoped<IUserRepository, UserRepository>();
        // ... MediatR handlers خودکار با اسکن اسمبلی پیدا می‌شن
        return services;
    }
}
