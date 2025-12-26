using Florique.Api.Services;
using Florique.Api.Middleware;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel to use PORT environment variable (for Railway/Render)
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

// Add services to the container
builder.Services.AddControllers();

// Add rate limiting
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: partition => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 100,
                QueueLimit = 0,
                Window = TimeSpan.FromMinutes(1)
            }));

    options.RejectionStatusCode = 429; // Too Many Requests
});

// Add Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register Florique services
builder.Services.AddSingleton<ConfigurationService>();
builder.Services.AddSingleton<DatabaseService>();

// Configure CORS to allow mobile app to access the API
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Enable rate limiting (before authentication)
app.UseRateLimiter();

// Enable CORS
app.UseCors("AllowAll");

// Enable device authentication (must be before UseAuthorization)
app.UseDeviceAuthentication();

app.UseAuthorization();

app.MapControllers();

app.Run();
