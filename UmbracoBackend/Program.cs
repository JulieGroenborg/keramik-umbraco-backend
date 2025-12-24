using DotNetEnv;       // For .env support
using Stripe;
using Umbraco.Cms.Core.Notifications;
using UmbracoBackend.Handlers; 
using UmbracoBackend.Helpers;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// 1️⃣ Load environment variables from .env
Env.Load(); 

// 2️⃣ Read Stripe secret key and set globally
var stripeKey = Environment.GetEnvironmentVariable("STRIPE_SECRET_KEY");
if (string.IsNullOrEmpty(stripeKey))
    throw new Exception("Stripe secret key is not set in environment variables.");

StripeConfiguration.ApiKey = stripeKey;

// 3️⃣ Register controllers
builder.Services.AddControllers();

// singleton gør, at der kun oprettes én instans af StockService i hele applikationen
builder.Services.AddSingleton<IStockService, StockService>();

// Registrerer IHttpClientFactory. Dette er nødvendigt for at vores controller 
// kan lave sikre server-til-server kald til Umbraco Delivery API.
// Vi bruger det til at validere priser og navne, så vi ikke stoler på frontenden.
builder.Services.AddHttpClient();

// 4️⃣ Secure CORS policy
builder.Services.AddCors(options =>
{
    options.AddPolicy("SecureFrontend", policy =>
    {
        policy.WithOrigins(
                "http://localhost:3000",                         // Local dev frontend
                "https://keramik-nextjs-frontend.vercel.app"     // Production frontend
            )
            .WithHeaders("Content-Type")          // Allow only needed headers
            .WithMethods("GET", "POST");          // Allow only needed methods (SSE bruger GET)
    });
});

// 5️⃣ Build Umbraco
builder.CreateUmbracoBuilder()
    .AddBackOffice()
    .AddWebsite()
    .AddDeliveryApi()
    .AddComposers()
    .AddNotificationHandler<ContentPublishedNotification, StockNotificationHandler>()
    .Build();

WebApplication app = builder.Build();

// 6️⃣ Boot Umbraco
await app.BootUmbracoAsync();

// 7️⃣ Configure middleware and endpoints
app.UseCors("SecureFrontend");  // Apply CORS **before controllers**

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

// bruges til stock controlleren i forhold til SSE
app.MapControllers();

await app.RunAsync();