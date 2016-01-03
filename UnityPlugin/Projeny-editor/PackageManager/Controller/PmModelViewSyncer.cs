using System;
using System.IO;
using UnityEditorInternal;
using UnityEngine;
using UnityEditor;
using System.Collections;
using System.Collections.Generic;
using Projeny.Internal;
using System.Linq;

namespace Projeny.Internal
{
    public class PmModelViewSyncer : IDisposable
    {
        readonly PmModel _model;
        readonly PmView _view;

        readonly EventManager _eventManager = new EventManager();

        public PmModelViewSyncer(
            PmModel model, PmView view)
        {
            _model = model;
            _view = view;
        }

        public void Initialize()
        {
            _model.PluginItemsChanged += _eventManager.Add(OnListDisplayValuesDirty, EventQueueMode.LatestOnly);
            _model.AssetItemsChanged += _eventManager.Add(OnListDisplayValuesDirty, EventQueueMode.LatestOnly);
            _model.PackagesChanged += _eventManager.Add(OnListDisplayValuesDirty, EventQueueMode.LatestOnly);
            _model.ReleasesChanged += _eventManager.Add(OnListDisplayValuesDirty, EventQueueMode.LatestOnly);

            _view.ViewStateChanged += _eventManager.Add(OnListDisplayValuesDirty, EventQueueMode.LatestOnly);

            foreach (var list in _view.Lists)
            {
                list.SortDescendingChanged += _eventManager.Add(OnListDisplayValuesDirty, EventQueueMode.LatestOnly);
                list.SortMethodChanged += _eventManager.Add(OnListDisplayValuesDirty, EventQueueMode.LatestOnly);
            }

            // Don't bother showing the search pane for assets / plugins  - Or is that useful?

            var releaseList = _view.GetList(ListTypes.Release);
            releaseList.ShowSortPane = true;
            releaseList.SortMethodCaptions = new List<string>()
            {
                // These should match ReleasesSortMethod
                "Order By Name",
                "Order By File Modification Time",
                "Order By Size",
                "Order By Release Date"
            };

            var packagesList = _view.GetList(ListTypes.Package);
            packagesList.ShowSortPane = true;
            packagesList.SortMethodCaptions = new List<string>()
            {
                // These should match PackagesSortMethod
                "Order By Name",
                "Order By Install Date",
                "Order By Release Publish Date"
            };

            _eventManager.Trigger(OnListDisplayValuesDirty);
        }

        public void Dispose()
        {
            _model.PluginItemsChanged -= _eventManager.Remove(OnListDisplayValuesDirty);
            _model.AssetItemsChanged -= _eventManager.Remove(OnListDisplayValuesDirty);
            _model.PackagesChanged -= _eventManager.Remove(OnListDisplayValuesDirty);
            _model.ReleasesChanged -= _eventManager.Remove(OnListDisplayValuesDirty);

            foreach (var list in _view.Lists)
            {
                list.SortDescendingChanged -= _eventManager.Remove(OnListDisplayValuesDirty);
                list.SortMethodChanged -= _eventManager.Remove(OnListDisplayValuesDirty);
            }

            _view.ViewStateChanged -= _eventManager.Remove(OnListDisplayValuesDirty);

            _eventManager.AssertIsEmpty();
        }

        public void Update()
        {
            _eventManager.Flush();
        }

        void OnListDisplayValuesDirty()
        {
            _view.SetListItems(
                ListTypes.Release,
                OrderReleases().Select(x => CreateListItem(x)).ToList());

            _view.SetListItems(
                ListTypes.PluginItem,
                OrderPluginItems().Select(x => CreateListItemForProjectItem(x)).ToList());

            _view.SetListItems(
                ListTypes.AssetItem,
                OrderAssetItems().Select(x => CreateListItemForProjectItem(x)).ToList());

            _view.SetListItems(
                ListTypes.Package,
                OrderPackages().Select(x => CreateListItem(x)).ToList());
        }

        IEnumerable<string> OrderAssetItems()
        {
            if (_view.GetList(ListTypes.AssetItem).SortDescending)
            {
                return _model.AssetItems.OrderByDescending(x => x);
            }

            return _model.AssetItems.OrderBy(x => x);
        }

        IEnumerable<string> OrderPluginItems()
        {
            if (_view.GetList(ListTypes.PluginItem).SortDescending)
            {
                return _model.PluginItems.OrderByDescending(x => x);
            }

            return _model.PluginItems.OrderBy(x => x);
        }

        IEnumerable<ReleaseInfo> OrderReleases()
        {
            if (_view.GetList(ListTypes.Release).SortDescending)
            {
                return _model.Releases.OrderByDescending(x => GetReleaseSortField(x));
            }

            return _model.Releases.OrderBy(x => GetReleaseSortField(x));
        }

        IEnumerable<PackageInfo> OrderPackages()
        {
            if (_view.GetList(ListTypes.Package).SortDescending)
            {
                return _model.Packages.OrderByDescending(x => GetPackageSortField(x));
            }

            return _model.Packages.OrderBy(x => GetPackageSortField(x));
        }

        object GetPackageSortField(PackageInfo info)
        {
            switch ((PackagesSortMethod)_view.GetList(ListTypes.Package).SortMethod)
            {
                case PackagesSortMethod.Name:
                {
                    return info.Name;
                }
                case PackagesSortMethod.InstallDate:
                {
                    return info.InstallInfo.InstallDateTicks;
                }
                case PackagesSortMethod.ReleasePublishDate:
                {
                    return info.InstallInfo.ReleaseInfo.AssetStoreInfo.PublishDateTicks;
                }
            }

            Assert.Throw();
            return null;
        }

        object GetReleaseSortField(ReleaseInfo info)
        {
            switch ((ReleasesSortMethod)_view.GetList(ListTypes.Release).SortMethod)
            {
                case ReleasesSortMethod.Name:
                {
                    return info.Name;
                }
                case ReleasesSortMethod.FileModificationDate:
                {
                    return info.FileModificationDateTicks;
                }
                case ReleasesSortMethod.Size:
                {
                    return info.CompressedSize;
                }
                case ReleasesSortMethod.ReleaseDate:
                {
                    return info.AssetStoreInfo.PublishDateTicks;
                }
            }

            Assert.Throw();
            return null;
        }

        ListItemData CreateListItemForProjectItem(string name)
        {
            string caption;

            if (_view.ViewState == PmViewStates.PackagesAndProject)
            {
                caption = ImguiUtil.WrapWithColor(name, _view.Skin.Theme.DraggableItemAlreadyAddedColor);
            }
            else
            {
                // this isn't always the case since it can be rendered when interpolating
                //Assert.That(_viewState == PmViewStates.Project);
                caption = name;
            }

            return new ListItemData()
            {
                Caption = caption,
                Model = name
            };
        }

        ListItemData CreateListItem(ReleaseInfo info)
        {
            string caption;

            if (_model.IsReleaseInstalled(info))
            {
                caption = ImguiUtil.WrapWithColor(
                    info.Name, _view.Skin.Theme.DraggableItemAlreadyAddedColor);
            }
            else
            {
                caption = info.Name;
            }

            caption = string.IsNullOrEmpty(info.Version) ? caption : "{0} {1}"
                .Fmt(caption, ImguiUtil.WrapWithColor("v" + info.Version, _view.Skin.Theme.VersionColor));

            return new ListItemData()
            {
                Caption = caption,
                Model = info,
            };
        }

        ListItemData CreateListItem(PackageInfo info)
        {
            string caption;

            if (_view.ViewState == PmViewStates.ReleasesAndPackages)
            {
                var releaseInfo = info.InstallInfo.ReleaseInfo;
                if (!string.IsNullOrEmpty(releaseInfo.Name))
                {
                    caption = "{0} ({1}{2})".Fmt(
                        info.Name,
                        ImguiUtil.WrapWithColor(releaseInfo.Name, _view.Skin.Theme.DraggableItemAlreadyAddedColor),
                        string.IsNullOrEmpty(releaseInfo.Version) ? "" : ImguiUtil.WrapWithColor(" v" + releaseInfo.Version, _view.Skin.Theme.VersionColor));
                }
                else
                {
                    caption = info.Name;
                }
            }
            else
            {
                // this isn't always the case since it can be rendered when interpolating
                //Assert.IsEqual(_model.ViewState, PmViewStates.PackagesAndProject);

                if (_model.IsPackageAddedToProject(info.Name))
                {
                    caption = ImguiUtil.WrapWithColor(
                        info.Name, _view.Skin.Theme.DraggableItemAlreadyAddedColor);
                }
                else
                {
                    caption = info.Name;
                }
            }

            return new ListItemData()
            {
                Caption = caption,
                Model = info
            };
        }

        public enum PackagesSortMethod
        {
            Name,
            InstallDate,
            ReleasePublishDate
        }

        public enum ReleasesSortMethod
        {
            Name,
            FileModificationDate,
            Size,
            ReleaseDate
        }
    }
}