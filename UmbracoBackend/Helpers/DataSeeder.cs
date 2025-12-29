using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Examine;

namespace UmbracoBackend.Helpers
{
    public static class DataSeeder
    {
        public static void Seed(IContentService contentService, IExamineManager examineManager)
        {
            // 1. Forside (Alias: frontpage)
            var root = contentService.GetRootContent().FirstOrDefault(x => x.ContentType.Alias == "frontpage");
            if (root == null)
            {
                root = contentService.Create("Forside", -1, "frontpage");
                root.SetValue("title", "Keramik med kærlighed");
                root.SetValue("subtitle", "Velkommen til min butik"); // Obligatorisk felt
                contentService.SaveAndPublish(root);
            }

            // 2. Om mig (Alias: contentPage)
            if (!contentService.GetPagedChildren(root.Id, 0, 10, out _).Any(x => x.Name == "Om mig"))
            {
                var omMig = contentService.Create("Om mig", root.Id, "contentPage");
                omMig.SetValue("title", "Ægte håndlavet keramik fra Danmark");
                contentService.SaveAndPublish(omMig);
            }

            // 3. Kontakt (Alias: contentPage)
            if (!contentService.GetPagedChildren(root.Id, 0, 10, out _).Any(x => x.Name == "Kontakt"))
            {
                var kontakt = contentService.Create("Kontakt", root.Id, "contentPage");
                kontakt.SetValue("title", "Kontakt mig på Julieeriksen09@hotmail.com");
                contentService.SaveAndPublish(kontakt);
            }

            // 4. Shop & Kategori (Newman forventer /shop/kopper/...)
            var shop = contentService.GetPagedChildren(root.Id, 0, 10, out _).FirstOrDefault(x => x.ContentType.Alias == "shop");
            if (shop == null)
            {
                shop = contentService.Create("Shop", root.Id, "shop");
                shop.SetValue("title", "Vores Shop");
                contentService.SaveAndPublish(shop);
            }

            var kategori = contentService.GetPagedChildren(shop.Id, 0, 10, out _).FirstOrDefault(x => x.Name == "kopper");
            if (kategori == null)
            {
                // Bruger 'category' alias fra din uSync
                kategori = contentService.Create("kopper", shop.Id, "category");
                kategori.SetValue("title", "Kopper");
                contentService.SaveAndPublish(kategori);
            }

            // 5. Produkter under kategorien
            SeedProduct(contentService, kategori.Id, "Glad kop", new Guid("1ceb8771-9542-4a1b-967d-b9919131f951"), 10);
            SeedProduct(contentService, kategori.Id, "Udsolgt kop", new Guid("4f016ba8-5a14-4f55-a157-014eff5f32a4"), 0);
            SeedProduct(contentService, kategori.Id, "Ikke udgivet kop", new Guid("62d4cea7-9135-4e19-aa18-bddced283215"), 5, false);

            // 6. Genopbyg søgeindeks (Vigtigt for Delivery API)
            if (examineManager.TryGetIndex(Constants.UmbracoIndexes.ExternalIndexName, out var index))
            {
                index.CreateIndex(); 
            }
        }

        private static void SeedProduct(IContentService contentService, int parentId, string name, Guid key, int stock, bool publish = true)
        {
            if (contentService.GetById(key) == null)
            {
                var product = contentService.Create(name, parentId, "product");
                product.Key = key;
                product.SetValue("stock", stock);
                product.SetValue("price", 100);
                
                if (publish) contentService.SaveAndPublish(product);
                else contentService.Save(product);
            }
        }
    }
}