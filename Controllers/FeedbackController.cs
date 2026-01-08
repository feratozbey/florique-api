using Microsoft.AspNetCore.Mvc;
using Florique.Api.Services;
using Florique.Api.Models;

namespace Florique.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FeedbackController : ControllerBase
{
    private readonly DatabaseService _databaseService;
    private readonly ILogger<FeedbackController> _logger;

    public FeedbackController(DatabaseService databaseService, ILogger<FeedbackController> logger)
    {
        _databaseService = databaseService;
        _logger = logger;
    }

    /// <summary>
    /// Submit user feedback
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<bool>>> SubmitFeedback([FromBody] SubmitFeedbackRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.UserId))
        {
            return BadRequest(new ApiResponse<bool>
            {
                Success = false,
                Message = "UserId is required"
            });
        }

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest(new ApiResponse<bool>
            {
                Success = false,
                Message = "Email is required"
            });
        }

        if (string.IsNullOrWhiteSpace(request.FeedbackText))
        {
            return BadRequest(new ApiResponse<bool>
            {
                Success = false,
                Message = "Feedback text is required"
            });
        }

        var result = await _databaseService.SubmitFeedbackAsync(
            request.UserId,
            request.Email,
            request.FeedbackText);

        if (result)
        {
            return Ok(new ApiResponse<bool>
            {
                Success = true,
                Message = "Feedback submitted successfully",
                Data = true
            });
        }

        return StatusCode(500, new ApiResponse<bool>
        {
            Success = false,
            Message = "Failed to submit feedback"
        });
    }
}
