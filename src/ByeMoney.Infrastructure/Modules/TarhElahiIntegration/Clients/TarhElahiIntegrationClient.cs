using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ByeMoney.Application.Modules.TarhElahiIntegration.DTOs;
using ByeMoney.Application.Modules.TarhElahiIntegration.Exceptions;
using ByeMoney.Application.Modules.TarhElahiIntegration.Interfaces;
using Microsoft.Extensions.Logging;

namespace ByeMoney.Infrastructure.Modules.TarhElahiIntegration.Clients;

public class TarhElahiIntegrationClient : ITarhElahiIntegrationClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TarhElahiIntegrationClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public TarhElahiIntegrationClient(HttpClient httpClient, ILogger<TarhElahiIntegrationClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<TarhElahiUserDto?> GetUserAsync(string externalUserId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(externalUserId);

        var requestUri = $"api/integrations/byemoney/v1/users/{Uri.EscapeDataString(externalUserId)}";
        return await SendAndDeserializeAsync<TarhElahiUserDto>(requestUri, externalUserId, "user", ct);
    }

    public async Task<TarhElahiCourseDto?> GetCourseAsync(string externalId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(externalId);

        var requestUri = $"api/integrations/byemoney/v1/courses/{Uri.EscapeDataString(externalId)}";
        return await SendAndDeserializeAsync<TarhElahiCourseDto>(requestUri, externalId, "course", ct);
    }

    public async Task<bool> NotifyCoursePurchaseAsync(CoursePurchaseNotificationDto payload, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var requestUri = "api/integrations/byemoney/v1/purchases/confirm";
        try
        {
            var response = await _httpClient.PostAsJsonAsync(requestUri, payload, ct);
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation(
                    "Successfully notified TarhElahi of course purchase {PurchaseId} for external user {ExternalUserId}.",
                    payload.PurchaseId,
                    payload.BuyerExternalUserId);
                return true;
            }

            _logger.LogWarning(
                "TarhElahi purchase notification returned non-success status code {StatusCode} for purchase {PurchaseId}.",
                (int)response.StatusCode,
                payload.PurchaseId);
            return false;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error notifying TarhElahi of course purchase {PurchaseId}.",
                payload.PurchaseId);
            return false;
        }
    }

    private async Task<T?> SendAndDeserializeAsync<T>(
        string requestUri,
        string identifier,
        string entityType,
        CancellationToken ct) where T : class
    {
        HttpResponseMessage response;
        try
        {
            response = await _httpClient.GetAsync(requestUri, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            _logger.LogInformation("Request for TarhElahi {EntityType} '{Identifier}' was canceled by caller.", entityType, identifier);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to TarhElahi while fetching {EntityType} '{Identifier}'.", entityType, identifier);
            throw new TarhElahiUnavailableException(
                $"Tarh-e-Elahi service is currently unavailable or timed out while fetching {entityType}.",
                ex);
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogInformation("TarhElahi {EntityType} '{Identifier}' was not found (404).", entityType, identifier);
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            var statusCode = response.StatusCode;
            _logger.LogWarning("TarhElahi returned non-success status code {StatusCode} for {EntityType} '{Identifier}'.",
                (int)statusCode, entityType, identifier);

            throw new TarhElahiUnavailableException(
                $"Tarh-e-Elahi request for {entityType} failed with status code {(int)statusCode} ({statusCode}).",
                statusCode: statusCode);
        }

        try
        {
            var contentStream = await response.Content.ReadAsStreamAsync(ct);
            return await JsonSerializer.DeserializeAsync<T>(contentStream, JsonOptions, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deserialize TarhElahi response for {EntityType} '{Identifier}'.", entityType, identifier);
            throw new TarhElahiUnavailableException(
                $"Failed to deserialize response from Tarh-e-Elahi for {entityType}.",
                ex,
                response.StatusCode);
        }
    }
}
