using Microsoft.OpenApi.Models;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "LessonService API",
        Version = "v1",
        Description = "API для управления уроками в VideoPlatform",
        Contact = new OpenApiContact
        {
            Name = "VideoPlatform Team",
            Email = "support@videoplatform.local"
        }
    });

    // Включить XML-комментарии
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }
});

var app = builder.Build();

// Configure middleware
if (app.Environment.IsDevelopment())
{
    // Настроить Swagger для работы за reverse proxy
   /* app.UseSwagger(c =>
    {
        c.PreSerializeFilters.Add((swagger, httpReq) =>
        {
            // Указать правильный сервер для Swagger UI
            swagger.Servers = new List<OpenApiServer>
            {
                new OpenApiServer
                {
                    Url = $"https://{httpReq.Host.Value}",
                    Description = "Production (via Nginx)"
                }
            };
        });
    });*/
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "LessonService v1");
        c.RoutePrefix = "swagger";
    });
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();