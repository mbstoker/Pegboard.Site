using PegboardWebSite.Services;
using Serilog;

namespace PegboardWebSite;

public class Program
{
    public static void Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .WriteTo.File("Logs/log-.txt", rollingInterval: RollingInterval.Day) // Daily log files
            .CreateLogger();

        var builder = WebApplication.CreateBuilder(args);
        
        // Replace built-in logging with Serilog
        builder.Host.UseSerilog();

        // Add services to the container.
        builder.Services.AddTransient<TrackedRequestRepository>();
        builder.Services.AddTransient<EmailService>();
        builder.Services.AddRazorPages();
        builder.Services.AddDistributedMemoryCache();
        builder.Services.AddSession();

        builder.Services.AddControllersWithViews();

        var app = builder.Build();
        app.UseSession();
        app.MapControllerRoute(
            name: "default",
            pattern: "{controller=Home}/{action=Index}/{id?}");

        // Configure the HTTP request pipeline.
        if (!app.Environment.IsDevelopment())
        {
             app.UseExceptionHandler("/Error");
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UseStaticFiles();

        app.UseRouting();

        // Resolve the visitor's display currency early (before any view renders) so a
        // ?ccy= choice can be persisted as a cookie before the response starts. See
        // Services/CurrencyResolver. Cheap, runs only for non-static requests.
        app.Use(async (ctx, next) =>
        {
            Services.CurrencyResolver.Get(ctx);
            await next();
        });

        app.UseAuthorization();

        // Legacy /demo path used in older outreach copy → permanent redirect to instant-demo flow on the app.
        // ASP.NET routing matches both /demo and /demo/ against this single registration.
        app.MapGet("/demo", () => Results.Redirect("https://play.epegboard.com/instant-demo", permanent: true));

        app.MapRazorPages();

        app.Run();
    }
}
