using FileService.Worker.Services;
using FileService.Worker.Workers;
using Minio;
using Shared.Configuration;

var builder = Host.CreateApplicationBuilder(args);

// Configuration
builder.Services.Configure<MinioSettings>(builder.Configuration.GetSection(StorageSettings.SectionName));
builder.Services.Configure<RabbitMqSettings>(builder.Configuration.GetSection(RabbitMqSettings.SectionName));

// MinIO
builder.Services.AddMinio(configureClient => configureClient
    .WithEndpoint(builder.Configuration["Storage:Endpoint"]!)
    .WithCredentials(
        builder.Configuration["Storage:AccessKey"]!,
        builder.Configuration["Storage:SecretKey"]!)
    .WithSSL(builder.Configuration.GetValue<bool>("Storage:UseSSL"))
    .WithHttpClient(new HttpClient(new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
    }))
    .Build());

// Services
builder.Services.AddScoped<IMinioService, MinioService>();
builder.Services.AddSingleton<IRabbitMqService, RabbitMqService>();
builder.Services.AddScoped<IFFmpegService, FFmpegService>();
builder.Services.AddScoped<IVideoProcessingService, VideoProcessingService>();

// Workers
builder.Services.AddHostedService<VideoProcessingWorker>();

var host = builder.Build();
host.Run();
