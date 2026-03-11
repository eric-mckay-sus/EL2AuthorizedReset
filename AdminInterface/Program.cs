using AdminInterface.Components;
using Microsoft.EntityFrameworkCore;
using AdminInterface;
using Microsoft.AspNetCore.Components.Authorization;

var builder = WebApplication.CreateBuilder(args);

// Database Configuration
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContextFactory<AuthResetDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddScoped<IUserIdentityService, UserIdentityService>();

// Authentication & Authorization
builder.Services.AddAuthentication("AutoAuth")
    .AddAutoAuthentication();

builder.Services.AddAuthorization(); // Required for attribute-based security
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

/* --- STATUS CODE HANDLING --- */
// This tells the server: "If you see a 401 or 403, don't tell the browser yet. 
// Re-run the pipeline at the root path so Blazor can load and handle it."
app.UseStatusCodePagesWithReExecute("/"); 

app.UseAntiforgery();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
