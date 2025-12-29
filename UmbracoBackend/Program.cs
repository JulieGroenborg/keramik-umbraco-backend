using DotNetEnv;       // For .env support
using Stripe;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services; // Adds support for IContentService
using UmbracoBackend.Handlers; 
using UmbracoBackend.Helpers;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// --- 1️⃣ KONFIGURATION AF MILJØVARIABLER ---

// Vi finder den præcise sti til .env filen i projekt-roden.
// Directory.GetCurrentDirectory() sikrer, at vi kigger i roden selvom appen kører fra bin-mappen.
var envPath = Path.Combine(Directory.GetCurrentDirectory(), ".env");
if (System.IO.File.Exists(envPath))
{
    DotNetEnv.Env.Load(envPath);
}

// Dette overfører variablerne fra DotNetEnv til .NET's interne konfigurations-system.
// Uden denne linje vil dine controllers (via IConfiguration) se værdierne som 'null'.
builder.Configuration.AddEnvironmentVariables();

// TEST (Valgfri): Bekræfter i terminalen ved opstart at builder kan se din secret.
// Console.WriteLine($"BUILDER KONFIGURATION TEST: {builder.Configuration["STRIPE_WEBHOOK_SECRET"]}");


// --- 2️⃣ STRIPE GLOBALE INDSTILLINGER ---

// Vi læser den hemmelige Stripe API nøgle direkte fra konfigurationen.
var stripeKey = builder.Configuration["STRIPE_SECRET_KEY"];
if (string.IsNullOrEmpty(stripeKey))
    throw new Exception("FEJL: Stripe secret key mangler i .env filen.");

StripeConfiguration.ApiKey = stripeKey;


// --- 3️⃣ SERVICES & CONTROLLERS ---

builder.Services.AddControllers();

// Singleton sikrer, at der kun findes én instans af StockService i hukommelsen.
builder.Services.AddSingleton<IStockService, StockService>();

// HttpClient bruges til sikre server-til-server kald (f.eks. validering mod Delivery API).
builder.Services.AddHttpClient();


// --- 4️⃣ CORS SIKKERHEDSPOLITIK ---

builder.Services.AddCors(options =>
{
    options.AddPolicy("SecureFrontend", policy =>
    {
        policy.WithOrigins(
                "http://localhost:3000",                         // Lokal udvikling
                "https://keramik-nextjs-frontend.vercel.app"     // Produktion
            )
            .WithHeaders("Content-Type")          
            .WithMethods("GET", "POST");          
    });
});


// --- 5️⃣ UMBRACO OPSÆTNING ---

builder.CreateUmbracoBuilder()
    .AddBackOffice()
    .AddWebsite()
    .AddDeliveryApi()
    .AddComposers()
    .AddNotificationHandler<ContentPublishedNotification, StockNotificationHandler>()
    .Build();

WebApplication app = builder.Build();


// --- 6️⃣ BOOT & MIDDLEWARE ---

await app.BootUmbracoAsync();


if (app.Environment.IsEnvironment("Testing"))
{
    using (var scope = app.Services.CreateScope())
    {
        var contentService = scope.ServiceProvider.GetRequiredService<IContentService>();
        var examineManager = scope.ServiceProvider.GetRequiredService<IExamineManager>(); // Hent denne
        UmbracoBackend.Helpers.DataSeeder.Seed(contentService, examineManager);
    }
}

// CORS skal påføres før controllers for at virke korrekt.
app.UseCors("SecureFrontend");

app.UseUmbraco()
    .WithMiddleware(u =>
    {
        u.UseBackOffice();
        u.UseWebsite();
    })
    .WithEndpoints(u =>
    {
        u.UseInstallerEndpoints();
        u.UseBackOfficeEndpoints();
        u.UseWebsiteEndpoints();
    });

// Mapper API-routes (vigtigt for din StripeWebhookController og StockController).
app.MapControllers();

await app.RunAsync();