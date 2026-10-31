using System.Reflection;
using ByeMoney.Application.Modules.Purchases.Carts.Services;
using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Application.Modules.Wallet.Services;
using ByeMoney.Domain.Modules.Wallet.Ledgers;
using FluentAssertions;
using Xunit;

namespace ByeMoney.UnitTests;

public sealed class WalletArchitectureTests
{
    private const BindingFlags ScannedMethodFlags =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    private static readonly Assembly[] ScannedAssemblies =
    [
        typeof(ByeMoney.Application.DependencyInjection).Assembly,
        typeof(LedgerEntry).Assembly,
        typeof(ByeMoney.Infrastructure.Persistence.ApplicationDbContext).Assembly
    ];

    /// <summary>
    /// فهرست مجاز و قابل‌گسترش کلاس‌هایی که اجازه فراخوانی مستقیم فکتوری LedgerEntry.Create را دارند.
    /// </summary>
    public static readonly HashSet<Type> AllowedPostingTypes =
    [
        typeof(WalletPostingService),
        typeof(CourseCartPayment),
        typeof(LedgerEntry)
    ];

    /// <summary>
    /// تنها کلاسی که اجازهٔ فراخوانی ثبت افزایش نور را دارد؛ سرویس واحد نور.
    /// کسر موجودی و ثبت‌های خرید در این فهرست محدود نمی‌شوند.
    /// </summary>
    public static readonly HashSet<Type> AllowedCreditPostingTypes =
    [
        typeof(NoorCreditService)
    ];

    [Fact]
    public void OnlyAllowedPostingTypesMayCallLedgerEntryCreate()
    {
        var callers = FindDirectCallers(typeof(LedgerEntry).GetMethod(nameof(LedgerEntry.Create))!);

        callers.Should().Contain(caller => caller.Contains("." + nameof(WalletPostingService) + ".", StringComparison.Ordinal),
            "شناسایی فراخوانی‌ها باید کار کند، وگرنه این نگهبان بی‌اثر است.");
        callers.Should().OnlyContain(caller => IsAllowedCaller(caller, AllowedPostingTypes),
            "تنها سرویس‌های ثبت مالی مجاز باید مستقیماً LedgerEntry.Create را فراخوانی کنند.");
    }

    [Fact]
    public void OnlyUnifiedNoorCreditServiceMayPostCredit()
    {
        var callers = FindDirectCallers(
            typeof(IWalletPostingService).GetMethod(nameof(IWalletPostingService.PostCreditAsync))!);

        callers.Should().Contain(caller => caller.Contains("." + nameof(NoorCreditService) + ".", StringComparison.Ordinal),
            "شناسایی فراخوانی‌ها باید کار کند، وگرنه این نگهبان بی‌اثر است.");
        callers.Should().OnlyContain(caller => IsAllowedCaller(caller, AllowedCreditPostingTypes),
            "افزایش نور فقط باید از سرویس واحد نور عبور کند.");
    }

    private static bool IsAllowedCaller(string caller, IEnumerable<Type> allowedTypes)
        => allowedTypes.Any(allowed => caller.StartsWith(allowed.FullName + ".", StringComparison.Ordinal));

    private static List<string> FindDirectCallers(MethodInfo targetMethod)
    {
        var callers = new List<string>();

        foreach (var assembly in ScannedAssemblies)
        {
            foreach (var type in assembly.GetTypes())
            {
                foreach (var method in type.GetMethods(ScannedMethodFlags))
                {
                    var il = method.GetMethodBody()?.GetILAsByteArray();
                    if (il is null)
                        continue;

                    for (var index = 0; index <= il.Length - 5; index++)
                    {
                        if (il[index] is not (0x28 or 0x6F))
                            continue;

                        if (ResolveCalledMethod(type, il, index) == targetMethod)
                            callers.Add($"{OwningType(type).FullName}.{method.Name}");
                    }
                }
            }
        }

        return [.. callers.Distinct(StringComparer.Ordinal)];
    }

    private static Type OwningType(Type type)
        => type.DeclaringType ?? type;

    private static MethodBase? ResolveCalledMethod(Type callerType, byte[] il, int callOpcodeIndex)
    {
        try
        {
            var metadataToken = BitConverter.ToInt32(il, callOpcodeIndex + 1);
            return callerType.Module.ResolveMethod(metadataToken);
        }
        catch
        {
            // توکن‌های بیرون از ماژول قابل حل نیستند.
            return null;
        }
    }
}
