using WebApp.Hubs;
using WebApp.Services;
using WebApp.Workers;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();

// HTTP Clients
builder.Services.AddHttpClient<IFileServiceClient, FileServiceClient>(client =>
{
    var baseUrl = builder.Configuration["FileService:BaseUrl"];
    Console.WriteLine($"FileService BaseUrl: {baseUrl}"); // Для отладки
    client.BaseAddress = new Uri(baseUrl ?? "http://fileservice:8080");
});

builder.Services.AddHttpClient<ILessonServiceClient, LessonServiceClient>(client =>
{
    var baseUrl = builder.Configuration["LessonService:BaseUrl"];
    Console.WriteLine($"LessonService BaseUrl: {baseUrl}"); // Для отладки
    client.BaseAddress = new Uri(baseUrl ?? "http://lessonservice:8080");
});

builder.Services.AddScoped<IVideoService, VideoService>();
builder.Services.AddSingleton<IVideoProgressCache, VideoProgressCache>();

// Background Worker
builder.Services.AddHostedService<VideoProgressWorker>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapControllers();
app.MapHub<VideoProgressHub>("/videoProgressHub");

app.Run();
