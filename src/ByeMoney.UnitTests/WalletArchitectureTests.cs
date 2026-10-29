using System.Reflection;
using ByeMoney.Application.Modules.Purchases.Cart.Services;
using ByeMoney.Application.Modules.Wallet.Services;
using ByeMoney.Domain.Modules.Wallet.Ledgers;
using FluentAssertions;
using Xunit;

namespace ByeMoney.UnitTests;

public sealed class WalletArchitectureTests
{
    /// <summary>
    /// فهرست مجاز و قابل‌گسترش کلاس‌هایی که اجازه فراخوانی مستقیم فکتوری LedgerEntry.Create را دارند.
    /// </summary>
    public static readonly HashSet<Type> AllowedPostingTypes =
    [
        typeof(WalletPostingService),
        typeof(CourseCartPayment),
        typeof(LedgerEntry)
    ];

    [Fact]
    public void OnlyAllowedPostingTypesMayCallLedgerEntryCreate()
    {
        var targetMethod = typeof(LedgerEntry).GetMethod(nameof(LedgerEntry.Create))!;
        var assemblies = new[]
        {
            typeof(ByeMoney.Application.DependencyInjection).Assembly,
            typeof(ByeMoney.Domain.Modules.Wallet.Wallets.Wallet).Assembly,
            typeof(ByeMoney.Infrastructure.Persistence.ApplicationDbContext).Assembly
        };

        var offendingCallers = new List<string>();

        foreach (var assembly in assemblies)
        {
            var types = assembly.GetTypes();
            foreach (var type in types)
            {
                var rootType = type.DeclaringType ?? type;
                if (AllowedPostingTypes.Contains(rootType))
                    continue;

                var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);
                foreach (var method in methods)
                {
                    var body = method.GetMethodBody();
                    if (body == null) continue;

                    var il = body.GetILAsByteArray();
                    if (il == null) continue;

                    for (int i = 0; i < il.Length - 4; i++)
                    {
                        if (il[i] == 0x28 || il[i] == 0x6F) // OpCodes.Call (0x28) or Callvirt (0x6F)
                        {
                            try
                            {
                                int metadataToken = BitConverter.ToInt32(il, i + 1);
                                var calledMethod = type.Module.ResolveMethod(metadataToken);
                                if (calledMethod == targetMethod)
                                {
                                    offendingCallers.Add($"{type.FullName}.{method.Name}");
                                }
                            }
                            catch
                            {
                                // Ignored for external tokens
                            }
                        }
                    }
                }
            }
        }

        offendingCallers.Should().BeEmpty("تنها سرویس‌های ثبت مالی مجاز باید مستقیماً LedgerEntry.Create را فراخوانی کنند.");
    }
}

