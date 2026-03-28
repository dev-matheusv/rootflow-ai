var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapGet("/", () => Results.Ok(new
{
    name = "RootFlow API",
    status = "ready",
    environment = app.Environment.EnvironmentName
}))
.WithName("GetApiInfo");

app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy"
}))
.WithName("GetHealth");

app.Run();
