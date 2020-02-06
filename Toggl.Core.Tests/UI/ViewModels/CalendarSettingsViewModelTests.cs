using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using NSubstitute;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Toggl.Core.Exceptions;
using Toggl.Core.Tests.Generators;
using Toggl.Core.Tests.TestExtensions;
using Toggl.Core.UI.Collections;
using Toggl.Core.UI.ViewModels.Calendar;
using Toggl.Core.UI.ViewModels.Selectable;
using Toggl.Core.UI.ViewModels.Settings;
using Toggl.Core.UI.Views;
using Toggl.Shared;
using Toggl.Shared.Extensions;
using Xunit;

namespace Toggl.Core.Tests.UI.ViewModels
{
    using ImmutableCalendarSectionModel = IImmutableList<SectionModel<UserCalendarSourceViewModel, SelectableUserCalendarViewModel>>;

    public sealed class CalendarSettingsViewModelTests
    {
        public abstract class CalendarSettingsViewModelTest : BaseViewModelTests<CalendarSettingsViewModel>
        {
            protected override CalendarSettingsViewModel CreateViewModel()
                => new CalendarSettingsViewModel(UserPreferences, InteractorFactory, OnboardingStorage, AnalyticsService, NavigationService, RxActionFactory, PermissionsChecker, SchedulerProvider);
        }

        public sealed class TheConstructor : CalendarSettingsViewModelTest
        {
            [Theory, LogIfTooSlow]
            [ConstructorData]
            public void ThrowsIfAnyOfTheArgumentsIsNull(
                bool useUserPreferences,
                bool useInteractorFactory,
                bool useOnboardingStorage,
                bool useAnalyticsService,
                bool useNavigationService,
                bool useRxActionFactory,
                bool usePermissionsChecker,
                bool useSchedulerProvider)
            {
                Action tryingToConstructWithEmptyParameters =
                    () => new CalendarSettingsViewModel(
                        useUserPreferences ? UserPreferences : null,
                        useInteractorFactory ? InteractorFactory : null,
                        useOnboardingStorage ? OnboardingStorage : null,
                        useAnalyticsService ? AnalyticsService : null,
                        useNavigationService ? NavigationService : null,
                        useRxActionFactory ? RxActionFactory : null,
                        usePermissionsChecker ? PermissionsChecker : null,
                        useSchedulerProvider ? SchedulerProvider : null
                    );

                tryingToConstructWithEmptyParameters.Should().Throw<ArgumentNullException>();
            }
        }

        public sealed class TheInitializeMethod : CalendarSettingsViewModelTest
        {
            [Fact, LogIfTooSlow]
            public async Task FillsTheCalendarList()
            {
                var userCalendarsObservable = Enumerable
                    .Range(0, 9)
                    .Select(id => new UserCalendar(
                        id.ToString(),
                        $"Calendar #{id}",
                        $"Source #{id % 3}",
                        false))
                    .Apply(Observable.Return);
                InteractorFactory.GetUserCalendars().Execute().Returns(userCalendarsObservable);
                UserPreferences.CalendarIntegrationEnabled().Returns(true);
                var viewModel = CreateViewModel();

                await viewModel.Initialize();
                TestScheduler.Start();

                var calendars = await viewModel.Calendars.FirstAsync();
                calendars.Should().HaveCount(3);
                calendars.ForEach(group => group.Items.Should().HaveCount(3));
            }

            [Property]
            public void SetsProperCalendarsAsSelected(
                NonEmptySet<NonEmptyString> strings0,
                NonEmptySet<NonEmptyString> strings1)
            {
                var enabledCalendarIds = strings0.Get.Select(str => str.Get).ToList();
                var unenabledCalendarIds = strings1.Get.Select(str => str.Get).ToList();
                var allCalendarIds = enabledCalendarIds.Concat(unenabledCalendarIds).ToList();
                UserPreferences.EnabledCalendarIds().Returns(enabledCalendarIds);
                var userCalendars = allCalendarIds
                    .Select(id => new UserCalendar(
                        id,
                        "Does not matter",
                        "Does not matter, pt.2"
                    ));
                InteractorFactory
                    .GetUserCalendars()
                    .Execute()
                    .Returns(Observable.Return(userCalendars));
                var viewModel = CreateViewModel();

                viewModel.Initialize().Wait();

                var calendars = viewModel.Calendars.FirstAsync().Wait();
                foreach (var calendarGroup in calendars)
                {
                    foreach (var calendar in calendarGroup.Items)
                    {
                        if (enabledCalendarIds.Contains(calendar.Id))
                            calendar.Selected.Should().BeTrue();
                    }
                }
            }

            [Fact, LogIfTooSlow]
            public async Task SetsTheEnabledCalendarsToNullWhenCalendarPermissionsWereNotGranted()
            {
                PermissionsChecker.CalendarPermissionGranted.Returns(Observable.Return(false));
                UserPreferences.EnabledCalendarIds().Returns(new List<string>());
                var viewModel = CreateViewModel();

                await viewModel.Initialize();
                TestScheduler.Start();

                UserPreferences.Received().SetEnabledCalendars(Arg.Is<string[]>(strings => strings == null || strings.Length == 0));
            }

            [Fact, LogIfTooSlow]
            public async Task DoesNotSetTheEnabledCalendarsToNullWhenCalendarPermissionsWereGranted()
            {
                PermissionsChecker.CalendarPermissionGranted.Returns(Observable.Return(true));
                UserPreferences.EnabledCalendarIds().Returns(new List<string>());
                var viewModel = CreateViewModel();

                await viewModel.Initialize();
                TestScheduler.Start();

                UserPreferences.DidNotReceive().SetEnabledCalendars(Arg.Is<string[]>(strings => strings == null || strings.Length == 0));
            }

            [Fact, LogIfTooSlow]
            public async Task DisablesTheCalendarIntegrationIfCalendarPermissionIsNotGranted()
            {
                PermissionsChecker.CalendarPermissionGranted.Returns(Observable.Return(false));
                UserPreferences.EnabledCalendarIds().Returns(new List<string>());
                var viewModel = CreateViewModel();

                await viewModel.Initialize();
                TestScheduler.Start();

                UserPreferences.Received().SetCalendarIntegrationEnabled(false);
            }

            [Fact, LogIfTooSlow]
            public async Task HandlesNotAuthorizedException()
            {
                InteractorFactory
                    .GetUserCalendars()
                    .Execute()
                    .Returns(Observable.Throw<IEnumerable<UserCalendar>>(new NotAuthorizedException("")));

                await ViewModel.Initialize();
                var calendars = await ViewModel.Calendars.FirstAsync();

                calendars.Should().HaveCount(0);
            }
        }

        public sealed class TheSaveAction : CalendarSettingsViewModelTest
        {
            [Fact, LogIfTooSlow]
            public async Task SavesTheSelectedCalendars()
            {
                PermissionsChecker.CalendarPermissionGranted.Returns(Observable.Return(true));
                UserPreferences.CalendarIntegrationEnabled().Returns(true);
                var userCalendars = Enumerable
                    .Range(0, 9)
                    .Select(id => new UserCalendar(
                        id.ToString(),
                        $"Calendar #{id}",
                        $"Source #{id % 3}",
                        false));
                InteractorFactory
                    .GetUserCalendars()
                    .Execute()
                    .Returns(Observable.Return(userCalendars));
                await ViewModel.Initialize();
                var selectedIds = new[] { "0", "2", "4", "7" };
                var calendars = userCalendars
                    .Where(calendar => selectedIds.Contains(calendar.Id))
                    .Select(calendar => new SelectableUserCalendarViewModel(calendar, false))
                    .ToArray();

                ViewModel.SelectCalendar.ExecuteSequentially(calendars)
                    .AppendAction(ViewModel.Save)
                    .Subscribe();

                TestScheduler.Start();

                UserPreferences.Received().SetEnabledCalendars(selectedIds);
            }
        }

        public sealed class TheViewAppearedMethod : CalendarSettingsViewModelTest
        {
            [Fact, LogIfTooSlow]
            public async Task AsksForCalendarPermissionIfIsFirstTimeOpeningThisView()
            {
                var viewModel = CreateViewModel();
                viewModel.AttachView(Substitute.For<IView>());
                await viewModel.Initialize();

                viewModel.ViewAppeared();

                viewModel.View.Received().RequestCalendarAuthorization();
            }


            [Fact, LogIfTooSlow]
            public async Task DoesNotAskForPermissionIfItWasAskedAlready()
            {
                OnboardingStorage.CalendarPermissionWasAskedBefore().Returns(true);
                var viewModel = CreateViewModel();
                viewModel.AttachView(Substitute.For<IView>());
                await viewModel.Initialize();

                viewModel.ViewAppeared();

                viewModel.View.DidNotReceive().RequestCalendarAuthorization();
            }
        }

        public sealed class TheSelectCalendarAction : CalendarSettingsViewModelTest
        {
            public TheSelectCalendarAction()
            {
                var userCalendars = Enumerable
                    .Range(0, 9)
                    .Select(id => new UserCalendar(
                        id.ToString(),
                        $"Calendar #{id}",
                        $"Source #{id % 3}",
                        true));
                var userCalendarsObservable = Observable.Return(userCalendars);
                InteractorFactory.GetUserCalendars().Execute().Returns(userCalendarsObservable);
                PermissionsChecker.CalendarPermissionGranted.Returns(Observable.Return(true));
                UserPreferences.CalendarIntegrationEnabled().Returns(true);
            }

            [Fact, LogIfTooSlow]
            public async Task MarksTheCalendarAsSelectedIfItIsNotSelected()
            {
                await ViewModel.Initialize();
                var viewModelCalendars = await ViewModel.Calendars.FirstAsync();
                var calendarToBeSelected = viewModelCalendars.First().Items.First();

                ViewModel.SelectCalendar.Execute(calendarToBeSelected);
                TestScheduler.Start();

                calendarToBeSelected.Selected.Should().BeTrue();
            }

            [Fact, LogIfTooSlow]
            public async Task MarksTheCalendarAsNotSelectedIfItIsSelected()
            {
                await ViewModel.Initialize();
                var viewModelCalendars = await ViewModel.Calendars.FirstAsync();
                var calendarToBeSelected = viewModelCalendars.First().Items.First();

                ViewModel.SelectCalendar.Execute(calendarToBeSelected); //Select the calendar
                TestScheduler.Start();
                ViewModel.SelectCalendar.Execute(calendarToBeSelected); //Deselect the calendar
                TestScheduler.Start();

                calendarToBeSelected.Selected.Should().BeFalse();
            }
        }

        public sealed class TheToggleCalendarIntegrationAction
        {
            public sealed class WhenDisablingTheCalendarIntegration : CalendarSettingsViewModelTest
            {
                public WhenDisablingTheCalendarIntegration()
                {
                    UserPreferences.CalendarIntegrationEnabled().Returns(true);
                    PermissionsChecker.CalendarPermissionGranted.Returns(Observable.Return(true));
                }

                [Fact, LogIfTooSlow]
                public async Task SetsCalendarIntegrationToDisabled()
                {
                    await ViewModel.Initialize();

                    ViewModel.ToggleCalendarIntegration.Execute();
                    TestScheduler.Start();

                    UserPreferences.Received().SetCalendarIntegrationEnabled(false);
                }

                [Fact, LogIfTooSlow]
                public async Task SetsEnabledCalendarsToNull()
                {
                    await ViewModel.Initialize();

                    ViewModel.ToggleCalendarIntegration.Execute();
                    TestScheduler.Start();

                    UserPreferences.Received().SetEnabledCalendars(null);
                }

                [Fact, LogIfTooSlow]
                public async Task RemovesAllCalendarsFromTheCalendarsProperty()
                {
                    var userCalendars = Enumerable
                        .Range(0, 9)
                        .Select(id => new UserCalendar(
                            id.ToString(),
                            $"Calendar #{id}",
                            $"Source #{id % 3}",
                            false));
                    var userCalendarsObservable = Observable.Return(userCalendars);
                    InteractorFactory.GetUserCalendars().Execute().Returns(userCalendarsObservable);
                    var calendarsObserver = TestScheduler.CreateObserver<ImmutableCalendarSectionModel>();
                    ViewModel.Calendars.Subscribe(calendarsObserver);
                    await ViewModel.Initialize();

                    UserPreferences.CalendarIntegrationEnabled().Returns(false);
                    ViewModel.ToggleCalendarIntegration.Execute();
                    TestScheduler.Start();

                    //The first calendar list is empty, so the second one actually holds initial values
                    calendarsObserver.Values().ElementAt(1).Should().HaveCount(3);
                    calendarsObserver.Values().Last().Should().BeEmpty();
                }
            }

            public sealed class WhenEnablingTheCalendarIntegration : CalendarSettingsViewModelTest
            {
                public WhenEnablingTheCalendarIntegration()
                {
                    UserPreferences.CalendarIntegrationEnabled().Returns(false);
                    PermissionsChecker.CalendarPermissionGranted.Returns(Observable.Return(true));
                }

                [Fact, LogIfTooSlow]
                public async Task SetsCalendarIntegrationEnabled()
                {
                    await ViewModel.Initialize();

                    ViewModel.ToggleCalendarIntegration.Execute();
                    TestScheduler.Start();

                    UserPreferences.Received().SetCalendarIntegrationEnabled(true);
                }

                [Fact, LogIfTooSlow]
                public async Task ReloadsTheCalendars()
                {
                    UserPreferences
                        .When(preferences => preferences.SetCalendarIntegrationEnabled(true))
                        .Do(_ => UserPreferences.CalendarIntegrationEnabled().Returns(true));
                    await ViewModel.Initialize();

                    ViewModel.ToggleCalendarIntegration.Execute();
                    TestScheduler.Start();

                    InteractorFactory.GetUserCalendars().Received().Execute();
                }

                public sealed class WhenCalendarPermissionIsNotGranted : CalendarSettingsViewModelTest
                {
                    public WhenCalendarPermissionIsNotGranted()
                    {
                        UserPreferences.CalendarIntegrationEnabled().Returns(false);
                        PermissionsChecker.CalendarPermissionGranted.Returns(Observable.Return(false));
                    }

                    [Fact, LogIfTooSlow]
                    public async Task AsksForCalendarPermission()
                    {
                        await ViewModel.Initialize();
                        var view = Substitute.For<IView>();
                        ViewModel.AttachView(view);

                        ViewModel.ToggleCalendarIntegration.Execute();
                        TestScheduler.Start();

                        view.Received().RequestCalendarAuthorization(true);
                    }

                    [Fact, LogIfTooSlow]
                    public async Task EnablesCalendarIntegrationIfPermissionRequestIsApproved()
                    {
                        await ViewModel.Initialize();
                        var view = Substitute.For<IView>();
                        view.RequestCalendarAuthorization(true).Returns(Observable.Return(true));
                        ViewModel.AttachView(view);

                        ViewModel.ToggleCalendarIntegration.Execute();
                        TestScheduler.Start();

                        UserPreferences.Received().SetCalendarIntegrationEnabled(true);
                    }

                    [Fact, LogIfTooSlow]
                    public async Task ReloadsTheCalendarsIfPermissionRequestIsApproved()
                    {
                        await ViewModel.Initialize();
                        var view = Substitute.For<IView>();
                        view.RequestCalendarAuthorization(true).Returns(Observable.Return(true));
                        view.When(view => view.RequestCalendarAuthorization(true))
                            .Do(_ => UserPreferences.CalendarIntegrationEnabled().Returns(true));
                        ViewModel.AttachView(view);

                        ViewModel.ToggleCalendarIntegration.Execute();
                        TestScheduler.Start();

                        InteractorFactory.GetUserCalendars().Received().Execute();
                    }

                    [Fact, LogIfTooSlow]
                    public async Task DoesNotEnableCalendarIntegrationIfPermissionRequestIsDenied()
                    {
                        await ViewModel.Initialize();
                        var view = Substitute.For<IView>();
                        view.RequestCalendarAuthorization(true).Returns(Observable.Return(false));
                        ViewModel.AttachView(view);

                        ViewModel.ToggleCalendarIntegration.Execute();
                        TestScheduler.Start();

                        UserPreferences.DidNotReceive().SetCalendarIntegrationEnabled(true);
                    }

                    [Fact, LogIfTooSlow]
                    public async Task DoesNotReloadCalendarsIfPermissionRequestIsDenied()
                    {
                        await ViewModel.Initialize();
                        var view = Substitute.For<IView>();
                        view.RequestCalendarAuthorization(true).Returns(Observable.Return(false));
                        ViewModel.AttachView(view);

                        ViewModel.ToggleCalendarIntegration.Execute();
                        TestScheduler.Start();

                        InteractorFactory.GetUserCalendars().DidNotReceive().Execute();
                    }
                }
            }
        }

        public sealed class TheAnalytics : CalendarSettingsViewModelTest
        {
            public TheAnalytics()
            {
                PermissionsChecker.CalendarPermissionGranted.Returns(Observable.Return(true));
                UserPreferences.CalendarIntegrationEnabled().Returns(true);
            }

            [Fact, LogIfTooSlow]
            public async Task TracksNumberOfLinkedCalendarsChanged()
            {
                var userCalendars = Enumerable
                    .Range(0, 9)
                    .Select(id => new UserCalendar(
                        id.ToString(),
                        $"Calendar #{id}",
                        $"Source #{id % 3}",
                        false));

                InteractorFactory
                    .GetUserCalendars()
                    .Execute()
                    .Returns(Observable.Return(userCalendars));

                await ViewModel.Initialize();
                var selectedIds = new[] { "0", "2", "4", "7" };

                var calendars = userCalendars
                    .Where(calendar => selectedIds.Contains(calendar.Id))
                    .Select(calendar => new SelectableUserCalendarViewModel(calendar, false));

                ViewModel.SelectCalendar.ExecuteSequentially(calendars)
                    .AppendAction(ViewModel.Save)
                    .Subscribe();

                TestScheduler.Start();

                AnalyticsService.NumberOfLinkedCalendarsChanged.Received().Track(4);
                AnalyticsService.NumberOfLinkedCalendarsNewUser.DidNotReceiveWithAnyArgs().Track(4);
            }

            [Fact, LogIfTooSlow]
            public async Task TracksNumberOfLinkedCalendarsNewUser()
            {
                var initialSelectedIds = new List<string> { };
                UserPreferences.EnabledCalendarIds().Returns(initialSelectedIds);
                OnboardingStorage.IsFirstTimeConnectingCalendars().Returns(true);

                var userCalendars = Enumerable
                    .Range(0, 9)
                    .Select(id => new UserCalendar(
                        id.ToString(),
                        $"Calendar #{id}",
                        $"Source #{id % 3}",
                        false));

                InteractorFactory
                    .GetUserCalendars()
                    .Execute()
                    .Returns(Observable.Return(userCalendars));

                await ViewModel.Initialize();
                var selectedIds = new[] { "2", "4", "7" };

                var calendars = userCalendars
                    .Where(calendar => selectedIds.Contains(calendar.Id))
                    .Select(calendar => new SelectableUserCalendarViewModel(calendar, false));

                ViewModel.SelectCalendar.ExecuteSequentially(calendars)
                    .AppendAction(ViewModel.Save)
                    .Subscribe();

                TestScheduler.Start();

                ViewModel.ViewDisappeared();

                AnalyticsService.NumberOfLinkedCalendarsNewUser.Received().Track(3);
                AnalyticsService.NumberOfLinkedCalendarsChanged.DidNotReceiveWithAnyArgs().Track(3);
            }
        }
    }
}
