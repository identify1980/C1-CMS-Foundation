using System;
using System.Collections.Generic;
using System.Linq;
using System.Transactions;
using Composite.C1Console.Actions;
using Composite.C1Console.Elements;
using Composite.C1Console.Elements.ElementProviderHelpers.AssociatedDataElementProviderHelper;
using Composite.C1Console.Elements.Plugins.ElementProvider;
using Composite.C1Console.Events;
using Composite.Core.Extensions;
using Composite.Core.Linq;
using Composite.Core.Routing;
using Composite.Data;
using Composite.Data.ProcessControlled;
using Composite.Data.ProcessControlled.ProcessControllers.GenericPublishProcessController;
using Composite.Data.Types;
using Composite.Core.Parallelization;
using Composite.Core.ResourceSystem;
using Composite.Core.ResourceSystem.Icons;
using Composite.C1Console.Security;
using Composite.Data.Transactions;
using Composite.C1Console.Users;
using Composite.Core.WebClient;
using Composite.C1Console.Workflow;
using Microsoft.Practices.EnterpriseLibrary.Common.Configuration;
using Microsoft.Practices.EnterpriseLibrary.Common.Configuration.ObjectBuilder;
using Microsoft.Practices.ObjectBuilder;
//using PageManager = Composite.Data.PageManager;


namespace Composite.Plugins.Elements.ElementProviders.PageElementProvider
{
    [ActionTokenProvider(GenericPublishProcessControllerActionTypeNames.UndoUnpublishedChanges, typeof(PageElementProviderActionTokenProvider))]
    [ConfigurationElementType(typeof(PageElementProviderData))]
    internal class PageElementProvider : IHooklessElementProvider, IDataExchangingElementProvider, IDragAndDropElementProvider, ILocaleAwareElementProvider, IAuxiliarySecurityAncestorProvider
    {
        private ElementProviderContext _context;
        private AssociatedDataElementProviderHelper<IPage> _pageAccociatedHelper;


        public static ResourceHandle EditPage = GetIconHandle("page-edit-page");
        public static ResourceHandle LocalizePage = GetIconHandle("page-localize-page");
        public static ResourceHandle ManageHostNames = GetIconHandle("page-manage-host-names");
        public static ResourceHandle AddPage = GetIconHandle("page-add-page");
        public static ResourceHandle ListUnpublishedItems = GetIconHandle("page-list-unpublished-items");
        public static ResourceHandle AddSubPage = GetIconHandle("page-add-sub-page");
        public static ResourceHandle DeletePage = GetIconHandle("page-delete-page");
        public static ResourceHandle PageViewPublicScope = GetIconHandle("page-view-public-scope");
        public static ResourceHandle PageViewPublicScopeDisabled = GetIconHandle("page-view-public-scope-disabled");
        public static ResourceHandle PageViewAdministratedScope = GetIconHandle("page-view-administrated-scope");
        public static ResourceHandle PageDraft = GetIconHandle("page-draft");
        public static ResourceHandle PageAwaitingApproval = GetIconHandle("page-awaiting-approval");
        public static ResourceHandle PageAwaitingPublication = GetIconHandle("page-awaiting-publication");
        public static ResourceHandle PagePublication = GetIconHandle("page-publication");
        public static ResourceHandle PageGhosted = GetIconHandle("page-ghosted");
        public static ResourceHandle PageDisabled = GetIconHandle("page-disabled");
        public static ResourceHandle RootOpen = GetIconHandle("page-root-open");
        public static ResourceHandle RootClosed = GetIconHandle("page-root-closed");
        public static ResourceHandle ActivateLocalization = GetIconHandle("page-activatelocalization");
        public static ResourceHandle DeactivateLocalization = GetIconHandle("page-deactivatelocalization");
        public static ResourceHandle AddDataAssociationTypeIcon = GetIconHandle("dataassociation-add-association");
        public static ResourceHandle EditDataAssociationTypeIcon = GetIconHandle("dataassociation-edit-association");
        public static ResourceHandle RemoveDataAssociationTypeIcon = GetIconHandle("dataassociation-remove-association");

        private static readonly ActionGroup PrimaryActionGroup = new ActionGroup(ActionGroupPriority.PrimaryHigh);
        private static readonly ActionGroup ViewActionGroup = new ActionGroup("View", ActionGroupPriority.PrimaryLow);
        private static readonly ActionGroup AppendedActionGroup = new ActionGroup("Common tasks", ActionGroupPriority.GeneralAppendMedium);
        private static readonly ActionGroup MetaDataAppendedActionGroup = new ActionGroup("Associated data", ActionGroupPriority.PrimaryMedium);
        internal static readonly List<PermissionType> EditPermissionTypes = new List<PermissionType> { PermissionType.Edit };
        internal static readonly List<PermissionType> LocalizePermissionTypes = new List<PermissionType> { PermissionType.Edit };
        internal static readonly List<PermissionType> AddPermissionTypes = new List<PermissionType> { PermissionType.Add };
        internal static readonly List<PermissionType> DeletePermissionTypes = new List<PermissionType> { PermissionType.Delete };
        private static readonly PermissionType[] AddAssociatedTypePermissionTypes = new PermissionType[] { PermissionType.Add };
        private static readonly PermissionType[] RemoveAssociatedTypePermissionTypes = new PermissionType[] { PermissionType.Delete };
        private static readonly PermissionType[] EditAssociatedTypePermissionTypes = new PermissionType[] { PermissionType.Edit };


        private static ResourceHandle GetIconHandle(string name)
        {
            return new ResourceHandle(BuildInIconProviderName.ProviderName, name);
        }


        public PageElementProvider()
        {
            AuxiliarySecurityAncestorFacade.AddAuxiliaryAncestorProvider<DataEntityToken>(this);
        }



        public ElementProviderContext Context
        {
            set
            {
                _context = value;

                _pageAccociatedHelper = new AssociatedDataElementProviderHelper<IPage>(
                    _context,
                    new PageElementProviderEntityToken(_context.ProviderName),
                    true);
            }
        }



        public bool ContainsLocalizedData
        {
            get
            {
                return true;
            }
        }



        public IEnumerable<Element> GetRoots(SearchToken searchToken)
        {
            int pages;
            using (new DataScope(DataScopeIdentifier.Administrated))
            {
                pages = PageServices.GetChildrenCount(Guid.Empty);
            }

            EntityToken entityToken = new PageElementProviderEntityToken(_context.ProviderName);

            ElementDragAndDropInfo dragAndDropInfo = new ElementDragAndDropInfo();
            dragAndDropInfo.AddDropType(typeof(IPage));
            dragAndDropInfo.SupportsIndexedPosition = true;

            Element element = new Element(_context.CreateElementHandle(entityToken), dragAndDropInfo)
            {
                VisualData = new ElementVisualizedData
                {
                    Label = StringResourceSystemFacade.GetString("Composite.Plugins.PageElementProvider", "PageElementProvider.RootLabel"),
                    ToolTip = StringResourceSystemFacade.GetString("Composite.Plugins.PageElementProvider", "PageElementProvider.RootLabelToolTip"),
                    HasChildren = pages != 0,
                    Icon = PageElementProvider.RootClosed,
                    OpenedIcon = PageElementProvider.RootOpen
                }
            };

            element.AddAction(new ElementAction(new ActionHandle(new WorkflowActionToken(WorkflowFacade.GetWorkflowType("Composite.Plugins.Elements.ElementProviders.PageElementProvider.AddNewPageWorkflow"), AddPermissionTypes) { DoIgnoreEntityTokenLocking = true }))
            {
                VisualData = new ActionVisualizedData
                {
                    Label = StringResourceSystemFacade.GetString("Composite.Plugins.PageElementProvider", "PageElementProvider.AddPageAtRoot"),
                    ToolTip = StringResourceSystemFacade.GetString("Composite.Plugins.PageElementProvider", "PageElementProvider.AddPageAtRootToolTip"),
                    Icon = PageElementProvider.AddPage,
                    Disabled = false,
                    ActionLocation = new ActionLocation
                    {
                        ActionType = ActionType.Add,
                        IsInFolder = false,
                        IsInToolbar = true,
                        ActionGroup = PrimaryActionGroup
                    }
                }
            });


            element.AddAction(new ElementAction(new ActionHandle(new ViewUnpublishedItemsActionToken()))
            {
                VisualData = new ActionVisualizedData
                {
                    //Label = "List unpublished Pages and Folder Data",
                    //ToolTip = "Get an overview of pages and page folder data that haven't been published yet.",
                    Label = StringResourceSystemFacade.GetString("Composite.Plugins.PageElementProvider", "PageElementProvider.ViewUnpublishedItems"),
                    ToolTip = StringResourceSystemFacade.GetString("Composite.Plugins.PageElementProvider", "PageElementProvider.ViewUnpublishedItemsToolTip"),
                    Icon = PageElementProvider.ListUnpublishedItems,
                    Disabled = false,
                    ActionLocation = new ActionLocation
                    {
                        ActionType = ActionType.Other,
                        IsInFolder = false,
                        IsInToolbar = true,
                        ActionGroup = ViewActionGroup
                    }
                }
            });


            element.AddAction(new ElementAction(new ActionHandle(new WorkflowActionToken(WorkflowFacade.GetWorkflowType("Composite.C1Console.Elements.ElementProviderHelpers.AssociatedDataElementProviderHelper.AddMetaDataWorkflow"), AddAssociatedTypePermissionTypes) { DoIgnoreEntityTokenLocking = true }))
            {
                VisualData = new ActionVisualizedData
                {
                    Label = StringResourceSystemFacade.GetString("Composite.Management", "AssociatedDataElementProviderHelper.AddMetaDataTypeLabel"),
                    ToolTip = StringResourceSystemFacade.GetString("Composite.Management", "AssociatedDataElementProviderHelper.AddMetaDataTypeToolTip"),
                    Icon = AddDataAssociationTypeIcon,
                    Disabled = false,
                    ActionLocation = new ActionLocation
                    {
                        ActionType = ActionType.Add,
                        IsInFolder = false,
                        IsInToolbar = false,
                        ActionGroup = MetaDataAppendedActionGroup
                    }
                }
            });


            element.AddAction(new ElementAction(new ActionHandle(new WorkflowActionToken(WorkflowFacade.GetWorkflowType("Composite.C1Console.Elements.ElementProviderHelpers.AssociatedDataElementProviderHelper.EditMetaDataWorkflow"), EditAssociatedTypePermissionTypes)))
            {
                VisualData = new ActionVisualizedData
                {
                    Label = StringResourceSystemFacade.GetString("Composite.Management", "AssociatedDataElementProviderHelper.EditMetaDataTypeLabel"),
                    ToolTip = StringResourceSystemFacade.GetString("Composite.Management", "AssociatedDataElementProviderHelper.EditMetaDataTypeToolTip"),
                    Icon = EditDataAssociationTypeIcon,
                    Disabled = false,
                    ActionLocation = new ActionLocation
                    {
                        ActionType = ActionType.Edit,
                        IsInFolder = false,
                        IsInToolbar = false,
                        ActionGroup = MetaDataAppendedActionGroup
                    }
                }
            });


            element.AddAction(new ElementAction(new ActionHandle(new WorkflowActionToken(WorkflowFacade.GetWorkflowType("Composite.C1Console.Elements.ElementProviderHelpers.AssociatedDataElementProviderHelper.DeleteMetaDataWorkflow"), RemoveAssociatedTypePermissionTypes)))
            {
                VisualData = new ActionVisualizedData
                {
                    Label = StringResourceSystemFacade.GetString("Composite.Management", "AssociatedDataElementProviderHelper.RemoveMetaDataTypeLabel"),
                    ToolTip = StringResourceSystemFacade.GetString("Composite.Management", "AssociatedDataElementProviderHelper.RemoveMetaDataTypeToolTip"),
                    Icon = RemoveDataAssociationTypeIcon,
                    Disabled = false,
                    ActionLocation = new ActionLocation
                    {
                        ActionType = ActionType.Delete,
                        IsInFolder = false,
                        IsInToolbar = false,
                        ActionGroup = MetaDataAppendedActionGroup
                    }
                }
            });

            // Creates a problem for the front-end "toolbar caching" mechanism - dont re-introduce this right befroe a release
            // Reason: ActionTokin is always unique for a page, making the ActionKey (hash) unique
            //if (RuntimeInformation.IsDebugBuild == true)
            //{
            //    element.AddAction(new ElementAction(new ActionHandle(new DisplayLocalOrderingActionToken(Guid.Empty)))
            //    {
            //        VisualData = new ActionVisualizedData
            //        {
            //            Label = StringResourceSystemFacade.GetString("Composite.Plugins.PageElementProvider", "PageElementProvider.DisplayLocalOrderingLabel"),
            //            ToolTip = StringResourceSystemFacade.GetString("Composite.Plugins.PageElementProvider", "PageElementProvider.DisplayLocalOrderingToolTip"),
            //            Icon = CommonElementIcons.Nodes,
            //            Disabled = false,
            //            ActionLocation = new ActionLocation
            //            {
            //                ActionType = ActionType.DeveloperMode,
            //                IsInFolder = false,
            //                IsInToolbar = false,
            //                ActionGroup = AppendedActionGroup
            //            }
            //        }
            //    });
            //}

            yield return element;
        }



        public IEnumerable<Element> GetChildren(EntityToken entityToken, SearchToken searchToken)
        {
            if (entityToken is AssociatedDataElementProviderHelperEntityToken)
            {
                return _pageAccociatedHelper.GetChildren((AssociatedDataElementProviderHelperEntityToken)entityToken, false);
            }

            using (DataScope dataScope = new DataScope(DataScopeIdentifier.Administrated))
            {
                var allChildPages = GetChildrenPages(entityToken, searchToken);
                List<KeyValuePair<PageLocaleState, IPage>> childPages = IEnumerableExtensionMethods.ToList(allChildPages, f => new KeyValuePair<PageLocaleState, IPage>(PageLocaleState.Own, f));

                List<Element> childPageElements = GetElements(childPages);

                return GetChildElements(entityToken, childPageElements);
            }
        }



        public IEnumerable<Element> GetForeignRoots(SearchToken searchToken)
        {
            return GetRoots(searchToken);
        }



        public IEnumerable<Element> GetForeignChildren(EntityToken entityToken, SearchToken searchToken)
        {
            if ((entityToken is DataEntityToken) && (((DataEntityToken)entityToken).Data == null)) return new Element[] { };

            if (entityToken is AssociatedDataElementProviderHelperEntityToken)
            {
                return _pageAccociatedHelper.GetChildren((AssociatedDataElementProviderHelperEntityToken)entityToken, true);
            }

            Dictionary<Guid, IPage> pages;
            using (DataScope dataScope = new DataScope(DataScopeIdentifier.Administrated))
            {
                pages = GetChildrenPages(entityToken, searchToken).ToDictionary(f => f.Id);
            }


            Dictionary<Guid, IPage> foreignAdministratedPages;
            using (DataScope dataScope = new DataScope(DataScopeIdentifier.Administrated, UserSettings.ForeignLocaleCultureInfo))
            {
                foreignAdministratedPages = GetChildrenPages(entityToken, searchToken).ToDictionary(f => f.Id);
            }

            Dictionary<Guid, IPage> foreignPublicPages;
            using (DataScope dataScope = new DataScope(DataScopeIdentifier.Public, UserSettings.ForeignLocaleCultureInfo))
            {
                foreignPublicPages = GetChildrenPages(entityToken, searchToken).ToList().ToDictionary(f => f.Id);
            }


            Guid? itemId = GetParentPageId(entityToken);
            if (itemId.HasValue == false) return new Element[] { };

            IEnumerable<Guid> childPageIds =
              (from ps in DataFacade.GetData<IPageStructure>()
               where ps.ParentId == itemId.Value
               orderby ps.LocalOrdering
               select ps.Id).ToList();


            List<KeyValuePair<PageLocaleState, IPage>> resultPages = new List<KeyValuePair<PageLocaleState, IPage>>();
            foreach (Guid pageId in childPageIds)
            {
                IPage page;
                if (pages.TryGetValue(pageId, out page) == true)
                {
                    resultPages.Add(new KeyValuePair<PageLocaleState, IPage>(PageLocaleState.Own, page));
                }
                else if ((foreignAdministratedPages.TryGetValue(pageId, out page) == true) &&
                         ((page.PublicationStatus == GenericPublishProcessController.AwaitingPublication) || (page.PublicationStatus == GenericPublishProcessController.Published)))
                {
                    resultPages.Add(new KeyValuePair<PageLocaleState, IPage>(PageLocaleState.ForiegnActive, page));
                }
                else if (foreignPublicPages.TryGetValue(pageId, out page) == true)
                {
                    resultPages.Add(new KeyValuePair<PageLocaleState, IPage>(PageLocaleState.ForiegnActive, page));
                }
                else if (foreignAdministratedPages.TryGetValue(pageId, out page) == true)
                {
                    resultPages.Add(new KeyValuePair<PageLocaleState, IPage>(PageLocaleState.ForiegnDisabled, page));
                }
            }

            List<Element> childPageElements = GetElements(resultPages);

            return GetChildElements(entityToken, childPageElements);
        }



        public Dictionary<EntityToken, IEnumerable<EntityToken>> GetParents(IEnumerable<EntityToken> entityTokens)
        {
            Dictionary<EntityToken, IEnumerable<EntityToken>> result = new Dictionary<EntityToken, IEnumerable<EntityToken>>();

            foreach (EntityToken entityToken in entityTokens)
            {
                DataEntityToken dataEntityToken = entityToken as DataEntityToken;

                Type type = dataEntityToken.InterfaceType;
                if (type != typeof(IPage)) continue;

                IPage page = dataEntityToken.Data as IPage;
                if (page.GetParentId() != Guid.Empty) continue;

                PageElementProviderEntityToken newEntityToken = new PageElementProviderEntityToken(_context.ProviderName);

                result.Add(entityToken, new EntityToken[] { newEntityToken });
            }

            return result;
        }


        private IEnumerable<Element> GetChildElements(EntityToken entityToken, IEnumerable<Element> childPageElements)
        {
            Guid? itemId = GetParentPageId(entityToken);
            if (itemId.HasValue == false) return new Element[] { };

            List<Element> associatedChildElements;
            if (itemId.Value != Guid.Empty)
            {
                using (new DataScope(DataScopeIdentifier.Administrated))
                {
                    IPage page = PageManager.GetPageById(itemId.Value);

                    if (page != null) // null => Foreign page
                    {
                        associatedChildElements = _pageAccociatedHelper.GetChildren(page, entityToken);
                    }
                    else
                    {
                        associatedChildElements = new List<Element>();
                    }
                }
            }
            else
            {
                associatedChildElements = new List<Element>();
            }

            associatedChildElements.AddRange(childPageElements);

            return associatedChildElements;
        }



        private Guid? GetParentPageId(EntityToken entityToken)
        {
            if (entityToken is PageElementProviderEntityToken)
            {
                return Guid.Empty;
            }

            if (entityToken is DataEntityToken)
            {
                IPage parentPage = ((DataEntityToken)entityToken).Data as IPage;

                if (parentPage == null) return null;

                return parentPage.Id;
            }
            throw new NotImplementedException();
        }



        private IEnumerable<IPage> GetChildrenPages(EntityToken entityToken, SearchToken searchToken)
        {
            Guid? itemId = GetParentPageId(entityToken);

            if (itemId.HasValue == false) return new IPage[] { };


            if (searchToken.IsValidKeyword() == false)
            {
                return PageServices.GetChildren(itemId.Value).Evaluate().AsQueryable();
            }

            string keyword = searchToken.Keyword.ToLower();

            var predicateItems =
                from page in DataFacade.GetData<IPage>()
                where ((page.Description != null) && (page.Description.ToLower().Contains(keyword))) ||
                      ((page.Title != null) && (page.Title.ToLower().Contains(keyword)))
                select new TreeNode() { Key = page.Id, ParentKey = page.GetParentId() };


            List<TreeNode> keyTree =
                DataFacade.GetData<IPage>().Select(x => new TreeNode() { Key = x.Id, ParentKey = x.GetParentId() }).ToList();

            IEnumerable<TreeNode> nodes = new List<TreeNode>();
            foreach (TreeNode node in predicateItems)
            {
                nodes = nodes.Concat(GetAncestorPath(node, keyTree)).ToList();
            }

            List<Guid> pageIds = nodes.Where(x => x.ParentKey == itemId).Select(x => x.Key).Distinct().ToList();

            var pages = new List<IPage>();

            foreach (var page in DataFacade.GetData<IPage>())
            {
                if (pageIds.Contains(page.Id))
                {
                    pages.Add(page);
                }
            }

            return pages.AsQueryable();
        }


        private class TreeNode
        {
            public Guid Key { get; set; }
            public Guid ParentKey { get; set; }

        }


        private IList<TreeNode> GetAncestorPath(TreeNode key, IList<TreeNode> keys)
        {
            if (key.ParentKey == Guid.Empty)
            {
                return new List<TreeNode>() { key };
            }

            var parent = keys.First(x => x.Key == key.ParentKey);

            IList<TreeNode> ancestors = GetAncestorPath(parent, keys);
            ancestors.Add(key);
            return ancestors;
        }



        public bool OnElementDraggedAndDropped(EntityToken draggedEntityToken, EntityToken newParentEntityToken, int dropIndex, DragAndDropType dragAndDropType, FlowControllerServicesContainer flowControllerServicesContainer)
        {
            IPage draggedPage = (IPage)((DataEntityToken)draggedEntityToken).Data;

            Guid newParentPageId;
            if ((newParentEntityToken is PageElementProviderEntityToken) == true)
            {
                newParentPageId = Guid.Empty;
            }
            else if ((newParentEntityToken is DataEntityToken) == true)
            {
                IPage newParentPage = (IPage)((DataEntityToken)newParentEntityToken).Data;
                newParentPageId = newParentPage.Id;
            }
            else
            {
                throw new NotImplementedException();
            }

            IPage oldParent = null;
            if (draggedPage.GetParentId() != Guid.Empty)
            {
                oldParent = DataFacade.GetData<IPage>(f => f.Id == draggedPage.GetParentId()).Single();
            }

            if (dragAndDropType == DragAndDropType.Move)
            {
                using (TransactionScope transationScope = TransactionsFacade.CreateNewScope())
                {
                    string urlTitle = draggedPage.UrlTitle;
                    int counter = 1;

                    while (true)
                    {
                        bool urlTitleClashe =
                            (from p in PageServices.GetChildren(newParentPageId)
                             where p.UrlTitle == urlTitle
                             select p).Any();


                        if (urlTitleClashe == false)
                        {
                            break;
                        }

                        urlTitle = string.Format("{0}{1}", draggedPage.UrlTitle, counter++);
                    }

                    draggedPage.UrlTitle = urlTitle;

                    // BUG: DropIndex isn't calculated in a right way, for the UI, it's an index for pages from the current language, and from foreign one
                    // but MoveTo method requires index for pages from all the languages
                    draggedPage.MoveTo(newParentPageId, dropIndex, false);
                    
                    DataFacade.Update(draggedPage);

                    EntityTokenCacheFacade.ClearCache(draggedPage.GetDataEntityToken());

                    transationScope.Complete();
                }
            }
            else
            {
                throw new NotImplementedException();
            }


            if (oldParent != null)
            {
                ParentTreeRefresher oldParentParentTreeRefresher = new ParentTreeRefresher(flowControllerServicesContainer);
                oldParentParentTreeRefresher.PostRefreshMesseges(oldParent.GetDataEntityToken());
            }
            else
            {
                SpecificTreeRefresher oldParentspecificTreeRefresher = new SpecificTreeRefresher(flowControllerServicesContainer);
                oldParentspecificTreeRefresher.PostRefreshMesseges(new PageElementProviderEntityToken(_context.ProviderName));
            }

            ParentTreeRefresher newParentParentTreeRefresher = new ParentTreeRefresher(flowControllerServicesContainer);
            newParentParentTreeRefresher.PostRefreshMesseges(newParentEntityToken);

            return true;
        }



        private enum PageLocaleState
        {
            Own,
            ForiegnActive,
            ForiegnDisabled
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="pages">isLocalized -> IPage</param>
        /// <returns></returns>
        private List<Element> GetElements(List<KeyValuePair<PageLocaleState, IPage>> pages)
        {
            //ElementDragAndDropInfo dragAndDropInfo = new ElementDragAndDropInfo(typeof(IPage));
            //dragAndDropInfo.AddDropType(typeof(IPage));
            //dragAndDropInfo.SupportsIndexedPosition = true;




            string editPageLabel = StringResourceSystemFacade.GetString("Composite.Plugins.PageElementProvider", "PageElementProvider.EditPage");
            string editPageToolTip = StringResourceSystemFacade.GetString("Composite.Plugins.PageElementProvider", "PageElementProvider.EditPageToolTip");
            string localizePageLabel = StringResourceSystemFacade.GetString("Composite.Plugins.PageElementProvider", "PageElementProvider.LocalizePage");
            string localizePageToolTip = StringResourceSystemFacade.GetString("Composite.Plugins.PageElementProvider", "PageElementProvider.LocalizePageToolTip");
            string addNewPageLabel = StringResourceSystemFacade.GetString("Composite.Plugins.PageElementProvider", "PageElementProvider.AddSubPage");
            string addNewPageToolTip = StringResourceSystemFacade.GetString("Composite.Plugins.PageElementProvider", "PageElementProvider.AddSubPageToolTip");
            string deletePageLabel = StringResourceSystemFacade.GetString("Composite.Plugins.PageElementProvider", "PageElementProvider.Delete");
            string deletePageToolTip = StringResourceSystemFacade.GetString("Composite.Plugins.PageElementProvider", "PageElementProvider.DeleteToolTip");
            string viewPublicPageLabel = StringResourceSystemFacade.GetString("Composite.Plugins.PageElementProvider", "PageElementProvider.ViewPublicPage");
            string viewPublicPageToolTip = StringResourceSystemFacade.GetString("Composite.Plugins.PageElementProvider", "PageElementProvider.ViewPublicToolTip");
            string viewDraftPageLabel = StringResourceSystemFacade.GetString("Composite.Plugins.PageElementProvider", "PageElementProvider.ViewDraftPage");
            string viewDraftPageToolTip = StringResourceSystemFacade.GetString("Composite.Plugins.PageElementProvider", "PageElementProvider.ViewDraftToolTip");
            //string displayLocalOrderingLabel = StringResourceSystemFacade.GetString("Composite.Plugins.PageElementProvider", "PageElementProvider.DisplayLocalOrderingLabel");
            //string displayLocalOrderingToolTip = StringResourceSystemFacade.GetString("Composite.Plugins.PageElementProvider", "PageElementProvider.DisplayLocalOrderingToolTip");

            string urlMappingName = null;
            if (UserSettings.ForeignLocaleCultureInfo != null)
            {
                urlMappingName = DataLocalizationFacade.GetCultureTitle(UserSettings.ForeignLocaleCultureInfo);
            }

            Element[] elements = new Element[pages.Count];

            ParallelFacade.For("PageElementProvider. Getting elements", 0, pages.Count, i =>
            {
                var kvp = pages[i];
                IPage page = kvp.Value;

                EntityToken entityToken = page.GetDataEntityToken();

                ElementDragAndDropInfo dragAndDropInfo = new ElementDragAndDropInfo(typeof(IPage));
                dragAndDropInfo.AddDropType(typeof(IPage));
                dragAndDropInfo.SupportsIndexedPosition = true;

                Element element = new Element(_context.CreateElementHandle(entityToken), MakeVisualData(page, kvp.Key, urlMappingName), dragAndDropInfo);

                element.PropertyBag.Add("Uri", "~/page({0})".FormatWith(page.Id));
                element.PropertyBag.Add("ElementType", "application/x-composite-page");
                element.PropertyBag.Add("DataId", page.Id.ToString());

                if (kvp.Key == PageLocaleState.Own)
                {
                    // Normal actions
                    element.AddAction(new ElementAction(new ActionHandle(new WorkflowActionToken(WorkflowFacade.GetWorkflowType("Composite.Plugins.Elements.ElementProviders.PageElementProvider.EditPageWorkflow"), EditPermissionTypes)))
                    {
                        VisualData = new ActionVisualizedData
                        {
                            Label = editPageLabel,
                            ToolTip = editPageToolTip,
                            Icon = PageElementProvider.EditPage,
                            Disabled = false,
                            ActionLocation = new ActionLocation
                            {
                                ActionType = ActionType.Edit,
                                IsInFolder = false,
                                IsInToolbar = true,
                                ActionGroup = PrimaryActionGroup
                            }
                        }
                    });

                    element.AddAction(new ElementAction(new ActionHandle(new WorkflowActionToken(WorkflowFacade.GetWorkflowType("Composite.Plugins.Elements.ElementProviders.PageElementProvider.AddNewPageWorkflow"), AddPermissionTypes) { DoIgnoreEntityTokenLocking = true }))
                    {
                        VisualData = new ActionVisualizedData
                        {
                            Label = addNewPageLabel,
                            ToolTip = addNewPageToolTip,
                            Icon = PageElementProvider.AddPage,
                            Disabled = false,
                            ActionLocation = new ActionLocation
                            {
                                ActionType = ActionType.Add,
                                IsInFolder = false,
                                IsInToolbar = true,
                                ActionGroup = PrimaryActionGroup
                            }
                        }
                    });

                    element.AddAction(new ElementAction(new ActionHandle(new WorkflowActionToken(WorkflowFacade.GetWorkflowType("Composite.Plugins.Elements.ElementProviders.PageElementProvider.DeletePageWorkflow"), DeletePermissionTypes)))
                    {
                        VisualData = new ActionVisualizedData
                        {
                            Label = deletePageLabel,
                            ToolTip = deletePageToolTip,
                            Icon = DeletePage,
                            Disabled = false,
                            ActionLocation = new ActionLocation
                            {
                                ActionType = ActionType.Delete,
                                IsInFolder = false,
                                IsInToolbar = true,
                                ActionGroup = PrimaryActionGroup
                            }
                        }
                    });


                    element.AddAction(new ElementAction(new ActionHandle(new ViewPublicActionToken()))
                    {
                        VisualData = new ActionVisualizedData
                        {
                            Label = viewPublicPageLabel,
                            ToolTip = viewPublicPageToolTip,
                            Icon = PageElementProvider.PageViewPublicScope,// (page.MajorVersionNumber == 0 ? PageElementProvider.PageViewPublicScopeDisabled : PageElementProvider.PageViewPublicScope),
                            Disabled = false, //(page.MajorVersionNumber == 0),
                            ActionLocation = new ActionLocation
                            {
                                ActionType = ActionType.Other,
                                IsInFolder = false,
                                IsInToolbar = false,
                                ActionGroup = ViewActionGroup
                            }
                        }
                    });

                    element.AddAction(new ElementAction(new ActionHandle(new ViewDraftActionToken()))
                    {
                        VisualData = new ActionVisualizedData
                        {
                            Label = viewDraftPageLabel,
                            ToolTip = viewDraftPageToolTip,
                            Icon = PageElementProvider.PageViewAdministratedScope,
                            Disabled = false,
                            ActionLocation = new ActionLocation
                            {
                                ActionType = ActionType.Other,
                                IsInFolder = false,
                                IsInToolbar = false,
                                ActionGroup = ViewActionGroup
                            }
                        }
                    });

                    // Creates a problem for the front-end "toolbar caching" mechanism - dont re-introduce this right befroe a release
                    // Reason: ActionTokin is always unique for a page, making the ActionKey (hash) unique

                    //if (RuntimeInformation.IsDebugBuild == true)
                    //{
                    //    element.AddAction(new ElementAction(new ActionHandle(new DisplayLocalOrderingActionToken(page.Id)))
                    //    {
                    //        VisualData = new ActionVisualizedData
                    //        {
                    //            Label = displayLocalOrderingLabel,
                    //            ToolTip = displayLocalOrderingToolTip,
                    //            Icon = CommonElementIcons.Nodes,
                    //            Disabled = false,
                    //            ActionLocation = new ActionLocation
                    //            {
                    //                ActionType = ActionType.DeveloperMode,
                    //                IsInFolder = false,
                    //                IsInToolbar = false,
                    //                ActionGroup = AppendedActionGroup
                    //            }
                    //        }
                    //    });
                    //}

                    _pageAccociatedHelper.AttachElementActions(element, page);
                }
                else if (kvp.Key == PageLocaleState.ForiegnActive)
                {
                    // Localized actions
                    bool addAction = false;

                    Guid parentId = page.GetParentId();
                    if (parentId == Guid.Empty)
                    {
                        addAction = true;
                    }
                    else
                    {
                        using (new DataScope(DataScopeIdentifier.Administrated, UserSettings.ActiveLocaleCultureInfo))
                        {
                            bool exists = DataFacade.GetData<IPage>(f => f.Id == parentId).Any();
                            if (exists == true)
                            {
                                addAction = true;
                            }
                        }
                    }


                    if (addAction == true)
                    {
                        element.AddAction(new ElementAction(new ActionHandle(new WorkflowActionToken(WorkflowFacade.GetWorkflowType("Composite.Plugins.Elements.ElementProviders.PageElementProvider.LocalizePageWorkflow"), LocalizePermissionTypes)))
                        {
                            VisualData = new ActionVisualizedData
                            {
                                Label = localizePageLabel,
                                ToolTip = localizePageToolTip,
                                Icon = PageElementProvider.LocalizePage,
                                Disabled = false,
                                ActionLocation = new ActionLocation
                                {
                                    ActionType = ActionType.Edit,
                                    IsInFolder = false,
                                    IsInToolbar = true,
                                    ActionGroup = PrimaryActionGroup
                                }
                            }
                        });
                    }


                    element.AddAction(new ElementAction(new ActionHandle(new ViewPublicActionToken()))
                    {
                        VisualData = new ActionVisualizedData
                        {
                            Label = viewPublicPageLabel,
                            ToolTip = viewPublicPageToolTip,
                            Icon = PageElementProvider.PageViewPublicScope,// (page.MajorVersionNumber == 0 ? PageElementProvider.PageViewPublicScopeDisabled : PageElementProvider.PageViewPublicScope),
                            Disabled = false, //(page.MajorVersionNumber == 0),
                            ActionLocation = new ActionLocation
                            {
                                ActionType = ActionType.Other,
                                IsInFolder = false,
                                IsInToolbar = false,
                                ActionGroup = ViewActionGroup
                            }
                        }
                    });



                    //element.AddAction(new ElementAction(new ActionHandle(new ViewDraftActionToken()))
                    //{
                    //    VisualData = new ActionVisualizedData
                    //    {
                    //        Label = viewDraftPageLabel,
                    //        ToolTip = viewDraftPageToolTip,
                    //        Icon = PageElementProvider.PageViewAdministratedScope,
                    //        Disabled = false,
                    //        ActionLocation = new ActionLocation
                    //        {
                    //            ActionType = ActionType.Other,
                    //            IsInFolder = false,
                    //            IsInToolbar = false,
                    //            ActionGroup = ViewActionGroup
                    //        }
                    //    }
                    //});
                }

                elements[i] = element;
            }); 

            return new List<Element>(elements);
        }



        private ElementVisualizedData MakeVisualData(IPage page, PageLocaleState pageLocaleState, string urlMappingName)
        {
            ElementVisualizedData visualizedElement = new ElementVisualizedData();

            bool hasChildren = PageServices.GetChildrenCount(page.Id) > 0 || _pageAccociatedHelper.HasChildren(page);

            visualizedElement.HasChildren = hasChildren;
            visualizedElement.Label = page.Title;
            visualizedElement.ToolTip = page.Description;

            if (pageLocaleState == PageLocaleState.Own)
            {
                if (page.PublicationStatus == GenericPublishProcessController.Draft)
                {
                    visualizedElement.Icon = PageElementProvider.PageDraft;
                    visualizedElement.OpenedIcon = PageElementProvider.PageDraft;
                }
                else if (page.PublicationStatus == GenericPublishProcessController.AwaitingApproval)
                {
                    visualizedElement.Icon = PageElementProvider.PageAwaitingApproval;
                    visualizedElement.OpenedIcon = PageElementProvider.PageAwaitingApproval;
                }
                else if (page.PublicationStatus == GenericPublishProcessController.AwaitingPublication)
                {
                    visualizedElement.Icon = PageElementProvider.PageAwaitingPublication;
                    visualizedElement.OpenedIcon = PageElementProvider.PageAwaitingPublication;
                }
                else
                {
                    visualizedElement.Icon = PageElementProvider.PagePublication;
                    visualizedElement.OpenedIcon = PageElementProvider.PagePublication;
                }
            }
            else if (pageLocaleState == PageLocaleState.ForiegnActive)
            {
                visualizedElement.Icon = PageElementProvider.PageGhosted;
                visualizedElement.OpenedIcon = PageElementProvider.PageGhosted;
                visualizedElement.IsDisabled = false;
                visualizedElement.Label = string.Format("{0} ({1})", visualizedElement.Label, urlMappingName);
            }
            else
            {
                visualizedElement.Icon = PageElementProvider.PageDisabled;
                visualizedElement.OpenedIcon = PageElementProvider.PageDisabled;
                visualizedElement.IsDisabled = true;
                visualizedElement.ToolTip = StringResourceSystemFacade.GetString("Composite.Plugins.PageElementProvider", "PageElementProvider.DisabledPage");
                visualizedElement.Label = string.Format("{0} ({1})", visualizedElement.Label, urlMappingName);
            }

            return visualizedElement;
        }


        #region IDataExchangingElementProvider Members

        public object GetData(string name)
        {
            return "The page element provider here - you asked me for '" + name + "'";
        }

        #endregion
    }



    internal sealed class PreviewActionExecutor : Composite.C1Console.Actions.IActionExecutor
    {
        public FlowToken Execute(EntityToken entityToken, ActionToken actionToken, FlowControllerServicesContainer flowControllerServicesContainer)
        {
            DataEntityToken token = (DataEntityToken)entityToken;
            IPage page = token.Data as IPage;

            PublicationScope publicationScope = PublicationScope.Unpublished;
            if (actionToken is ViewPublicActionToken)
            {
                publicationScope = PublicationScope.Published;

                // Checking whether the page exist in 'Public' scope
                using (new DataScope(DataScopeIdentifier.Public, page.DataSourceId.LocaleScope))
                {
                    bool exist = DataFacade.GetData<IPage>(x => x.Id == page.Id).Any();
                    if (!exist)
                    {
                        var managementConsoleMessageService = flowControllerServicesContainer.GetService<IManagementConsoleMessageService>();

                        managementConsoleMessageService.ShowMessage(DialogType.Message,
                            StringResourceSystemFacade.GetString("Composite.Plugins.PageElementProvider", "Preview.PublishedPage.NotPublishedTitle"),
                            StringResourceSystemFacade.GetString("Composite.Plugins.PageElementProvider", "Preview.PublishedPage.NotPublishedMessage"));

                        return null;
                    }
                }
            }

            IPage previewPage;
            using(new DataScope(publicationScope, page.DataSourceId.LocaleScope))
            {
                previewPage = PageManager.GetPageById(page.Id);
            }

            UrlData<IPage> pageUrlData = new UrlData<IPage>(previewPage);

            var urlSpace = new UrlSpace();
            if(HostnameBindingsFacade.GetBindingForCurrentRequest() != null)
            {
                urlSpace.ForceRelativeUrls = true;
            }

            string url = PageUrls.BuildUrl(pageUrlData, UrlKind.Public, urlSpace)
                      ?? PageUrls.BuildUrl(pageUrlData, UrlKind.Internal, urlSpace); 

            var arguments = new Dictionary<string, string> {{"URL", url}};
            IManagementConsoleMessageService consoleServices = flowControllerServicesContainer.GetService<IManagementConsoleMessageService>();
            ConsoleMessageQueueFacade.Enqueue(new OpenHandledViewMessageQueueItem(EntityTokenSerializer.Serialize(entityToken, true), "Composite.Management.Browser", arguments), consoleServices.CurrentConsoleId);

            return null;
        }
    }



    // Not to used on elements. This is only for determin drag'n'drop security
    internal sealed class DragAndDropActionToken : ActionToken
    {
        private static PermissionType[] _permissoinTypes = new PermissionType[] { PermissionType.Administrate, PermissionType.Edit };

        public override IEnumerable<PermissionType> PermissionTypes
        {
            get { return _permissoinTypes; }
        }
    }



    [IgnoreEntityTokenLocking]
    [ActionExecutor(typeof(PreviewActionExecutor))]
    internal sealed class ViewPublicActionToken : ActionToken
    {
        private static IEnumerable<PermissionType> _permissionTypes = new PermissionType[] { PermissionType.Read };

        public ViewPublicActionToken()
        {
        }

        public override IEnumerable<PermissionType> PermissionTypes
        {
            get { return _permissionTypes; }
        }

        public override string Serialize()
        {
            return "ViewPublic";
        }


        public static ActionToken Deserialize(string serializedData)
        {
            return new ViewPublicActionToken();
        }
    }



    [IgnoreEntityTokenLocking]
    [ActionExecutor(typeof(PreviewActionExecutor))]
    internal sealed class ViewDraftActionToken : ActionToken
    {
        private static IEnumerable<PermissionType> _permissionTypes = new PermissionType[] { PermissionType.Read };

        public ViewDraftActionToken()
        {
        }

        public override IEnumerable<PermissionType> PermissionTypes
        {
            get { return _permissionTypes; }
        }

        public override string Serialize()
        {
            return "ViewDraft";
        }


        public static ActionToken Deserialize(string serializedData)
        {
            return new ViewDraftActionToken();
        }
    }



    internal sealed class PageElementProviderAssembler : IAssembler<IHooklessElementProvider, HooklessElementProviderData>
    {
        public IHooklessElementProvider Assemble(IBuilderContext context, HooklessElementProviderData objectConfiguration, IConfigurationSource configurationSource, ConfigurationReflectionCache reflectionCache)
        {
            return new PageElementProvider();
        }
    }



    [Assembler(typeof(PageElementProviderAssembler))]
    internal sealed class PageElementProviderData : HooklessElementProviderData
    {
    }

}
