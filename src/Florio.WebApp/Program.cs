using System.Threading.RateLimiting;

using Florio.VectorEmbeddings.Extensions;
using Florio.VectorEmbeddings.Qdrant;
using Florio.WebApp.HealthChecks;
using Florio.WebApp.Settings;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.AddServiceDefaults();

builder.Services.AddGutenbergDownloaderAndParser();
builder.Services.AddVectorEmbeddings<QdrantRepository>();

builder.AddQdrantClient("qdrant");
builder.Services.AddHealthChecks()
    .AddCheck<VectorDatabaseHealthCheck>("Vector Database", tags: ["live"]);

builder.Services.AddRazorPages();

builder.Services.Configure<RateLimitSettings>(
    builder.Configuration.GetSection(nameof(RateLimitSettings)));

var rateLimitSettings = new RateLimitSettings();
builder.Configuration.GetSection(nameof(RateLimitSettings)).Bind(rateLimitSettings);

builder.Services.AddRateLimiter(limiterOptions =>
    limiterOptions.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
    {
        var userAgent = httpContext.Request.Headers.UserAgent.ToString();

        return RateLimitPartition.GetFixedWindowLimiter(userAgent, _ =>
            new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = rateLimitSettings.PermitLimit,
                Window = TimeSpan.FromSeconds(rateLimitSettings.WindowSeconds),
                QueueProcessingOrder = rateLimitSettings.QueueProcessingOrder,
                QueueLimit = rateLimitSettings.QueueLimit,
            }
        );
    }));

builder.Services.AddOutputCache(options =>
{
    options.AddBasePolicy(builder => builder.Expire(TimeSpan.FromMinutes(5)));
});
builder.Services.AddResponseCaching();

builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseResponseCaching();
app.UseResponseCompression();

app.UseRouting();
app.UseRateLimiter();

app.UseAuthorization();

app.MapRazorPages();

app.Run();
