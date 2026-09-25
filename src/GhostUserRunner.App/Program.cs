using System.Diagnostics;
using GhostUserRunner.App.Services;
using GhostUserRunner.Core.Configuration;

var configPath = Path.Combine(AppContext.BaseDirectory, "config", "appsettings.json");
if (!File.Exists(configPath)) throw new FileNotFoundException("Missing config/appsettings.json", configPath);
var options = RunnerOptionsLoader.LoadFile(configPath);
var validation = OptionsValidator.Validate(options);
if (!validation.IsValid) throw new InvalidDataException(string.Join(Environment.NewLine, validation.Errors.Select(error => error.Message)));
var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://127.0.0.1:5000", "http://[::1]:5000");
builder.Services.AddSingleton(options); builder.Services.AddSingleton<SessionService>();
builder.Services.ConfigureHttpJsonOptions(json => json.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
var app = builder.Build();
app.Use(async (context, next) => { if (context.Connection.RemoteIpAddress is not null && !System.Net.IPAddress.IsLoopback(context.Connection.RemoteIpAddress)) { context.Response.StatusCode = 403; return; } context.Response.Headers.CacheControl = "no-store"; await next(); });
app.UseDefaultFiles(); app.UseStaticFiles();
app.MapGet("/api/session/status", (SessionService service) => Results.Ok(service.Status));
app.MapPost("/api/session/run", async (RunRequest request, SessionService service) => { if (request.DurationHours is { } hours && (!double.IsFinite(hours) || hours < 8 || hours > 168)) return Results.BadRequest(new { code = "session.invalid_duration", message = "Duration must be between 8 and 168 hours." }); var result = await service.StartAsync(request.DurationHours is { } value ? TimeSpan.FromHours(value) : null, request.DiagnosticSeed); return result.Accepted ? Results.Accepted(value: result) : Results.Conflict(result); });
app.MapPost("/api/session/pause", async (SessionService service) => ToResult(await service.PauseAsync()));
app.MapPost("/api/session/resume", (SessionService service) => ToResult(service.Resume()));
app.MapPost("/api/session/stop", async (SessionService service) => ToResult(await service.StopAsync()));
var background = args.Contains("--background", StringComparer.OrdinalIgnoreCase);
if (!background) app.Lifetime.ApplicationStarted.Register(() => { try { Process.Start(new ProcessStartInfo("http://localhost:5000") { UseShellExecute = true }); } catch { } });
await app.RunAsync();
static IResult ToResult(SessionCommandResult result) => result.Accepted ? Results.Ok(result) : Results.Conflict(result);
public sealed record RunRequest(double? DurationHours, int? DiagnosticSeed);
public partial class Program { }
