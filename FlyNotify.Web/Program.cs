using FlyNotify.Models;
using FlyNotify.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddSingleton<FlightService>();
builder.Services.AddSingleton<SchedulerWorker>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<SchedulerWorker>());

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseDefaultFiles();
app.UseStaticFiles();

// REST API Endpoints

app.MapGet("/api/profiles", (FlightService service) =>
{
    return Results.Ok(service.GetProfiles());
});

app.MapPost("/api/profiles", (FlightProfile profile, FlightService service) =>
{
    if (string.IsNullOrWhiteSpace(profile.DepartureAirport) || string.IsNullOrWhiteSpace(profile.ArrivalAirport))
    {
        return Results.BadRequest("Departure and Arrival airports are required.");
    }
    if (profile.TravelDate == DateTime.MinValue)
    {
        return Results.BadRequest("Invalid travel date.");
    }
    if (profile.TravelEndDate == DateTime.MinValue || profile.TravelEndDate < profile.TravelDate)
    {
        profile.TravelEndDate = profile.TravelDate;
    }
    
    // Ensure status defaults if not provided
    if (string.IsNullOrEmpty(profile.FlightNumber)) profile.FlightNumber = "TBD";
    if (string.IsNullOrEmpty(profile.AvailabilityStatus)) profile.AvailabilityStatus = "TBD";
    if (string.IsNullOrEmpty(profile.DetailedStatus)) profile.DetailedStatus = "TBD";

    service.AddProfile(profile);
    SystemLog.Log($"Added flight profile: {profile.DepartureAirport} -> {profile.ArrivalAirport} ({profile.TravelDate:yyyy-MM-dd})");
    return Results.Ok(new { success = true });
});

app.MapDelete("/api/profiles", (string departure, string arrival, string travelDate, string flightNumber, FlightService service) =>
{
    service.DeleteProfile(departure, arrival, travelDate, flightNumber);
    SystemLog.Log($"Deleted flight profile: {departure} -> {arrival} ({travelDate})");
    return Results.Ok(new { success = true });
});

app.MapPost("/api/scrape-now", (bool isLive, SchedulerWorker worker) =>
{
    SystemLog.Log($"On-demand batch query triggered (Live: {isLive})");
    _ = Task.Run(async () =>
    {
        await worker.RunBatchQueryAsync(isLive, CancellationToken.None);
    });
    return Results.Ok(new { message = "Scan initiated in background." });
});

app.MapGet("/api/logs", () =>
{
    return Results.Ok(SystemLog.GetLogs());
});

app.Run();
