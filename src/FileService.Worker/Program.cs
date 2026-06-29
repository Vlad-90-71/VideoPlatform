using FileService.Worker.Services;
using FileService.Worker.Workers;
using Shared.Configuration;

var builder = Host.CreateApplicationBuilder(args);

// Configuration
builder.Services.Configure<StorageSettings>(
    builder.Configuration.GetSection(StorageSettings.SectionName));

builder.Services.Configure<RabbitMqSettings>(
    builder.Configuration.GetSection(RabbitMqSettings.SectionName));

builder.Services.Configure<FileServiceSettings>(
    builder.Configuration.GetSection("FileService"));

// ✅ HTTP клиенты
builder.Services.AddHttpClient<IFileServiceClient, FileServiceClient>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHttpClient<IPresignedHttpClient, PresignedHttpClient>(client =>
{
    client.Timeout = TimeSpan.FromMinutes(10);
});

// Services
builder.Services.AddScoped<IFFmpegService, FFmpegService>();
builder.Services.AddScoped<IVideoProcessingService, VideoProcessingService>();
builder.Services.AddSingleton<IRabbitMqService, RabbitMqService>();
builder.Services.AddHostedService<VideoProcessingWorker>();

var host = builder.Build();
host.Run();