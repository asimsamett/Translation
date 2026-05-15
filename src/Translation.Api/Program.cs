using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Identity.Web;
using Translation.Api.Auth;
using Translation.Core;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddTranslationCore();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var azureAdSection = builder.Configuration.GetSection("AzureAd");
var azureAdConfigured = !string.IsNullOrWhiteSpace(azureAdSection["TenantId"]);

if (azureAdConfigured)
{
    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddMicrosoftIdentityWebApi(azureAdSection);
}
else
{
    builder.Services.AddAuthentication("Dev")
        .AddScheme<AuthenticationSchemeOptions, DevAuthenticationHandler>("Dev", _ => { });
}

builder.Services.AddAuthorization();

var corsOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:5173"];
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(corsOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
});

var app = builder.Build();

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        if (!context.Request.Path.StartsWithSegments("/api"))
            return;

        var feature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
        var exception = feature?.Error;

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/problem+json";

        await context.Response.WriteAsJsonAsync(new
        {
            title = "İşlem tamamlanamadı",
            detail = exception?.Message ?? "Sunucuda beklenmeyen bir hata oluştu.",
            status = StatusCodes.Status500InternalServerError
        });
    });
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options => options.RoutePrefix = "swagger");
}

app.UseHttpsRedirection();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseCors();
app.UseAuthentication();

if (!azureAdConfigured)
{
    app.Use(async (context, next) =>
    {
        var result = await context.AuthenticateAsync("Dev");
        if (result.Succeeded && result.Principal is not null)
            context.User = result.Principal;
        await next();
    });
}

app.UseAuthorization();
app.MapControllers();
app.MapFallbackToFile("index.html");
app.Run();
