﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Runtime.Caching;
using System.Web; 
using Composite.Core.Extensions;
using Composite.Core.Routing;
using Composite.Core.WebClient;
using Composite.Data;
using Composite.Data.Types;

namespace Composite.AspNet
{
    /// <summary>
    /// CompositeC1 implementation of <see cref="SiteMapProvider"/>
    /// </summary>
    public class CompositeC1SiteMapProvider : SiteMapProvider
    {
        protected class SiteMapContainer
        {
            public SiteMapNode Root { get; set; }

            public IDictionary<string, SiteMapNode> KeyToNodesMap { get; private set; }
            public IDictionary<string, SiteMapNode> RawUrlToNodesMap { get; private set; }
            public IDictionary<string, SiteMapNode> ParentNodesMap { get; private set; }
            public IDictionary<string, SiteMapNodeCollection> ChildCollectionsMap { get; private set; }

            public SiteMapContainer()
            {
                KeyToNodesMap = new Dictionary<string, SiteMapNode>();
                RawUrlToNodesMap = new Dictionary<string, SiteMapNode>();
                ParentNodesMap = new Dictionary<string, SiteMapNode>();
                ChildCollectionsMap = new Dictionary<string, SiteMapNodeCollection>();
            }
        }



        private const string _key = "sitemap";
        private static readonly object _lock = new object();
        private static readonly SiteMapContainer _emptySiteMap = new SiteMapContainer();

        /// <exclude />
        public bool ExtranetEnabled { get; private set; }

        /// <exclude />
        public override SiteMapProvider ParentProvider { get; set; }

        /// <exclude />
        public override SiteMapNode CurrentNode
        {
            get { return FindSiteMapNode(HttpContext.Current); }
        }

        /// <exclude />
        public override SiteMapNode RootNode
        {
            get { return GetRootNodeCore(); }
        }

        /// <exclude />
        public override SiteMapProvider RootProvider
        {
            get
            {
                if (ParentProvider != null)
                {
                    return ParentProvider.RootProvider;
                }

                return this;
            }
        }

        /// <exclude />
        public override SiteMapNode FindSiteMapNode(HttpContext context)
        {
            string key = GetCurrentNodeKey();

            return FindSiteMapNodeFromKey(key);
        }


        /// <exclude />
        public SiteMapNode FindSiteMapNode(string rawUrl, CultureInfo culture)
        {
            SiteMapNode node = null;
            var container = GetContainer(culture);
            if (container != null)
            {
                container.RawUrlToNodesMap.TryGetValue(rawUrl, out node);
            }

            return node;
        }

        /// <exclude />
        public override SiteMapNode FindSiteMapNodeFromKey(string key)
        {
            return FindSiteMapNodeFromKey(key, false);
        }

        /// <exclude />
        public SiteMapNode FindSiteMapNodeFromKey(string key, bool allLanguages)
        {
            if (!allLanguages)
            {
                return FindSiteMapNodeFromKey(key, CultureInfo.CurrentCulture);
            }

            var siteMap = LoadSiteMap();
            if (siteMap == null) return null;

            SiteMapNode node;
            if(siteMap.KeyToNodesMap.TryGetValue(key, out node))
            {
                return node;
            }

            return null;
        }

        /// <exclude />
        public SiteMapNode FindSiteMapNodeFromKey(string key, CultureInfo culture)
        {
            SiteMapNode node;
            var container = GetContainer(culture);

            container.KeyToNodesMap.TryGetValue(key, out node);

            return node;
        }

        public override SiteMapNodeCollection GetChildNodes(SiteMapNode node)
        {
            Verify.ArgumentNotNull(node, "node");

            SiteMapNodeCollection childNodes;
            var culture = ((CompositeC1SiteMapNode) node).Culture;
            var container = GetContainer(culture);

            container.ChildCollectionsMap.TryGetValue(node.Key, out childNodes);

            if (childNodes == null)
            {
                return SiteMapNodeCollection.ReadOnly(new SiteMapNodeCollection());
            }

            if (!SecurityTrimmingEnabled)
            {
                return SiteMapNodeCollection.ReadOnly(childNodes);
            }

            var context = HttpContext.Current;
            var returnList = new SiteMapNodeCollection(childNodes.Count);

            foreach (SiteMapNode child in childNodes)
            {
                if (child.IsAccessibleToUser(context))
                {
                    returnList.Add(child);
                }
            }

            return SiteMapNodeCollection.ReadOnly(returnList);
        }

        public override SiteMapNode GetParentNode(SiteMapNode node)
        {
            Verify.ArgumentNotNull(node, "node");

            SiteMapNode parentNode;
            var culture = ((CompositeC1SiteMapNode) node).Culture;
            var container = GetContainer(culture);

            container.ParentNodesMap.TryGetValue(node.Key, out parentNode);

            if ((parentNode == null) && (ParentProvider != null))
            {
                parentNode = ParentProvider.GetParentNode(node);
            }

            if (parentNode == null) return null;
            if (!parentNode.IsAccessibleToUser(HttpContext.Current)) return null;

            return parentNode;
        }

        protected override SiteMapNode GetRootNodeCore()
        {
            var context = SiteMapContext.Current;

            var culture = (context != null) ? context.RootPage.DataSourceId.LocaleScope : CultureInfo.CurrentCulture;

            var container = GetContainer(culture);

            return container.Root;
        }

        /// <exclude />
        public IEnumerable<CompositeC1SiteMapNode> GetRootNodes()
        {
            var list = new List<SiteMapNode>();
            foreach (Guid rootPageId in PageManager.GetChildrenIDs(Guid.Empty))
            {
                foreach (var culture in DataLocalizationFacade.ActiveLocalizationCultures)
                {
                    var siteMap = LoadSiteMap(culture, rootPageId);

                    SiteMapNode rootNode;
                    if (siteMap != null && (rootNode = siteMap.Root) != null)
                    {
                        list.Add(rootNode);
                    }
                }
            }

            return list.Cast<CompositeC1SiteMapNode>();
        }

        /// <exclude />
        public void Flush()
        {
            var keys = MemoryCache.Default.Where(o => o.Key.StartsWith(_key)).Select(o => o.Key);
            foreach (var key in keys)
            {
                MemoryCache.Default.Remove(key);
            }
        }

        protected void AddNode(SiteMapNode node, SiteMapNode parentNode, SiteMapContainer container)
        {
            Verify.ArgumentNotNull(node, "node");
            Verify.ArgumentNotNull(container, "container");

            container.KeyToNodesMap.Add(node.Key, node);
            container.ParentNodesMap.Add(node.Key, parentNode);

            if (!container.RawUrlToNodesMap.ContainsKey(node.Url))
            {
                container.RawUrlToNodesMap.Add(node.Url, node);
            }

            if (parentNode != null)
            {
                if (!container.ChildCollectionsMap.ContainsKey(parentNode.Key))
                {
                    container.ChildCollectionsMap[parentNode.Key] = new SiteMapNodeCollection();
                }

                container.ChildCollectionsMap[parentNode.Key].Add(node);
            }
        }

        private SiteMapContainer GetContainer(CultureInfo cultureInfo)
        {
            using(new DataScope(cultureInfo))
            {
                return LoadSiteMap();
            }
        }

        private SiteMapContainer LoadSiteMap()
        {
            var siteMapContext = SiteMapContext.Current;

            if(siteMapContext != null)
            {
                var rootPage = siteMapContext.RootPage;

                return LoadSiteMap(rootPage.DataSourceId.LocaleScope, rootPage.Id);
            }

            Guid currentHomePage = SitemapNavigator.CurrentHomePageId;
            if (currentHomePage != Guid.Empty)
            {
                return LoadSiteMap(CultureInfo.CurrentCulture, currentHomePage);
            }

            var context = HttpContext.Current;

            using(var conn = new DataConnection())
            {
                PageNode pageNode = new SitemapNavigator(conn).GetPageNodeByHostname(context.Request.Url.Host);
                if(pageNode != null)
                {
                    return LoadSiteMap(conn.CurrentLocale, pageNode.Id);
                }
            }

            return null;
        }

        private SiteMapContainer LoadSiteMap(CultureInfo culture, Guid rootPageId)
        {
            var uri = ProcessUrl(HttpContext.Current.Request.Url);
            var host = uri.Host;

            SiteMapContainer siteMap = GetFromCache(host, culture, rootPageId);
            if (siteMap == null)
            {
                lock (_lock)
                {
                    siteMap = GetFromCache(host, culture, rootPageId);

                    if (siteMap == null)
                    {
                        siteMap = LoadSiteMapInternal(culture, rootPageId);
                        if (siteMap != null)
                        {
                            AddRolesInternal(siteMap);
                        }
                        else
                        {
                            siteMap = _emptySiteMap;
                        }

                        AddToCache(siteMap, host, culture, rootPageId);
                    }
                }
            }

            return object.ReferenceEquals(siteMap, _emptySiteMap) ? null : siteMap;
        }

        private SiteMapContainer GetFromCache(string host, CultureInfo culture, Guid rootPageId)
        {
            var key = GetCacheKey(host, culture, rootPageId);

            var context = HttpContext.Current;
            var container = context.Items[key] as SiteMapContainer;

            if (container == null)
            {
                if (!CanCache) return null;

                container = MemoryCache.Default.Get(key) as SiteMapContainer;
                if (container != null)
                {
                    context.Items.Add(key, container);
                }
            }

            return container;
        }

        private void AddToCache(SiteMapContainer siteMap, string host, CultureInfo culture, Guid rootPageId)
        {
            var key = GetCacheKey(host, culture, rootPageId);

            var context = HttpContext.Current;
            context.Items[key] = siteMap;

            if (CanCache)
            {
                MemoryCache.Default.Add(key, siteMap, ObjectCache.InfiniteAbsoluteExpiration);
            }
        }

        private string GetCacheKey(string host, CultureInfo culture, Guid rootPageId)
        {
            string hostnameKey;

            var urlSpace = new UrlSpace();
            if(urlSpace.ForceRelativeUrls)
            {
                hostnameKey = string.Empty;
            }
            else
            {
                hostnameKey = host;
            }

            return _key + hostnameKey + rootPageId.ToString() + culture.Name + this.PublicationScope;
        }
    

    private PublicationScope PublicationScope
        {
            get
            {
                return DataScopeManager.CurrentDataScope.ToPublicationScope();
            }
        }

        protected bool CanCache
        {
            get
            {
                return DataScopeManager.CurrentDataScope.ToPublicationScope() == PublicationScope.Published;
            }
        }

        protected string GetCurrentNodeKey()
        {
            return SitemapNavigator.CurrentPageId.ToString();
        }

        public override SiteMapNode FindSiteMapNode(string rawUrl)
        {
            var culture = DataLocalizationFacade.DefaultLocalizationCulture;

            foreach (var activeCulture in DataLocalizationFacade.ActiveLocalizationCultures)
            {
                string cultureUrlMapping = DataLocalizationFacade.GetUrlMappingName(activeCulture);
                if (cultureUrlMapping.IsNullOrEmpty()) continue;

                string mappingPrefix = UrlUtils.PublicRootPath + "/" + cultureUrlMapping;

                if(rawUrl.Equals(mappingPrefix, StringComparison.OrdinalIgnoreCase)
                    || rawUrl.StartsWith(mappingPrefix + "/", StringComparison.OrdinalIgnoreCase))
                {
                    culture = activeCulture;
                    break;
                }
            }

            var node = FindSiteMapNode(rawUrl, culture) as CompositeC1SiteMapNode;
            if (node == null)
            {
                if (IsDefaultDocumentUrl(rawUrl))
                {
                    node = RootNode as CompositeC1SiteMapNode;
                }
            }

            return node ?? FindSiteMapNode(rawUrl);
        }

        internal static bool IsDefaultDocumentUrl(string url)
        {
            return url == "/"
            || url.Equals(UrlUtils.PublicRootPath, StringComparison.OrdinalIgnoreCase)
            || url.Equals(UrlUtils.PublicRootPath + "/", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith(UrlUtils.PublicRootPath + "/?", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith(UrlUtils.PublicRootPath + "/default.aspx", StringComparison.OrdinalIgnoreCase);
        }

        public override bool IsAccessibleToUser(HttpContext ctx, SiteMapNode node)
        {
            if (PublicationScope == PublicationScope.Unpublished)
            {
                return true;
            }

            if (ExtranetEnabled)
            {
                throw new NotImplementedException("Extranet is not implemented in the currect C1 sitemap provider");
            }

            return true;
        }

        public override void Initialize(string name, NameValueCollection attributes)
        {
            DataEventHandler handler = (sender, e) => Flush();

            DataEventSystemFacade.SubscribeToDataAfterAdd<IPage>(handler, true);
            DataEventSystemFacade.SubscribeToDataAfterUpdate<IPage>(handler, true);
            DataEventSystemFacade.SubscribeToDataDeleted<IPage>(handler, true);

            DataEventSystemFacade.SubscribeToDataAfterAdd<IPageStructure>(handler, true);
            DataEventSystemFacade.SubscribeToDataAfterUpdate<IPageStructure>(handler, true);
            DataEventSystemFacade.SubscribeToDataDeleted<IPageStructure>(handler, true);

            DataEventSystemFacade.SubscribeToDataAfterAdd<ISystemActiveLocale>(handler, true);
            DataEventSystemFacade.SubscribeToDataAfterUpdate<ISystemActiveLocale>(handler, true);
            DataEventSystemFacade.SubscribeToDataDeleted<ISystemActiveLocale>(handler, true);

            if (attributes != null)
            {
                bool extranetEnabled;
                if (bool.TryParse(attributes["extranetEnabled"], out extranetEnabled))
                {
                    ExtranetEnabled = extranetEnabled;
                }
                attributes.Remove("extranetEnabled");
            }

            base.Initialize(name, attributes);
        }

        protected void LoadSiteMapInternal(IDictionary<CultureInfo, SiteMapContainer> list, Guid rootPageId)
        {
            var scope = PublicationScope;

            foreach (var culture in DataLocalizationFacade.ActiveLocalizationCultures)
            {
                using (var data = new DataConnection(scope, culture))
                {
                    var rootPage = data.SitemapNavigator.GetPageNodeById(rootPageId);

                    if (rootPage != null)
                    {
                        var container = new SiteMapContainer
                        {
                            Root = new CompositeC1SiteMapNode(this, rootPage, data)
                        };

                        list.Add(culture, container);

                        LoadNodes(rootPage, null, container, data);
                    }
                }
            }
        }

        protected SiteMapContainer LoadSiteMapInternal(CultureInfo culture, Guid rootPageId)
        {
            var scope = PublicationScope;

            using (var data = new DataConnection(scope, culture))
            {
                var rootPage = data.SitemapNavigator.GetPageNodeById(rootPageId);

                if (rootPage == null)
                {
                    return null;                    
                }

                var container = new SiteMapContainer
                {
                    Root = new CompositeC1SiteMapNode(this, rootPage, data)
                };

                LoadNodes(rootPage, null, container, data);

                return container;
            }
        }

        /// <summary>
        ///  Fixes up the hostname when using the preview-tab in the Console, since the requested page can belong to 
        /// a different domain than the console was opened in
        /// </summary>
        /// <param name="uri">The URI.</param>
        /// <returns></returns>
        protected Uri ProcessUrl(Uri uri)
        {
            PageUrlData urlData;

            var context = HttpContext.Current;

            var overridenContext = SiteMapContext.Current;
            if (overridenContext != null)
            {
                urlData = new PageUrlData(overridenContext.RootPage);
            }
            else
            {
                string currentUrl = context.Request.Url.ToString();

                string _previewKey = context.Request.QueryString["previewKey"];
                if (!String.IsNullOrEmpty(_previewKey))
                {
                    var page = (IPage)context.Cache.Get(_previewKey + "_SelectedPage");
                    urlData = new PageUrlData(page);
                }
                else
                {
                    urlData = PageUrls.ParseUrl(currentUrl);
                }
            }

            if (urlData != null)
            {
                var publicUrl = PageUrls.BuildUrl(urlData, UrlKind.Public, new UrlSpace { Hostname = uri.Host, ForceRelativeUrls = false });
                if (Uri.IsWellFormedUriString(publicUrl, UriKind.Absolute))
                {
                    var newHost = new Uri(publicUrl).Host;

                    uri = new Uri(ReplaceFirstOccurance(uri.ToString(), uri.Host, newHost));
                }
            }

            return uri;
        }

        private static string ReplaceFirstOccurance(string @string, string oldValue, string newValue)
        {
            int offset = @string.IndexOf(oldValue, StringComparison.Ordinal);
            if(offset >= 0)
            {
                return @string.Substring(0, offset) + newValue + @string.Substring(offset + oldValue.Length);
            }
            return @string;
        }

        protected virtual void AddRolesInternal(SiteMapContainer siteMapContainer) { }

        private void LoadNodes(PageNode pageNode, SiteMapNode parent, SiteMapContainer container, DataConnection data)
        {
            if (pageNode.Url == null)
            {
                return;
            }

            var node = new CompositeC1SiteMapNode(this, pageNode, data);
            AddNode(node, parent, container);

            var childs = pageNode.ChildPages;
            foreach (var child in childs)
            {
                LoadNodes(child, node, container, data);
            }
        }
    }
}
