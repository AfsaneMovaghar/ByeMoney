using ByeMoney.Application.Modules.Settings.Interfaces;
using ByeMoney.API.Contracts.Settings;
using Microsoft.AspNetCore.Mvc;

namespace ByeMoney.API.Controllers;

[ApiController]
[Route("api/settings")]
public class SettingsController(ISystemSettingRepository settings) : ControllerBase
{
    [HttpGet("conversion-rate")]
    [ProducesResponseType(typeof(ConversionRateResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ConversionRateResponse>> GetConversionRate(CancellationToken ct)
    {
        var rialPerNoor = await settings.GetRialToNoorConversionRateAsync(ct);
        return Ok(new ConversionRateResponse(rialPerNoor, rialPerNoor / 10m));
    }
}
