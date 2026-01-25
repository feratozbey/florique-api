using Microsoft.AspNetCore.Mvc;
using Florique.Api.Services;
using Florique.Api.Models;

namespace Florique.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BackgroundsController : ControllerBase
{
    private readonly DatabaseService _databaseService;
    private readonly ILogger<BackgroundsController> _logger;

    public BackgroundsController(DatabaseService databaseService, ILogger<BackgroundsController> logger)
    {
        _databaseService = databaseService;
        _logger = logger;
    }

    /// <summary>
    /// Get all available background options (names only, for backwards compatibility)
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<string>>>> GetBackgrounds()
    {
        var backgrounds = await _databaseService.LoadBackgroundOptionsAsync();

        return Ok(new ApiResponse<List<string>>
        {
            Success = true,
            Data = backgrounds
        });
    }

    /// <summary>
    /// Get all available background options with descriptions
    /// </summary>
    [HttpGet("with-descriptions")]
    public async Task<ActionResult<ApiResponse<List<DatabaseService.BackgroundOption>>>> GetBackgroundsWithDescriptions()
    {
        var backgrounds = await _databaseService.LoadBackgroundOptionsWithDescriptionsAsync();

        return Ok(new ApiResponse<List<DatabaseService.BackgroundOption>>
        {
            Success = true,
            Data = backgrounds
        });
    }
}
