using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;

namespace UmbracoBackend.Helpers
{
    public static class DataSeeder
    {
        public static void Seed(IContentService contentService)
        {
            // 1. Opret Forside (Leder efter: "Keramik med kærlighed")
            var root = contentService.GetRootContent().FirstOrDefault(x => x.ContentType.Alias == "frontpage");
            if (root == null)
            {
                root = contentService.Create("Forside", -1, "frontpage");
                root.SetValue("title", "Keramik med kærlighed"); // Juster alias hvis nødvendigt
                contentService.SaveAndPublish(root);
            }

            // 2. Opret Om Mig (Leder efter: "Ægte håndlavet keramik fra Danmark")
            if (!contentService.GetPagedChildren(root.Id, 0, 10, out _).Any(x => x.Name == "Om mig"))
            {
                var omMig = contentService.Create("Om mig", root.Id, "contentpage");
                omMig.SetValue("bodyText", "Ægte håndlavet keramik fra Danmark");
                contentService.SaveAndPublish(omMig);
            }

            // 3. Opret Kontakt (Leder efter email)
            if (!contentService.GetPagedChildren(root.Id, 0, 10, out _).Any(x => x.Name == "Kontakt"))
            {
                var kontakt = contentService.Create("Kontakt", root.Id, "contentpage");
                kontakt.SetValue("email", "Julieeriksen09@hotmail.com");
                contentService.SaveAndPublish(kontakt);
            }

            // 4. Opret Shop
            var shop = contentService.GetPagedChildren(root.Id, 0, 10, out _).FirstOrDefault(x => x.ContentType.Alias == "shop");
            if (shop == null)
            {
                shop = contentService.Create("Shop", root.Id, "shop");
                shop.SetValue("title", "Kategori");
                contentService.SaveAndPublish(shop);
            }

            // 5. Opret Produkter med specifikke GUID'er fra Postman
            SeedProduct(contentService, shop.Id, "Glad kop", new Guid("1ceb8771-9542-4a1b-967d-b9919131f951"), 10, true);
            SeedProduct(contentService, shop.Id, "Udsolgt kop", new Guid("4f016ba8-5a14-4f55-a157-014eff5f32a4"), 0, true);
            SeedProduct(contentService, shop.Id, "Ikke udgivet kop", new Guid("62d4cea7-9135-4e19-aa18-bddced283215"), 5, false);
        }

        private static void SeedProduct(IContentService contentService, int parentId, string name, Guid key, int stock, bool publish)
        {
            var product = contentService.GetById(key);
            if (product == null)
            {
                product = contentService.Create(name, parentId, "product");
                product.Key = key; // Sætter den faste GUID fra Postman
                product.SetValue("stock", stock);
                product.SetValue("price", 100);
                
                if (publish)
                    contentService.SaveAndPublish(product);
                else
                    contentService.Save(product);
            }
        }
    }
}