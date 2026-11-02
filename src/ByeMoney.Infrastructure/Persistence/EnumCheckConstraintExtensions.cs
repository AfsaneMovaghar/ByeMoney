using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ByeMoney.Infrastructure.Persistence;

public static class EnumCheckConstraintExtensions
{
    // قید IN از نام‌های خود Enum ساخته می‌شود تا با کد ناسازگار نشود.
    public static CheckConstraintBuilder AddEnumCheckConstraint<TEntity, TEnum>(
        this TableBuilder<TEntity> table,
        string constraintName,
        string columnName)
        where TEntity : class
        where TEnum : struct, Enum
    {
        var allowedValues = string.Join(", ", Enum.GetNames<TEnum>().Select(name => $"'{name}'"));

        return table.HasCheckConstraint(constraintName, $"\"{columnName}\" IN ({allowedValues})");
    }
}
