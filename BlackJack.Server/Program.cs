using BlackJack.Server.Hubs;
using BlackJack.Server.Services;

var builder = WebApplication.CreateBuilder(args);

// одно хранилищк для всех комнат на приложение
builder.Services.AddSingleton<RoomService>();

builder.Services.AddSignalR();

builder.Services.AddCors(options =>
{
    options.AddPolicy("BlazorClient", policy =>
    {
        policy
            .WithOrigins(
                "http://localhost:5212",
                "https://localhost:7121")
            .AllowAnyHeader()
            .WithMethods("GET", "POST", "DELETE")
            .AllowCredentials(); // разрешение передавать учётные данные
    });
});

var app = builder.Build();

app.UseCors("BlazorClient");

app.MapGet("/", () => "BlackJack запущен");


app.MapHub<GameHub>("/gamehub");

app.Run();
