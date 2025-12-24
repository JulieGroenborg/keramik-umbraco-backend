using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Web.Common.Controllers;
using System.Text;
using UmbracoBackend.Helpers;

namespace UmbracoBackend.Controllers
{
    public class StockController : UmbracoApiController
    {
        private readonly IStockService _stockService;

        public StockController(IStockService stockService)
        {
            _stockService = stockService;
        }

        // Dette er den rute, som browseren skal bruge (kan ses i network i devtools)
        [HttpGet]
        [Route("api/stock/updates")]
        public async Task GetStockUpdates()
        {
            var response = Response;

            // vi sætter de korrekte headers så SSE virker korrekt
            response.Headers["Content-Type"] = "text/event-stream";
            response.Headers["Cache-Control"] = "no-cache";
            response.Headers["Connection"] = "keep-alive";

            // denne variabel tjekker om forbindelsen er afbrudt (brugeren har lukket fanen)
            var afbrudt = HttpContext.RequestAborted;

            // Her definerer vi, hvad der skal ske, når StockService sender et signal.
            Action<Guid, int> handler = (productId, stock) =>
            {
                Task.Run(async () =>
                {
                    try
                    {
                        // vi laver dataen om til json
                        var stockData = new { productId = productId, stock = stock };
                        string json = System.Text.Json.JsonSerializer.Serialize(stockData);
                        
                        // vi sender dataen til client som json (igennem tunellen)
                        byte[] dataBytes = Encoding.UTF8.GetBytes($"data: {json}\n\n");
                        await response.Body.WriteAsync(dataBytes, 0, dataBytes.Length);
                        
                        // flush for at sikre at dataen bliver sendt med det samme
                        await response.Body.FlushAsync();
                    }
                    catch { /* Forbindelsen er nok lukket af kunden */ }
                });
            };

            // vi tilføjer vores handler som lytter til lageropdateringer
            _stockService.OnUpdate += handler;

            try
            {
                // Vi sætter serveren til at vente for evigt (indtil kunden lukker siden).
                await Task.Delay(-1, afbrudt);
            }
            finally
            {
                // Meget vigtigt: Vi fjerner lytteren, så serveren ikke spilder kræfter.
                _stockService.OnUpdate -= handler;
            }
        }
    }
}