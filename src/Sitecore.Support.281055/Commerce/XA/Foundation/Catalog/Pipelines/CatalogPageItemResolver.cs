using System;
using System.Globalization;
using System.Web;
using Sitecore.Commerce.XA.Foundation.Common;
using Sitecore.Commerce.XA.Foundation.Common.Providers;
using Sitecore.Commerce.XA.Foundation.Connect.Managers;
using Sitecore.Data.Managers;
using Sitecore.Diagnostics;
using Sitecore.Commerce.XA.Foundation.Catalog.Managers;
using Sitecore.Commerce.XA.Foundation.Common.Context;
using Sitecore.Commerce.XA.Foundation.Common.Utils;
using Sitecore.Pipelines;
using Sitecore.Web;

namespace Sitecore.Support.Commerce.XA.Foundation.Catalog.Pipelines
{
    public class CatalogPageItemResolver
    {
        public CatalogPageItemResolver()
        {
            this.SearchManager = ServiceLocatorHelper.GetService<ISearchManager>();
            Assert.IsNotNull(this.SearchManager, "this.SearchManager service could not be located.");

            this.StorefrontContext = ServiceLocatorHelper.GetService<IStorefrontContext>();
            Assert.IsNotNull(this.StorefrontContext, "this.StorefrontContext service could not be located.");

            this.ItemTypeProvider = ServiceLocatorHelper.GetService<IItemTypeProvider>();
            Assert.IsNotNull(this.ItemTypeProvider, "this.ItemTypeProvider service could not be located.");

            this.CatalogUrlManager = ServiceLocatorHelper.GetService<ICatalogUrlManager>();
            Assert.IsNotNull(this.CatalogUrlManager, "this.CatalogUrlManager service could not be located.");

            this.SiteContext = ServiceLocatorHelper.GetService<ISiteContext>();
            Assert.IsNotNull(this.SiteContext, "this.SiteContext service could not be located.");

            this.Context = ServiceLocatorHelper.GetService<IContext>();
            Assert.IsNotNull(this.SiteContext, "this.SitecoreContext service could not be located.");
        }

        public IContext Context
        {
            get;
        }

        public IStorefrontContext StorefrontContext
        {
            get;
            set;
        }

        public ISearchManager SearchManager
        {
            get;
            set;
        }

        public IItemTypeProvider ItemTypeProvider
        {
            get;
            set;
        }

        public ICatalogUrlManager CatalogUrlManager
        {
            get;
            set;
        }

        public ISiteContext SiteContext
        {
            get;
            set;
        }

        private bool IsGiftCardProductPage
        {
            get
            {
                return System.Convert.ToBoolean(HttpContext.Current.Items["IsGiftCardProductPage"], CultureInfo.InvariantCulture);
            }

            set
            {
                HttpContext.Current.Items["IsGiftCardProductPage"] = value;
            }
        }

        public virtual void Process(PipelineArgs args)
        {
            if (this.Context.Item == null || this.SiteContext.CurrentCatalogItem != null)
            {
                return;
            }

            var currentPageItemType = this.GetContextItemType();

            if (currentPageItemType == Sitecore.Commerce.XA.Foundation.Common.Constants.ItemTypes.Category || currentPageItemType == Sitecore.Commerce.XA.Foundation.Common.Constants.ItemTypes.Product)
            {
                var catalogItemId = this.GetCatalogItemIdFromUrl();

                if (!string.IsNullOrEmpty(catalogItemId))
                {
                    var catalogItemIsProduct = (currentPageItemType == Sitecore.Commerce.XA.Foundation.Common.Constants.ItemTypes.Product);
                    var currentCatalog = this.StorefrontContext.CurrentStorefront.Catalog;
                    var catalogContextItem = this.ResolveCatalogItem(catalogItemId, currentCatalog, catalogItemIsProduct);

                    if (catalogContextItem == null)
                    {
                        WebUtil.Redirect("~/");
                    }

                    this.SiteContext.CurrentCatalogItem = catalogContextItem;
                }
            }
        }

        protected virtual Sitecore.Data.Items.Item ResolveCatalogItem(string itemId, string catalogName, bool isProduct)
        {
            Sitecore.Data.Items.Item foundItem = null;

            if (!string.IsNullOrEmpty(itemId))
            {
                if (isProduct)
                {
                    foundItem = this.SearchManager.GetProduct(itemId, catalogName);
                }
                else
                {
                    foundItem = this.SearchManager.GetCategory(itemId, catalogName);
                }

            }

            return foundItem;
        }

        protected virtual bool IsGiftCardPageRequest()
        {
            var isGiftCardPage = false;

            if (this.IsGiftCardProductPage)
            {
                isGiftCardPage = true;
            }
            else
            {
                var languageUrlCode = this.Context.Language.ToString().ToLowerInvariant();
                var requestUrl = HttpContext.Current.Request.Url.AbsolutePath.ToLowerInvariant().Replace(".aspx", string.Empty);
                if (requestUrl.Contains(languageUrlCode))
                {
                    requestUrl = requestUrl.Replace("/" + languageUrlCode, string.Empty);
                }

                var giftPageUrl = this.StorefrontContext.CurrentStorefront.GiftCardPageLink?.ToLowerInvariant().Replace(".aspx", string.Empty);

                isGiftCardPage = (giftPageUrl.EndsWith(requestUrl, StringComparison.OrdinalIgnoreCase));
                this.IsGiftCardProductPage = isGiftCardPage;
            }

            return isGiftCardPage;
        }

        private Sitecore.Commerce.XA.Foundation.Common.Constants.ItemTypes GetContextItemType()
        {
            var template = TemplateManager.GetTemplate(Sitecore.Context.Item);
            var currentPageItemType = Sitecore.Commerce.XA.Foundation.Common.Constants.ItemTypes.Unknown;

            if (template.InheritsFrom(Sitecore.Commerce.XA.Foundation.Common.Constants.DataTemplates.CategoryPage.ID))
            {
                currentPageItemType = Sitecore.Commerce.XA.Foundation.Common.Constants.ItemTypes.Category;
            }
            else if (template.InheritsFrom(Sitecore.Commerce.XA.Foundation.Common.Constants.DataTemplates.ProductPage.ID))
            {
                currentPageItemType = Sitecore.Commerce.XA.Foundation.Common.Constants.ItemTypes.Product;
            }

            return currentPageItemType;
        }

        private string GetCatalogItemIdFromUrl()
        {
            if (this.IsGiftCardPageRequest())
            {
                var storefront = this.StorefrontContext.CurrentStorefront;
                return storefront.GiftCardProductId;
            }

            var catalogItemId = string.Empty;
            var url = HttpContext.Current.Request.RawUrl;

            #region   modified part to remove ending slash
            if (url.EndsWith("/", StringComparison.OrdinalIgnoreCase))
            {
                url = url.Substring(0, url.Length - 1);
            }
            #endregion

            var charIndex = url.LastIndexOf("/", StringComparison.OrdinalIgnoreCase);

            if (charIndex > 0)
            {
                url = url.Substring(charIndex + 1);

                charIndex = url.IndexOf("?", StringComparison.OrdinalIgnoreCase);

                if (charIndex > 0)
                {
                    url = url.Substring(0, charIndex);
                }

                catalogItemId = this.CatalogUrlManager.ExtractItemId(url);
            }

            return catalogItemId;
        }
    }
}
