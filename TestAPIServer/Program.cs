using TestAPIServer.Services;
using TestAPIServer.Filters;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers(options =>
{
    options.Filters.Add<ApiLoggingActionFilter>();
});
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// 태그 매핑 서비스 등록 (Singleton으로 변경 - OpcUaService가 Singleton이므로)
builder.Services.AddSingleton<ITagMappingService, TagMappingService>();

// OPC UA 서비스 등록 (Singleton으로 등록하여 하나의 연결을 유지)
builder.Services.AddSingleton<IOpcUaService, OpcUaService>();

// CORS 설정 (WinForm 및 Blazor 클라이언트에서 접근 가능하도록)
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
    app.MapOpenApi();
}

app.UseCors("AllowAll");

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
