using FileService.Services;
using Minio;
using Shared.Configuration;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "FileService API", Version = "v1" });
});

// Configuration
builder.Services.Configure<MinioSettings>(builder.Configuration.GetSection(MinioSettings.SectionName));
builder.Services.Configure<RabbitMqSettings>(builder.Configuration.GetSection(RabbitMqSettings.SectionName));
// MinIO
builder.Services.AddMinio(configureClient => configureClient
    .WithEndpoint(builder.Configuration.GetValue<string>("Minio:Endpoint")!)
    .WithCredentials(
        builder.Configuration.GetValue<string>("Minio:AccessKey")!,
        builder.Configuration.GetValue<string>("Minio:SecretKey")!)
    .WithSSL(builder.Configuration.GetValue<bool>("Minio:WithSSL"))
    .Build());

// Services
builder.Services.AddScoped<IMinioService, MinioService>();
builder.Services.AddSingleton<IRabbitMqService, RabbitMqService>();
builder.Services.AddScoped<IVideoService, VideoService>();

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
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();

app.Run();
