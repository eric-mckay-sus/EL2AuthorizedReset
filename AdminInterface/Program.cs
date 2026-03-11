using AdminInterface.Components;
using Microsoft.EntityFrameworkCore;
using AdminInterface;
using Microsoft.AspNetCore.Components.Authorization;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection"); // from appsettings.json, no idea how this looks in production

builder.Services.AddDbContextFactory<AuthResetDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddScoped<IUserIdentityService, UserIdentityService>();

// Add custom auto-authentication handler
builder.Services.AddAuthentication("AutoAuth")
    .AddAutoAuthentication();

builder.Services.AddAuthenticationCore();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<AuthenticationStateProvider, AutoAuthStateProvider>();

builder.Services.AddBlazorBootstrap();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.UseStatusCodePages(async context =>
{
    var response = context.HttpContext.Response;

    if (response.StatusCode == StatusCodes.Status401Unauthorized)
    {
        context.HttpContext.Response.Redirect("/unauthorized");
    }
    else if (response.StatusCode == StatusCodes.Status403Forbidden)
    {
        context.HttpContext.Response.Redirect("/forbidden");
    }
    await Task.CompletedTask;
});

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
