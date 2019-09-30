namespace Sitecore.Support.XA.Foundation.Multisite.LinkManagers
{
    using Microsoft.Extensions.DependencyInjection;
    using Sitecore.Data.Items;
    using Sitecore.DependencyInjection;
    using Sitecore.Diagnostics;
    using Sitecore.Links;
    using Sitecore.Sites;
    using Sitecore.Text;
    using Sitecore.Web;
    using Sitecore.XA.Foundation.Abstractions;
    using Sitecore.XA.Foundation.Multisite;
    using Sitecore.XA.Foundation.Multisite.Extensions;
    using Sitecore.XA.Foundation.Multisite.LinkManagers;
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Linq;
    using System.Web;
    using System.Web.Caching;

    public class LocalizableLinkProvider : Sitecore.XA.Foundation.Multisite.LinkManagers.LocalizableLinkProvider
    {
        private int _cacheExpiration = 1;

        private const string CacheKey = "LLM_{0}_{1}_{2}_";

        public override string GetItemUrl(Item item, UrlOptions options)
        {
            options.SiteResolving = (!IsEditOrPreview && options.SiteResolving);
            options = ResolveLanguageEmbedding(item, options);
            string itemUrl = base.GetItemUrl(item, options);
            if (item.Database == null)
            {
                return itemUrl;
            }
            SiteInfo siteInfo = ServiceLocator.ServiceProvider.GetService<ISiteInfoResolver>().GetSiteInfo(item);
            string text = (siteInfo != null) ? siteInfo.Name : null;
            string str = string.Format("LLM_{0}_{1}_{2}_", itemUrl, item.Database.Name, text);
            str += (options.AlwaysIncludeServerUrl ? "absolute" : "relative");
            string text2 = IsEditOrPreview ? string.Empty : (HttpRuntime.Cache.Get(str) as string);
            if (!string.IsNullOrWhiteSpace(text2))
            {
                return text2;
            }
            if (!item.Paths.IsMediaItem)
            {
                if (siteInfo != null && Context.Site != null)
                {
                    string name = siteInfo.Name;
                    IContext context = Context;
                    bool num = !string.Equals(name, (context != null) ? context.Site.Name : null, StringComparison.OrdinalIgnoreCase);
                    bool flag = !string.IsNullOrWhiteSpace(siteInfo.TargetHostName);
                    if (num & flag)
                    {
                        options.AlwaysIncludeServerUrl = true;
                    }
                }
                text2 = GetLocalizedUrl(item, itemUrl, options, text);
                HttpRuntime.Cache.Insert(str, text2, null, DateTime.UtcNow.AddMinutes(_cacheExpiration), Cache.NoSlidingExpiration);
                return text2;
            }
            return itemUrl;
        }

        private string GetLocalizedUrl(Item item, string url, UrlOptions options, string targetSite)
        {
            Uri uri = null;
            if (!url.StartsWith("/", StringComparison.Ordinal))
            {
                uri = new Uri(url);
                url = uri.LocalPath;
            }
            if (options.AddAspxExtension)
            {
                url = url.Replace(".aspx", string.Empty);
            }
            string str = string.Empty;
            List<string> list = url.Split(new char[1]
            {
            '/'
            }, StringSplitOptions.RemoveEmptyEntries).Reverse().ToList();
            if (list.Count > 0)
            {
                List<Item> list2 = item.Axes.GetAncestors().Reverse().ToList();
                list2.Insert(0, item);
                for (int i = 0; i < list.Count() && i < list2.Count; i++)
                {
                    if (!string.IsNullOrWhiteSpace(list2[i]["LocalizedUrlPart"]))
                    {
                        list[i] = list2[i]["LocalizedUrlPart"];
                    }
                }
                list.Reverse();
                str = list.Aggregate((string a, string b) => a + "/" + b);
            }
            str = "/" + str;
            if (options.AddAspxExtension && str.Length > 1)
            {
                str += ".aspx";
            }
            if (options.AlwaysIncludeServerUrl && uri != null)
            {
                str = uri.Scheme + Uri.SchemeDelimiter + uri.Host + str;
            }
            if (!string.IsNullOrWhiteSpace(targetSite))
            {
                SiteContext site = options.Site;
                bool? obj;
                if (site == null)
                {
                    obj = null;
                }
                else
                {
                    string text = site.Properties["IsSxaSite"];
                    obj = ((text != null) ? new bool?(text.Equals("true", StringComparison.OrdinalIgnoreCase)) : null);
                }
                bool? flag = obj;
                if (flag.HasValue && flag.Value && (PageMode.IsExperienceEditorEditing || PageMode.IsPreview))
                {
                    str = new UrlString(str).Add("sc_site", targetSite);
                }
            }
            return str;
        }
    }
}