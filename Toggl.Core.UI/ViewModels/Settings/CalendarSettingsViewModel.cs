﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using Toggl.Core.Analytics;
using Toggl.Core.Exceptions;
using Toggl.Core.Interactors;
using Toggl.Core.Services;
using Toggl.Core.UI.Collections;
using Toggl.Core.UI.Navigation;
using Toggl.Core.UI.Services;
using Toggl.Core.UI.ViewModels.Calendar;
using Toggl.Core.UI.ViewModels.Selectable;
using Toggl.Shared;
using Toggl.Shared.Extensions;
using Toggl.Storage.Settings;

namespace Toggl.Core.UI.ViewModels.Settings
{
    using CalendarSectionModel = SectionModel<UserCalendarSourceViewModel, SelectableUserCalendarViewModel>;
    using ImmutableCalendarSectionModel = IImmutableList<SectionModel<UserCalendarSourceViewModel, SelectableUserCalendarViewModel>>;

    [Preserve(AllMembers = true)]
    public class CalendarSettingsViewModel : ViewModel
    {
        private readonly IRxActionFactory rxActionFactory;
        private readonly IUserPreferences userPreferences;
        private readonly IAnalyticsService analyticsService;
        private readonly IOnboardingStorage onboardingStorage;
        private readonly IInteractorFactory interactorFactory;
        private readonly IPermissionsChecker permissionsChecker;

        private readonly CompositeDisposable disposeBag = new CompositeDisposable();
        private readonly ISubject<ImmutableCalendarSectionModel> calendarsSubject =
            new BehaviorSubject<ImmutableCalendarSectionModel>(ImmutableList.Create<CalendarSectionModel>());

        private HashSet<string> initialSelectedCalendarIds { get; } = new HashSet<string>();
        private HashSet<string> selectedCalendarIds { get; } = new HashSet<string>();

        public IObservable<bool> CalendarIntegrationEnabled { get; }
        public IObservable<ImmutableCalendarSectionModel> Calendars { get; }

        public ViewAction ToggleCalendarIntegration { get; }
        public ViewAction Save { get; }
        public InputAction<SelectableUserCalendarViewModel> SelectCalendar { get; }

        public CalendarSettingsViewModel(
            IUserPreferences userPreferences,
            IInteractorFactory interactorFactory,
            IOnboardingStorage onboardingStorage,
            IAnalyticsService analyticsService,
            INavigationService navigationService,
            IRxActionFactory rxActionFactory,
            IPermissionsChecker permissionsChecker,
            ISchedulerProvider schedulerProvider)
            : base(navigationService)
        {
            Ensure.Argument.IsNotNull(userPreferences, nameof(userPreferences));
            Ensure.Argument.IsNotNull(rxActionFactory, nameof(rxActionFactory));
            Ensure.Argument.IsNotNull(analyticsService, nameof(analyticsService));
            Ensure.Argument.IsNotNull(interactorFactory, nameof(interactorFactory));
            Ensure.Argument.IsNotNull(onboardingStorage, nameof(onboardingStorage));
            Ensure.Argument.IsNotNull(schedulerProvider, nameof(schedulerProvider));
            Ensure.Argument.IsNotNull(permissionsChecker, nameof(permissionsChecker));

            this.rxActionFactory = rxActionFactory;
            this.userPreferences = userPreferences;
            this.analyticsService = analyticsService;
            this.onboardingStorage = onboardingStorage;
            this.interactorFactory = interactorFactory;
            this.permissionsChecker = permissionsChecker;

            Save = rxActionFactory.FromAction(save);
            SelectCalendar = rxActionFactory.FromAction<SelectableUserCalendarViewModel>(selectCalendar);
            ToggleCalendarIntegration = rxActionFactory.FromAsync(toggleCalendarIntegration);

            permissionsChecker.CalendarPermissionGranted
                .Subscribe(disableCalendarIntegartionIfNeeded)
                .DisposedBy(disposeBag);

            Calendars = calendarsSubject.AsObservable().DistinctUntilChanged();
            CalendarIntegrationEnabled = userPreferences.CalendarIntegrationEnabledObservable
                .StartWith(userPreferences.CalendarIntegrationEnabled())
                .DistinctUntilChanged();
        }

        public override void ViewAppeared()
        {
            base.ViewAppeared();

            if (!onboardingStorage.CalendarPermissionWasAskedBefore())
            {
                View.RequestCalendarAuthorization(false)
                    .Subscribe(_ => onboardingStorage.SetCalendarPermissionWasAskedBefore())
                    .DisposedBy(disposeBag);
            }
        }

        public override void ViewDestroyed()
        {
            base.ViewDestroyed();

            disposeBag.Dispose();
        }

        public override Task<bool> CloseWithDefaultResult()
        {
            userPreferences.SetEnabledCalendars(initialSelectedCalendarIds.ToArray());
            return base.CloseWithDefaultResult();
        }

        private void disableCalendarIntegartionIfNeeded(bool calendarPermissionGranted)
        {
            if (!calendarPermissionGranted)
                userPreferences.SetCalendarIntegrationEnabled(false);
        }

        private async Task reloadCalendars()
        {
            if (!userPreferences.CalendarIntegrationEnabled())
            {
                calendarsSubject.OnNext(ImmutableList.Create<CalendarSectionModel>());
                return;
            }

            var calendars = await interactorFactory
                .GetUserCalendars()
                .Execute()
                .Catch((NotAuthorizedException _) => Observable.Return(new List<UserCalendar>()))
                .Select(group);

            calendarsSubject.OnNext(calendars);
        }

        private ImmutableCalendarSectionModel group(IEnumerable<UserCalendar> calendars)
            => calendars
                .Select(toSelectable)
                .GroupBy(calendar => calendar.SourceName)
                .Select(group =>
                    new CalendarSectionModel(
                        new UserCalendarSourceViewModel(group.First().SourceName),
                        group.OrderBy(calendar => calendar.Name)
                    )
                )
                .ToImmutableList();

        private SelectableUserCalendarViewModel toSelectable(UserCalendar calendar)
            => new SelectableUserCalendarViewModel(calendar, selectedCalendarIds.Contains(calendar.Id));


        private void save()
        {
            if (!userPreferences.CalendarIntegrationEnabled())
                selectedCalendarIds.Clear();

            userPreferences.SetEnabledCalendars(selectedCalendarIds.ToArray());

            if (onboardingStorage.IsFirstTimeConnectingCalendars() && initialSelectedCalendarIds.Count == 0)
            {
                analyticsService.NumberOfLinkedCalendarsNewUser.Track(selectedCalendarIds.Count);
            }
            else if (!selectedCalendarIds.SetEquals(initialSelectedCalendarIds))
            {
                analyticsService.NumberOfLinkedCalendarsChanged.Track(selectedCalendarIds.Count);
            }

            onboardingStorage.SetIsFirstTimeConnectingCalendars();

            Close(Unit.Default);
        }

        private void selectCalendar(SelectableUserCalendarViewModel calendar)
        {
            if (selectedCalendarIds.Contains(calendar.Id))
                selectedCalendarIds.Remove(calendar.Id);
            else
                selectedCalendarIds.Add(calendar.Id);
            calendar.Selected = !calendar.Selected;
        }

        private async Task toggleCalendarIntegration()
        {
            //Disabling the calendar integration
            if (userPreferences.CalendarIntegrationEnabled())
            {
                userPreferences.SetCalendarIntegrationEnabled(false);
                reloadCalendars();
                return;
            }

            //Enabling the calendar integration
            var calendarPermissionGranted = await permissionsChecker.CalendarPermissionGranted;
            if (!calendarPermissionGranted)
            {
                var permissionGranted = await View.RequestCalendarAuthorization(true);
                if (!permissionGranted)
                    return;
            }
            userPreferences.SetCalendarIntegrationEnabled(true);
            reloadCalendars();
        }
    }
}
