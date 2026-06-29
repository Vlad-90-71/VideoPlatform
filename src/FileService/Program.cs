using Microsoft.OpenApi.Models;
using Shared.Configuration;
using FileService.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "FileService API",
        Version = "v1",
        Description = "API для управления файлами в VideoPlatform",
        Contact = new OpenApiContact
        {
            Name = "VideoPlatform Team",
            Email = "support@videoplatform.local"
        }
    });
});

// Configuration
builder.Services.Configure<StorageSettings>(builder.Configuration.GetSection(StorageSettings.SectionName));

// Services
builder.Services.AddScoped<IPresignedUrlService, PresignedUrlService>();
builder.Services.AddScoped<IObjectStorageService, ObjectStorageService>();

// CORS
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

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "FileService v1");
        c.RoutePrefix = "swagger";
    });
}

//app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();

app.Run();