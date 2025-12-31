using Microsoft.AspNetCore.Mvc;
using Florique.Api.Services;
using Florique.Api.Models;

namespace Florique.Api.Controllers;

[ApiController]
[Route("api")]
public class JobsController : ControllerBase
{
    private readonly DatabaseService _databaseService;
    private readonly JobProcessingService _jobProcessingService;
    private readonly ILogger<JobsController> _logger;

    public JobsController(
        DatabaseService databaseService,
        JobProcessingService jobProcessingService,
        ILogger<JobsController> logger)
    {
        _databaseService = databaseService;
        _jobProcessingService = jobProcessingService;
        _logger = logger;
    }

    /// <summary>
    /// Register a device token for push notifications
    /// </summary>
    [HttpPost("register-device")]
    public async Task<ActionResult<ApiResponse<bool>>> RegisterDevice([FromBody] RegisterDeviceTokenRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.UserId))
        {
            return BadRequest(new ApiResponse<bool>
            {
                Success = false,
                Message = "UserId is required"
            });
        }

        if (string.IsNullOrWhiteSpace(request.FirebaseToken))
        {
            return BadRequest(new ApiResponse<bool>
            {
                Success = false,
                Message = "FirebaseToken is required"
            });
        }

        var result = await _databaseService.RegisterDeviceTokenAsync(
            request.UserId,
            request.FirebaseToken,
            request.Platform);

        if (result)
        {
            _logger.LogInformation("Device token registered for user {UserId}", request.UserId);
            return Ok(new ApiResponse<bool>
            {
                Success = true,
                Message = "Device token registered successfully",
                Data = true
            });
        }

        return StatusCode(500, new ApiResponse<bool>
        {
            Success = false,
            Message = "Failed to register device token"
        });
    }

    /// <summary>
    /// Start an async image enhancement job
    /// </summary>
    [HttpPost("enhance")]
    public async Task<ActionResult<ApiResponse<StartEnhancementJobResponse>>> StartEnhancement([FromBody] StartEnhancementJobRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.UserId))
        {
            return BadRequest(new ApiResponse<StartEnhancementJobResponse>
            {
                Success = false,
                Message = "UserId is required"
            });
        }

        if (string.IsNullOrWhiteSpace(request.ImagePath))
        {
            return BadRequest(new ApiResponse<StartEnhancementJobResponse>
            {
                Success = false,
                Message = "ImagePath (base64 image) is required"
            });
        }

        if (string.IsNullOrWhiteSpace(request.BackgroundStyle))
        {
            return BadRequest(new ApiResponse<StartEnhancementJobResponse>
            {
                Success = false,
                Message = "BackgroundStyle is required"
            });
        }

        try
        {
            // Check user has credits
            var credits = await _databaseService.GetCreditsAsync(request.UserId);
            if (!credits.HasValue || credits.Value < 1)
            {
                return BadRequest(new ApiResponse<StartEnhancementJobResponse>
                {
                    Success = false,
                    Message = "Insufficient credits"
                });
            }

            // Deduct 1 credit
            await _databaseService.UpdateCreditsAsync(request.UserId, -1);

            // Start the job
            var jobId = await _jobProcessingService.StartJobAsync(
                request.UserId,
                request.ImagePath,
                request.BackgroundStyle,
                request.DeviceToken);

            _logger.LogInformation("Started enhancement job {JobId} for user {UserId}", jobId, request.UserId);

            return Ok(new ApiResponse<StartEnhancementJobResponse>
            {
                Success = true,
                Message = "Enhancement job started",
                Data = new StartEnhancementJobResponse
                {
                    JobId = jobId,
                    Status = "processing",
                    EstimatedTime = 60 // 60 seconds estimate
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting enhancement job for user {UserId}", request.UserId);
            return StatusCode(500, new ApiResponse<StartEnhancementJobResponse>
            {
                Success = false,
                Message = "Failed to start enhancement job"
            });
        }
    }

    /// <summary>
    /// Get the status of an enhancement job
    /// </summary>
    [HttpGet("jobs/{jobId}/status")]
    public async Task<ActionResult<ApiResponse<JobStatusResponse>>> GetJobStatus(string jobId)
    {
        if (string.IsNullOrWhiteSpace(jobId))
        {
            return BadRequest(new ApiResponse<JobStatusResponse>
            {
                Success = false,
                Message = "JobId is required"
            });
        }

        var job = await _databaseService.GetJobAsync(jobId);

        if (job == null)
        {
            return NotFound(new ApiResponse<JobStatusResponse>
            {
                Success = false,
                Message = "Job not found"
            });
        }

        return Ok(new ApiResponse<JobStatusResponse>
        {
            Success = true,
            Data = new JobStatusResponse
            {
                JobId = job.JobId,
                Status = job.Status,
                Progress = job.Progress
            }
        });
    }

    /// <summary>
    /// Get the result of a completed enhancement job
    /// </summary>
    [HttpGet("jobs/{jobId}/result")]
    public async Task<ActionResult<ApiResponse<JobResultResponse>>> GetJobResult(string jobId)
    {
        if (string.IsNullOrWhiteSpace(jobId))
        {
            return BadRequest(new ApiResponse<JobResultResponse>
            {
                Success = false,
                Message = "JobId is required"
            });
        }

        var job = await _databaseService.GetJobAsync(jobId);

        if (job == null)
        {
            return NotFound(new ApiResponse<JobResultResponse>
            {
                Success = false,
                Message = "Job not found"
            });
        }

        if (job.Status == "processing")
        {
            return Ok(new ApiResponse<JobResultResponse>
            {
                Success = false,
                Message = "Job is still processing",
                Data = new JobResultResponse
                {
                    JobId = job.JobId,
                    Status = job.Status
                }
            });
        }

        if (job.Status == "failed")
        {
            return Ok(new ApiResponse<JobResultResponse>
            {
                Success = false,
                Message = job.ErrorMessage ?? "Job failed",
                Data = new JobResultResponse
                {
                    JobId = job.JobId,
                    Status = job.Status,
                    ErrorMessage = job.ErrorMessage
                }
            });
        }

        // Job completed successfully
        byte[]? imageData = null;
        if (!string.IsNullOrEmpty(job.EnhancedImageBase64))
        {
            try
            {
                imageData = Convert.FromBase64String(job.EnhancedImageBase64);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error converting base64 image for job {JobId}", jobId);
            }
        }

        return Ok(new ApiResponse<JobResultResponse>
        {
            Success = true,
            Message = "Job completed successfully",
            Data = new JobResultResponse
            {
                JobId = job.JobId,
                Status = job.Status,
                ImageData = imageData,
                BackgroundStyle = job.BackgroundStyle
            }
        });
    }
}

/// <summary>
/// Standard API response wrapper
/// </summary>
public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public T? Data { get; set; }
}

/// <summary>
/// Request models
/// </summary>
public class RegisterUserRequest
{
    public string UserId { get; set; } = string.Empty;
    public string? DeviceType { get; set; }
    public string? IpAddress { get; set; }
    public string? Location { get; set; }
}

public class UpdateCreditsRequest
{
    public string UserId { get; set; } = string.Empty;
    public int Amount { get; set; }
}
