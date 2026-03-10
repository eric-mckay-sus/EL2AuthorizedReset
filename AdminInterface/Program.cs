using AdminInterface.Components;
using Microsoft.EntityFrameworkCore;
using AdminInterface;
using Microsoft.AspNetCore.Components.Authorization;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection"); // from appsettings.json, no idea how this looks in production

builder.Services.AddDbContextFactory<AuthResetDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddAuthentication("CustomAutoAuth")
    .AddCookie("CustomAutoAuth", options =>
    {
        options.LoginPath = "/"; // If not logged in at all, go here
        options.AccessDeniedPath = "/"; // If logged in but not Admin, go here
        
        // This prevents the Blazor default redirect to /Account/Login
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.Redirect("/?error=unauthorized");
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.Redirect("/?error=forbidden");
            return Task.CompletedTask;
        };
    });
    
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

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
