using System;
// Denne fil er den der står for at holde styr på lageropdateringer og sende dem til klienter via SSE
namespace UmbracoBackend.Helpers
{
    public interface IStockService
    {
        // denne action skal indeholde 2 ting, et produkt ID og den nye lagerstatus
        event Action<Guid, int>? OnUpdate;
        void SendUpdate(Guid productId, int stock);
    }

    public class StockService : IStockService
    {
        // event der bliver trigget når der er en lageropdatering
        public event Action<Guid, int>? OnUpdate;

        // selve funktionen der sender opdateringen videre til alle lyttere (lyttere = hver fane der har en side åben der skal bruge lagerdata)
        public void SendUpdate(Guid productId, int stock)
        {
            int lyttere = OnUpdate?.GetInvocationList().Length ?? 0;
            Console.WriteLine($"[SERVICE] Sender signal for ID {productId}. Aktive lyttere: {lyttere}");
            
            OnUpdate?.Invoke(productId, stock);
        }
    }
}