using Microsoft.AspNetCore.Mvc;
using Stripe;
using Stripe.Checkout;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Web;
using System.Threading;
using Microsoft.Extensions.Configuration;

namespace server.Controllers
{
    [Route("stripe-api/webhook")]
    [ApiController]
    public class StripeWebhookController : ControllerBase
    {
        private readonly IContentService _contentService;
        private readonly IUmbracoContextFactory _contextFactory;
        private readonly IConfiguration _configuration;

        // --- SIKKERHED ---
        // Semaphore sikrer, at vi kun håndterer én lageropdatering ad gangen (undgår race conditions).
        private static readonly SemaphoreSlim _inventoryLock = new SemaphoreSlim(1, 1);

        public StripeWebhookController(
            IContentService contentService, 
            IUmbracoContextFactory contextFactory, 
            IConfiguration configuration) // Injiceres her
        {
            _contentService = contentService;
            _contextFactory = contextFactory;
            _configuration = configuration;
        }

        [HttpPost]
        public async Task<IActionResult> Index()
        {
            var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
            // Henter nøglen direkte fra .env via IConfiguration
            var webhookSecret = _configuration["STRIPE_WEBHOOK_SECRET"];
            Console.WriteLine($"DEBUG: Min secret er: {webhookSecret ?? "HELT TOM"}");
            
            try
            {
                var stripeEvent = EventUtility.ConstructEvent(
                    json,
                    Request.Headers["Stripe-Signature"],
                    webhookSecret
                );

                // Vi reagerer kun på 'checkout.session.completed'
                if (stripeEvent.Type == "checkout.session.completed")
                {
                    if (stripeEvent.Data.Object is Session session && !string.IsNullOrEmpty(session.Id))
                    {
                        var service = new SessionLineItemService();
                        var options = new SessionLineItemListOptions();
                        options.AddExpand("data.price.product"); // Så vi kan læse metadata
                        
                        var lineItems = service.List(session.Id, options);

                        await _inventoryLock.WaitAsync();
                        try 
                        {
                            foreach (var item in lineItems)
                            {
                                // Vi sender session.PaymentIntentId med, så vi kan refundere hvis nødvendigt
                                await UpdateUmbracoStock(item, session.PaymentIntentId);
                            }
                        }
                        finally 
                        {
                            _inventoryLock.Release();
                        }
                    }
                }

                return Ok();
            }
            catch (Exception e)
            {
                Console.WriteLine($"Webhook Error: {e.Message}");
                return BadRequest();
            }
        }

        private async Task UpdateUmbracoStock(LineItem item, string paymentIntentId)
        {
            var productService = new ProductService();
            var product = await productService.GetAsync(item.Price.ProductId);

            // Tjek om vi har vores Umbraco GUID gemt i Stripes metadata
            if (product.Metadata.TryGetValue("umbracoId", out string? guidString) && !string.IsNullOrEmpty(guidString))
            {
                Guid productGuid = Guid.Parse(guidString);

                using (var contextReference = _contextFactory.EnsureUmbracoContext())
                {
                    var content = _contentService.GetById(productGuid);
                    
                    if (content != null)
                    {
                        int currentStock = content.GetValue<int>("stockQuantity");
                        int quantityBought = (int)(item.Quantity ?? 0);

                        // --- LOGIK FOR REFUNDERING VED OVERSALG ---
                        if (currentStock < quantityBought)
                        {
                            Console.WriteLine($"ADVARSEL: Oversalg af {content.Name}! Lager: {currentStock}. Refunderer nu...");
                            
                            var refundOptions = new RefundCreateOptions
                            {
                                PaymentIntent = paymentIntentId, // ID på selve betalingen
                                Reason = RefundReasons.RequestedByCustomer // Standard årsag
                            };
                            
                            var refundService = new RefundService();
                            await refundService.CreateAsync(refundOptions);
                            
                            Console.WriteLine($"SUCCESS: Beløb for {content.Name} er sendt retur til kunden.");
                            return; // Stop her - vi skal ikke opdatere lageret til et negativt tal
                        }

                        // --- NORMAL LAGEROPDATERING ---
                        int newStock = currentStock - quantityBought;

                        content.SetValue("stockQuantity", newStock);
                        _contentService.SaveAndPublish(content);
                        
                        Console.WriteLine($"SUCCESS: Lager opdateret for {content.Name}. {currentStock} -> {newStock}");
                    }
                }
            }
            else 
            {
                Console.WriteLine($"FEJL: Ingen 'umbracoId' fundet for {item.Description}.");
            }
        }
    }
}