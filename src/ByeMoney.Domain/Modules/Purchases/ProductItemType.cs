using System.Text.Json;
using System.Text.Json.Serialization;

namespace ByeMoney.Domain.Modules.Purchases;

[JsonConverter(typeof(ProductItemTypeJsonConverter))]
public enum ProductItemType
{
    Course,
    CourseChapter,
    Product
}

public sealed class ProductItemTypeJsonConverter : JsonConverter<ProductItemType>
{
    public override ProductItemType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out var intVal))
        {
            if (Enum.IsDefined(typeof(ProductItemType), intVal))
                return (ProductItemType)intVal;
            throw new JsonException($"Invalid value {intVal} for ProductItemType.");
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            var str = reader.GetString();
            if (string.IsNullOrWhiteSpace(str))
                return ProductItemType.Course;

            return str.Trim().ToLowerInvariant() switch
            {
                "course" => ProductItemType.Course,
                "course_chapter" or "coursechapter" or "chapter" => ProductItemType.CourseChapter,
                "product" => ProductItemType.Product,
                _ => throw new JsonException($"Unknown ProductItemType: '{str}'. Expected 'course', 'course_chapter', or 'product'.")
            };
        }

        throw new JsonException($"Unexpected token {reader.TokenType} parsing ProductItemType.");
    }

    public override void Write(Utf8JsonWriter writer, ProductItemType value, JsonSerializerOptions options)
    {
        var str = value switch
        {
            ProductItemType.CourseChapter => "course_chapter",
            ProductItemType.Product => "product",
            _ => "course"
        };
        writer.WriteStringValue(str);
    }
}

