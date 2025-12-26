using Microsoft.AspNetCore.Mvc;
using Florique.Api.Services;
using Florique.Api.Models;

namespace Florique.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly DatabaseService _databaseService;
    private readonly ILogger<UsersController> _logger;

    public UsersController(DatabaseService databaseService, ILogger<UsersController> logger)
    {
        _databaseService = databaseService;
        _logger = logger;
    }

    /// <summary>
    /// Register a new user
    /// </summary>
    [HttpPost("register")]
    public async Task<ActionResult<ApiResponse<bool>>> RegisterUser([FromBody] RegisterUserRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.UserId))
        {
            return BadRequest(new ApiResponse<bool>
            {
                Success = false,
                Message = "UserId is required"
            });
        }

        var result = await _databaseService.RegisterUserAsync(
            request.UserId,
            request.DeviceType,
            request.IpAddress,
            request.Location);

        if (result)
        {
            return Ok(new ApiResponse<bool>
            {
                Success = true,
                Message = "User registered successfully",
                Data = true
            });
        }

        return StatusCode(500, new ApiResponse<bool>
        {
            Success = false,
            Message = "Failed to register user"
        });
    }

    /// <summary>
    /// Get user credits
    /// </summary>
    [HttpGet("{userId}/credits")]
    public async Task<ActionResult<ApiResponse<int>>> GetCredits(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return BadRequest(new ApiResponse<int>
            {
                Success = false,
                Message = "UserId is required"
            });
        }

        var credits = await _databaseService.GetCreditsAsync(userId);

        if (credits.HasValue)
        {
            return Ok(new ApiResponse<int>
            {
                Success = true,
                Data = credits.Value
            });
        }

        return NotFound(new ApiResponse<int>
        {
            Success = false,
            Message = "User not found"
        });
    }

    /// <summary>
    /// Update user credits
    /// </summary>
    [HttpPost("credits")]
    public async Task<ActionResult<ApiResponse<bool>>> UpdateCredits([FromBody] UpdateCreditsRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.UserId))
        {
            return BadRequest(new ApiResponse<bool>
            {
                Success = false,
                Message = "UserId is required"
            });
        }

        var result = await _databaseService.UpdateCreditsAsync(request.UserId, request.Amount);

        if (result)
        {
            return Ok(new ApiResponse<bool>
            {
                Success = true,
                Message = "Credits updated successfully",
                Data = true
            });
        }

        return StatusCode(500, new ApiResponse<bool>
        {
            Success = false,
            Message = "Failed to update credits"
        });
    }

    /// <summary>
    /// Get user information including credits, creation date, device type, IP address, and location
    /// </summary>
    [HttpGet("{userId}")]
    public async Task<ActionResult<ApiResponse<UserDto>>> GetUser(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return BadRequest(new ApiResponse<UserDto>
            {
                Success = false,
                Message = "UserId is required"
            });
        }

        var user = await _databaseService.GetUserAsync(userId);

        if (user != null)
        {
            return Ok(new ApiResponse<UserDto>
            {
                Success = true,
                Data = new UserDto
                {
                    UserId = user.UserId,
                    Credits = user.Credit ?? 0,
                    CreatedDate = user.CreatedDate,
                    DeviceType = user.DeviceType,
                    IpAddress = user.IpAddress,
                    Location = user.Location
                }
            });
        }

        return NotFound(new ApiResponse<UserDto>
        {
            Success = false,
            Message = "User not found"
        });
    }
}
