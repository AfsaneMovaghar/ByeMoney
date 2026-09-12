using ByeMoney.API;
using ByeMoney.API.Constants;
using ByeMoney.Application;
using ByeMoney.Infrastructure;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(outputTemplate: "{UtcTimestamp:yyyy-MM-dd HH:mm:ss.fff} UTC [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting web application...");

    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console(outputTemplate: "{UtcTimestamp:yyyy-MM-dd HH:mm:ss.fff} UTC [{Level:u3}] {Message:lj}{NewLine}{Exception}")
        .WriteTo.File(
            path: "logs/log-.txt",
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 14,
            outputTemplate: "{UtcTimestamp:yyyy-MM-dd HH:mm:ss.fff} UTC [{Level:u3}] {Message:lj}{NewLine}{Exception}"));

    builder.Services.AddApplicationServices();
    builder.Services.AddInfrastructureServices(builder.Configuration);
    builder.Services.AddPresentationServices(builder.Configuration);

    var app = builder.Build();
    app.UseMiddleware<ByeMoney.API.Middleware.GlobalExceptionHandlingMiddleware>();
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }
    app.UseCors(CorsPolicies.TarhElahi);

    if (!app.Environment.IsDevelopment())
    {
        app.UseHttpsRedirection();
    }

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}