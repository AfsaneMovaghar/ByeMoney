using System.Text.Json.Serialization;

namespace ByeMoney.Application.Modules.TarhElahiIntegration.DTOs;

/// <summary>
/// انواع آیتم‌های کاتالوگ در سیستم طرح الهی (Strapi).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<TarhElahiCatalogItemType>))]
public enum TarhElahiCatalogItemType
{
    [JsonStringEnumMemberName("course")]
    Course,

    [JsonStringEnumMemberName("course_chapter")]
    CourseChapter,

    [JsonStringEnumMemberName("product")]
    Product
}

