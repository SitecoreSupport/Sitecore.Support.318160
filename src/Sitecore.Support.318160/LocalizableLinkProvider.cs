using System;
using System.Linq;
using System.Web;
using System.Web.Caching;
using Microsoft.Extensions.DependencyInjection;
using Sitecore.Data.Items;
using Sitecore.Links;
using Sitecore.Text;
using Sitecore.XA.Foundation.Multisite;

namespace Sitecore.Support.XA.Foundation.Multisite.LinkManagers
{
  public class LocalizableLinkProvider : Sitecore.XA.Foundation.Multisite.LinkManagers.LocalizableLinkProvider
  {
    private int _cacheExpiration = 1;
    private const string CacheKey = "LLM_{0}_{1}_{2}_";
    public override string GetItemUrl(Item item, UrlOptions options)
    {
      options.SiteResolving = !IsEditOrPreview && options.SiteResolving;
      options = ResolveLanguageEmbedding(item, options);

      string url = base.GetItemUrl(item, options);
      if (item.Database == null)
      {
        return url;
      }

      var siteInfo = DependencyInjection.ServiceLocator.ServiceProvider.GetService<ISiteInfoResolver>().GetSiteInfo(item);
      var targetSiteName = siteInfo?.Name;

      string cacheKey = string.Format(CacheKey, url, item.Database.Name, targetSiteName);
      cacheKey += options.AlwaysIncludeServerUrl ? "absolute" : "relative";

      string newUrl = IsEditOrPreview ? string.Empty : HttpRuntime.Cache.Get(cacheKey) as string;
      if (!string.IsNullOrWhiteSpace(newUrl))
      {
        return newUrl;
      }

      if (!item.Paths.IsMediaItem)
      {
        newUrl = GetLocalizedUrl(item, url, options, targetSiteName);
        HttpRuntime.Cache.Insert(cacheKey, newUrl, null, DateTime.UtcNow.AddMinutes(_cacheExpiration), Cache.NoSlidingExpiration);
        return newUrl;
      }
      return url;
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

      var localizedUrl = string.Empty;
      var urlFragments = url.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries).Reverse().ToList();
      if (urlFragments.Count > 0)
      {
        var itemAncestors = item.Axes.GetAncestors().Reverse().ToList();
        itemAncestors.Insert(0, item);
        for (int i = 0; i < urlFragments.Count(); i++)
        {
          if (i >= itemAncestors.Count)
          {
            break;
          }
          if (!string.IsNullOrWhiteSpace(itemAncestors[i][Sitecore.XA.Foundation.Multisite.Constants.LocalizedUrlPart]))
          {
            urlFragments[i] = itemAncestors[i][Sitecore.XA.Foundation.Multisite.Constants.LocalizedUrlPart];
          }
        }
        urlFragments.Reverse();
        localizedUrl = urlFragments.Aggregate((a, b) => a + "/" + b);
      }

      localizedUrl = "/" + localizedUrl;
      if (options.AddAspxExtension && localizedUrl.Length > 1)
      {
        localizedUrl = localizedUrl + ".aspx";
      }

      if (options.AlwaysIncludeServerUrl && uri != null)
      {
        localizedUrl = $"{uri.Scheme}://{uri.Host}{localizedUrl}";
      }

      if (!string.IsNullOrWhiteSpace(targetSite))
      {
        var isSxaSite = options.Site?.Properties["IsSxaSite"]?.Equals("true", StringComparison.OrdinalIgnoreCase);
        if (isSxaSite != null && isSxaSite.Value && (PageMode.IsExperienceEditorEditing || PageMode.IsPreview))
        {
          var urlString = new UrlString(localizedUrl);
          localizedUrl = urlString.Add("sc_site", targetSite);
        }
      }

      return localizedUrl;
    }
  }
}