using Florio.VectorEmbeddings.Extensions;
using Florio.VectorEmbeddings.Qdrant;
using Florio.WebApp.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.AddServiceDefaults();

builder.Services.AddGutenbergDownloaderAndParser();
builder.Services.AddVectorEmbeddings<QdrantRepository>();

builder.AddQdrantClient("qdrant");
builder.Services.AddHealthChecks()
    .AddCheck<VectorDatabaseHealthCheck>("Vector Database", tags: ["live"]);

builder.Services.AddRazorPages();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

// TODO: add rate limiting

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapRazorPages();

app.Run();
