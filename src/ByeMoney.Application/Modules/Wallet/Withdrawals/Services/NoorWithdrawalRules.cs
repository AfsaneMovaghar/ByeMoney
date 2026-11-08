using System.Diagnostics.CodeAnalysis;
using ByeMoney.Domain.Modules.Wallet.Withdrawals;

namespace ByeMoney.Application.Modules.Wallet.Withdrawals.Services;

/// <summary>قواعد قالب ورودی خروج نور، مشترک بین اعتبارسنج‌ها و سرویس.</summary>
public static class NoorWithdrawalRules
{
    private const string IbanCountryPrefix = "IR";
    private const int IbanDigitCount = 24;

    /// <summary>فقط نام Enum پذیرفته می‌شود تا مقدار عددی در قرارداد API جا نیفتد.</summary>
    public static bool TryParseKind(string? value, out NoorWithdrawalKind kind) =>
        TryParseName(value, out kind);

    public static bool TryParseStatus(string? value, out NoorWithdrawalStatus status) =>
        TryParseName(value, out status);

    public static bool IsPositiveWholeNumber(decimal value) =>
        value > 0 && value == decimal.Floor(value);

    public static bool IsValidCardNumber(string? value) =>
        value is { Length: NoorWithdrawal.CardNumberLength } && value.All(char.IsAsciiDigit);

    public static bool IsValidIban(string? value) =>
        value is { Length: NoorWithdrawal.IbanLength } &&
        value.StartsWith(IbanCountryPrefix, StringComparison.Ordinal) &&
        value.Skip(IbanCountryPrefix.Length).Count(char.IsAsciiDigit) == IbanDigitCount;

    private static bool TryParseName<TEnum>([NotNullWhen(true)] string? value, out TEnum result)
        where TEnum : struct, Enum
    {
        result = default;
        return !string.IsNullOrWhiteSpace(value) &&
            Enum.GetNames<TEnum>().Contains(value, StringComparer.Ordinal) &&
            Enum.TryParse(value, ignoreCase: false, out result);
    }
}
