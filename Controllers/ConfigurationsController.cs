using Florique.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Florique.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ConfigurationsController : ControllerBase
{
    private readonly ConfigurationService _configService;
    private readonly ILogger<ConfigurationsController> _logger;

    public ConfigurationsController(ConfigurationService configService, ILogger<ConfigurationsController> logger)
    {
        _configService = configService;
        _logger = logger;
    }

    /// <summary>
    /// Gets a configuration value by key
    /// </summary>
    [HttpGet("{key}")]
    public async Task<IActionResult> GetConfiguration(string key)
    {
        try
        {
            var value = await _configService.GetConfigValueAsync(key);

            if (string.IsNullOrEmpty(value))
            {
                return NotFound(new { message = $"Configuration '{key}' not found" });
            }

            return Ok(new { key, value });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving configuration '{Key}'", key);
            return StatusCode(500, new { message = "Error retrieving configuration" });
        }
    }

    /// <summary>
    /// Updates a configuration value
    /// </summary>
    [HttpPut("{key}")]
    public async Task<IActionResult> UpdateConfiguration(string key, [FromBody] UpdateConfigRequest request)
    {
        try
        {
            await _configService.SetConfigValueAsync(key, request.Value, request.IsEncrypted);
            return Ok(new { message = "Configuration updated successfully", key });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating configuration '{Key}'", key);
            return StatusCode(500, new { message = "Error updating configuration" });
        }
    }

    /// <summary>
    /// Clears the configuration cache
    /// </summary>
    [HttpPost("clear-cache")]
    public IActionResult ClearCache()
    {
        try
        {
            _configService.ClearCache();
            return Ok(new { message = "Configuration cache cleared successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing configuration cache");
            return StatusCode(500, new { message = "Error clearing cache" });
        }
    }

    /// <summary>
    /// Gets OpenAI configuration
    /// </summary>
    [HttpGet("openai")]
    public async Task<IActionResult> GetOpenAiConfig()
    {
        try
        {
            var apiKey = await _configService.GetOpenAiApiKeyAsync();
            var endpoint = await _configService.GetOpenAiApiEndpointAsync();
            var model = await _configService.GetOpenAiModelAsync();

            return Ok(new
            {
                apiKey = string.IsNullOrEmpty(apiKey) ? null : "***configured***",
                endpoint,
                model
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving OpenAI configuration");
            return StatusCode(500, new { message = "Error retrieving OpenAI configuration" });
        }
    }
}

public class UpdateConfigRequest
{
    public string Value { get; set; } = string.Empty;
    public bool IsEncrypted { get; set; } = false;
}
