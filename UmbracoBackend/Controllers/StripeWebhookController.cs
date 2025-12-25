using Microsoft.AspNetCore.Mvc;
using Stripe;
using Stripe.Checkout;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Web;
using System.Threading;
using Microsoft.Extensions.Configuration;

namespace server.Controllers
{
    /// Denne controller fungerer som et "Webhook" endpoint. Stripe sender automatiske beskeder herhen, når en begivenhed sker i deres system (f.eks. en fuldført betaling).
    /// </summary>
    [Route("stripe-api/webhook")]
    [ApiController]
    public class StripeWebhookController : ControllerBase
    {
        private readonly IContentService _contentService;
        private readonly IUmbracoContextFactory _contextFactory;
        private readonly IConfiguration _configuration;

        // --- SIKKERHED: LAGERSTYRING ---
        // SemaphoreSlim(1, 1) sikrer "Thread Safety". Hvis to kunder køber den sidste vare præcis samtidig, sørger denne lock for, at de ikke begge når at trække fra lageret, før den første har opdateret værdien. Det forhindrer race conditions:
        private static readonly SemaphoreSlim _inventoryLock = new SemaphoreSlim(1, 1);

        public StripeWebhookController(
            IContentService contentService, 
            IUmbracoContextFactory contextFactory, 
            IConfiguration configuration)
        {
            _contentService = contentService;
            _contextFactory = contextFactory;
            _configuration = configuration;
        }

        [HttpPost]
        public async Task<IActionResult> Index()
        {
            // Vi læser den rå JSON fra Stripe-kaldet
            var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
            
            // Henter den hemmelige Webhook-nøgle fra .env (bruges til at verificere at kaldet rent faktisk kommer fra Stripe)
            var webhookSecret = _configuration["STRIPE_WEBHOOK_SECRET"];
            
            try
            {
                // Sikkerhedstjek: Vi verificerer signaturen for at sikre, at data ikke er blevet manipuleret.
                var stripeEvent = EventUtility.ConstructEvent(
                    json,
                    Request.Headers["Stripe-Signature"],
                    webhookSecret
                );

                // Vi reagerer kun, når en betaling er fuldført (Checkout Session Completed)
                if (stripeEvent.Type == "checkout.session.completed")
                {
                    if (stripeEvent.Data.Object is Session session && !string.IsNullOrEmpty(session.Id))
                    {
                        var service = new SessionLineItemService();
                        var options = new SessionLineItemListOptions();
                        
                        // Vi "expander" produkt-dataen, så vi kan læse den metadata (umbracoId), vi gemte i Session-oprettelsen.
                        options.AddExpand("data.price.product"); 
                        
                        var lineItems = service.List(session.Id, options);

                        // Vi aktiverer vores lager-lock her, før vi begynder at rette i Umbraco-noderne.
                        await _inventoryLock.WaitAsync();
                        try 
                        {
                            foreach (var item in lineItems)
                            {
                                // session.PaymentIntentId skal bruges, hvis vi bliver nødt til at lave en automatisk refundering.
                                await UpdateUmbracoStock(item, session.PaymentIntentId);
                            }
                        }
                        finally 
                        {
                            // VIGTIGT: Frigiv altid locken, uanset om opdateringen lykkedes eller fejlede.
                            _inventoryLock.Release();
                        }
                    }
                }

                return Ok(); // Vi sender 200 OK tilbage til Stripe, så de ved, vi har modtaget beskeden.
            }
            catch (Exception e)
            {
                Console.WriteLine($"Webhook Error: {e.Message}");
                return BadRequest(); // Ved fejl sender vi 400, så Stripe ved, de skal prøve igen senere.
            }
        }

        /// <summary>
        /// Opdaterer lageret i Umbraco baseret på de købte varer.
        /// </summary>
        private async Task UpdateUmbracoStock(LineItem item, string paymentIntentId)
        {
            var productService = new ProductService();
            var product = await productService.GetAsync(item.Price.ProductId);

            // Vi finder vores Umbraco GUID, som blev gemt i Stripes metadata under checkout-oprettelsen.
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
                        // Hvis lageret er blevet tomt siden kunden startede deres betaling (f.eks. ved langsom betaling), så refunderer vi automatisk pengene i stedet for at sælge en vare, vi ikke har.
                        if (currentStock < quantityBought)
                        {
                            Console.WriteLine($"ADVARSEL: Oversalg af {content.Name}! Refunderer nu...");
                            
                            var refundOptions = new RefundCreateOptions
                            {
                                PaymentIntent = paymentIntentId, 
                                Reason = RefundReasons.RequestedByCustomer
                            };
                            
                            var refundService = new RefundService();
                            await refundService.CreateAsync(refundOptions);
                            
                            return; // Vi stopper her, så lageret ikke bliver negativt.
                        }

                        // --- NORMAL LAGEROPDATERING ---
                        int newStock = currentStock - quantityBought;
                        content.SetValue("stockQuantity", newStock);

                        // Gemmer og udgiver ændringen i Umbraco. 
                        // Dette vil også trigge en ContentPublishedNotification, som sender SSE-beskeder til frontenden.
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