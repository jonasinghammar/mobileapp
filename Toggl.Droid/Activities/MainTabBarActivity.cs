using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Runtime;
using Android.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using Android.Util;
using Toggl.Core.Analytics;
using Toggl.Core.UI.ViewModels;
using Toggl.Core.UI.ViewModels.Calendar;
using Toggl.Core.UI.ViewModels.Reports;
using Toggl.Droid.Extensions;
using Toggl.Droid.Extensions.Reactive;
using Toggl.Droid.Fragments;
using Toggl.Droid.Helper;
using Toggl.Droid.Presentation;
using Toggl.Shared.Extensions;
using Fragment = AndroidX.Fragment.App.Fragment;
using System.Threading.Tasks;

namespace Toggl.Droid.Activities
{
    [Activity(Theme = "@style/Theme.Splash",
              ScreenOrientation = ScreenOrientation.Portrait,
              ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize)]
    public sealed partial class MainTabBarActivity : ReactiveActivity<MainTabBarViewModel>
    {
        public static readonly string StartingTabExtra = "StartingTabExtra";
        public static readonly string WorkspaceIdExtra = "WorkspaceIdExtra";
        public static readonly string StartDateExtra = "StartDateExtra";
        public static readonly string EndDateExtra = "EndDateExtra";

        private Fragment activeFragment;
        private bool activityResumedBefore = false;
        private int? requestedInitialTab;

        public MainTabBarActivity() : base(
            Resource.Layout.MainTabBarActivity,
            Resource.Style.AppTheme,
            Transitions.Fade)
        { }

        public MainTabBarActivity(IntPtr javaReference, JniHandleOwnership transfer)
            : base(javaReference, transfer)
        {
        }

        protected override void RestoreViewModelStateFromBundle(Bundle bundle)
        {
            base.RestoreViewModelStateFromBundle(bundle);

            restoreFragmentsViewModels();
            showInitialFragment(getInitialTab(Intent, bundle));
        }

        protected override void InitializeBindings()
        {
            navigationView
                .Rx()
                .ItemSelected()
                .Subscribe(onTabSelected)
                .DisposedBy(DisposeBag);
        }

        private int getInitialTab(Intent intent, Bundle bundle = null)
        {
            var intentTab = intent.GetIntExtra(StartingTabExtra, Resource.Id.MainTabTimerItem);
            if (intentTab != Resource.Id.MainTabTimerItem || bundle == null)
                return intentTab;

            var bundleTab = bundle.GetInt(StartingTabExtra, Resource.Id.MainTabTimerItem);
            return bundleTab;
        }

        protected override void OnNewIntent(Intent intent)
        {
            base.OnNewIntent(intent);
            requestedInitialTab = getInitialTab(intent);
        }

        protected override void OnSaveInstanceState(Bundle outState)
        {
            outState.PutInt(StartingTabExtra, navigationView.SelectedItemId);
            base.OnSaveInstanceState(outState);
        }

        private void restoreFragmentsViewModels()
        {
            foreach (var frag in SupportFragmentManager.Fragments)
            {
                switch (frag)
                {
                    case MainFragment mainFragment:
                        mainFragment.ViewModel = ViewModel.MainViewModel.Value as MainViewModel;
                        break;
                    case ReportsFragment reportsFragment:
                        reportsFragment.ViewModel = ViewModel.ReportsViewModel.Value as ReportsViewModel;
                        break;
                    case CalendarFragment calendarFragment:
                        calendarFragment.ViewModel = ViewModel.CalendarViewModel.Value as CalendarViewModel;
                        break;
                }
            }
        }

        protected override void OnResume()
        {
            base.OnResume();

            if (!activityResumedBefore)
            {
                navigationView.SelectedItemId = requestedInitialTab ?? Resource.Id.MainTabTimerItem;
                activityResumedBefore = true;
                requestedInitialTab = null;
                return;
            }

            if (requestedInitialTab == null) return;
            navigationView.SelectedItemId = requestedInitialTab.Value;
            requestedInitialTab = null;
        }

        private async Task<Fragment> getCachedFragment(int itemId)
        {
            var cachedFragment = SupportFragmentManager.Fragments.FirstOrDefault(f => f.Tag == itemId.ToString());
            if (cachedFragment != null)
                return cachedFragment;

            return itemId switch
            {
                Resource.Id.MainTabTimerItem => await Task.Run<Fragment>(async () =>
                {
                    var viewModel = ViewModel.MainViewModel.Value as MainViewModel;
                    await viewModel.Initialize();
                    return new MainFragment { ViewModel = viewModel };
                }),
                Resource.Id.MainTabReportsItem => await Task.Run<Fragment>(async () =>
                {
                    var viewModel = ViewModel.ReportsViewModel.Value as ReportsViewModel;
                    await viewModel.Initialize();
                    return new ReportsFragment { ViewModel = viewModel };
                }),
                Resource.Id.MainTabCalendarItem => await Task.Run<Fragment>(async () =>
                {
                    var viewModel = ViewModel.CalendarViewModel.Value as CalendarViewModel;
                    await viewModel.Initialize();
                    return new CalendarFragment { ViewModel = viewModel };
                }),
                _ => throw new ArgumentException($"Unexpected item id {itemId}")
            };
        }

        public override void OnBackPressed()
        {
            if (navigationView.SelectedItemId == Resource.Id.MainTabTimerItem)
            {
                FinishAfterTransition();
                return;
            }

            if (navigationView.SelectedItemId == Resource.Id.MainTabCalendarItem)
            {
                var calendarFragment = getCachedFragment(Resource.Id.MainTabCalendarItem) as IBackPressHandler;
                if (calendarFragment?.HandledBackPress() == true)
                    return;
            }
            
            showFragment(Resource.Id.MainTabTimerItem);
            navigationView.SelectedItemId = Resource.Id.MainTabTimerItem;
        }

        private async void onTabSelected(IMenuItem item)
        {
            if (item.ItemId != navigationView.SelectedItemId)
            {
                await showFragment(item.ItemId);
                return;
            }

            var fragment = await getCachedFragment(item.ItemId);
            if (fragment is IScrollableToStart scrollableToTop)
            {
                scrollableToTop.ScrollToStart();
            }
        }

        private async Task showFragment(int fragmentId)
        {
            SupportFragmentManager.ExecutePendingTransactions();
            var transaction = SupportFragmentManager.BeginTransaction();
            var fragment = await getCachedFragment(fragmentId);

            if (activeFragment is MainFragment mainFragmentToHide)
                mainFragmentToHide.SetFragmentIsVisible(false);

            if (fragment.IsAdded)
            {
                transaction
                    .Hide(activeFragment)
                    .Show(fragment);
            }
            else
            {
                transaction
                    .Add(Resource.Id.CurrentTabFragmmentContainer, fragment, fragmentId.ToString())
                    .Hide(activeFragment);
            }

            transaction.Commit();

            if (fragment is MainFragment mainFragmentToShow)
                mainFragmentToShow.SetFragmentIsVisible(true);

            activeFragment = fragment;
        }

        private async Task showInitialFragment(int initialTabItemId)
        {
            SupportFragmentManager.RemoveAllFragments();
            SupportFragmentManager.ExecutePendingTransactions();

            var initialFragment = await getCachedFragment(initialTabItemId);
            if (!initialFragment.IsAdded)
            {
                SupportFragmentManager
                    .BeginTransaction()
                    .Add(Resource.Id.CurrentTabFragmmentContainer, initialFragment, initialTabItemId.ToString())
                    .Commit();
            }

            if (initialFragment is MainFragment mainFragment)
                mainFragment.SetFragmentIsVisible(true);

            if (!(initialFragment is CalendarFragment))
            {
                ChangeBottomBarVisibility(true);
            }

            navigationView.SelectedItemId = initialTabItemId;
            activeFragment = initialFragment;
        }

        public void ChangeBottomBarVisibility(bool isVisible)
        {
            navigationView.Visibility = isVisible.ToVisibility();
        }
    }
}
