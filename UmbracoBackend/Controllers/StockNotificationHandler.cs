using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using UmbracoBackend.Helpers;

namespace UmbracoBackend.Handlers
{
    // Denne klasse er en "Lytter". Den får besked fra Umbraco, hver gang noget bliver udgivet.
    public class StockNotificationHandler : INotificationHandler<ContentPublishedNotification>
    {
        private readonly IStockService _stockService;

        // Her kobler vi os på vores StockService (vores radiostation).
        public StockNotificationHandler(IStockService stockService)
        {
            _stockService = stockService;
        }

        // Denne funktion kører automatisk, hver gang der er en lagerændring fra Umbraco.
        public void Handle(ContentPublishedNotification notification)
        {
            // looper for at gå igennem alle de udgivne noder
            foreach (var node in notification.PublishedEntities)
            {
                // her tjekker vi om det her et produkt? (Alias skal matche det i Umbraco).
                if (node.ContentType.Alias == "product")
                {
                    // vi tjekker om produktet har en lager property
                    if (node.HasProperty("stockQuantity"))
                    {
                        // vi henter produkt ID og lagerantal
                        var productGuid = node.Key;
                        int nytLager = node.GetValue<int>("stockQuantity");
                        
                        // vi sender lageropdateringen videre til StockService
                        _stockService.SendUpdate(productGuid, nytLager);
                    }
                }
            }
        }
    }
}