using ByeMoney.Domain.Common.Exceptions;
using ByeMoney.Domain.Resources;

namespace ByeMoney.Domain.Modules.Settings;

public class SystemSetting
{
    /// <summary>Unique setting key, e.g. "ConversionRate:RialToNoor".</summary>
    public string Key { get; private set; } = null!;

    /// <summary>Configured setting value stored as text (e.g. decimal string).</summary>
    public string Value { get; private set; } = null!;

    /// <summary>Optional human-readable description of what the setting controls.</summary>
    public string? Description { get; private set; }

    /// <summary>UTC timestamp when the setting was last created or modified.</summary>
    public DateTime UpdatedAtUtc { get; private set; }

    private SystemSetting() { }

    public static SystemSetting Create(string key, string value, string? description = null, DateTime? updatedAtUtc = null)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new DomainException(DomainErrors.SystemSetting_KeyRequired);

        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException(DomainErrors.SystemSetting_ValueRequired);

        return new SystemSetting
        {
            Key = key.Trim(),
            Value = value.Trim(),
            Description = description,
            UpdatedAtUtc = updatedAtUtc ?? DateTime.UtcNow
        };
    }

    public void UpdateValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException(DomainErrors.SystemSetting_ValueRequired);

        Value = value.Trim();
        UpdatedAtUtc = DateTime.UtcNow;
    }
}