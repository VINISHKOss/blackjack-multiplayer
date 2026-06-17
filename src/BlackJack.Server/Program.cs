using BlackJack.Server.Hubs;
using BlackJack.Server.Services;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

builder.Services.AddSingleton<BlackjackRulesService>();
builder.Services.AddSingleton<TableStateMapper>();
builder.Services.AddSingleton<GameService>();
builder.Services.AddSignalR();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
});

var app = builder.Build();

app.UseForwardedHeaders();

if (app.Environment.IsDevelopment())
    app.UseWebAssemblyDebugging();

app.UseRouting();
app.UseCors();
app.UseBlazorFrameworkFiles();
app.UseStaticFiles();
app.MapStaticAssets();

app.MapHub<GameHub>("/gamehub");
app.MapFallbackToFile("index.html");

app.Run();