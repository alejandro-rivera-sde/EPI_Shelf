using Microsoft.AspNetCore.Connections;
using Microsoft.OpenApi.Models;
using EPI_Shel.Data;
using EPI_Shel.Models;
using EPI_Shel.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Swagger configuration
builder.Services.AddSwaggerGen(c => {
    c.SwaggerDoc("v1", new OpenApiInfo {
        Title = "Epi_Shel API",
        Version = "v2",
        Description = ""
    });
    c.SchemaFilter<EnumMemberSchemaFilter>();
    c.UseInlineDefinitionsForEnums();
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
        c.IncludeXmlComments(xmlPath);
});

// Register services
builder.Services.AddSingleton<IDbConnection, SqlServerConnection>();
builder.Services.AddScoped<IProductService, ProductService>();

var app = builder.Build();

// Always show Swagger
app.UseSwagger();
app.UseSwaggerUI(c => {
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Epi_Shel API v2");
    c.RoutePrefix = string.Empty; // Swagger at root
    c.EnableTryItOutByDefault();
});

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();
