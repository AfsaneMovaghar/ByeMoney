using ByeMoney.Application.Modules.TarhElahiIntegration.DTOs;

namespace ByeMoney.Application.Modules.TarhElahiIntegration.Interfaces;

public interface ITarhElahiIntegrationClient
{
    Task<TarhElahiUserDto?> GetUserAsync(string externalUserId, CancellationToken ct = default);
    Task<TarhElahiCourseDto?> GetCourseAsync(string externalId, CancellationToken ct = default);





    Task<bool> NotifyCoursePurchaseAsync(CoursePurchaseNotificationDto payload, CancellationToken ct = default);
}

