using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Toggl.Core.UI.ViewModels.Calendar;
using Toggl.Core.UI.ViewModels.Reports;
using Toggl.Shared;

namespace Toggl.Core.UI.ViewModels
{
    [Preserve(AllMembers = true)]
    public sealed class MainTabBarViewModel : ViewModel
    {
        public Lazy<ViewModel> MainViewModel { get; }
        public Lazy<ViewModel> ReportsViewModel { get; }
        public Lazy<ViewModel> CalendarViewModel { get; }

        public IImmutableList<Lazy<ViewModel>> Tabs { get; }

        public MainTabBarViewModel(UIDependencyContainer container)
            : base(container.NavigationService)
        {
            MainViewModel = new Lazy<ViewModel>(() => new MainViewModel(
                container.DataSource,
                container.SyncManager,
                container.TimeService,
                container.RatingService,
                container.UserPreferences,
                container.AnalyticsService,
                container.OnboardingStorage,
                container.InteractorFactory,
                container.NavigationService,
                container.RemoteConfigService,
                container.AccessibilityService,
                container.UpdateRemoteConfigCacheService,
                container.AccessRestrictionStorage,
                container.SchedulerProvider,
                container.RxActionFactory,
                container.PermissionsChecker,
                container.BackgroundService,
                container.PlatformInfo,
                container.WidgetsService));

            ReportsViewModel = new Lazy<ViewModel>(() => new ReportsViewModel(
                container.DataSource,
                container.NavigationService,
                container.InteractorFactory,
                container.SchedulerProvider,
                container.RxActionFactory,
                container.AnalyticsService,
                container.TimeService,
                container.DateRangeShortcutsService));

            CalendarViewModel = new Lazy<ViewModel>(() => new CalendarViewModel(
                container.DataSource,
                container.TimeService,
                container.RxActionFactory,
                container.UserPreferences,
                container.AnalyticsService,
                container.BackgroundService,
                container.InteractorFactory,
                container.SchedulerProvider,
                container.NavigationService));

            Tabs = getViewModels().ToImmutableList();
        }

        private IEnumerable<Lazy<ViewModel>> getViewModels()
        {
            yield return MainViewModel;
            yield return ReportsViewModel;
            yield return CalendarViewModel;
        }
    }
}
