using Android.App;
using Android.Content.PM;
using Android.Runtime;
using Android.Views;
using System;
using Toggl.Core.UI.ViewModels.Settings;
using Toggl.Droid.Extensions.Reactive;
using Toggl.Droid.Presentation;
using Toggl.Shared.Extensions;

namespace Toggl.Droid.Activities
{
    [Activity(Theme = "@style/Theme.Splash",
              WindowSoftInputMode = SoftInput.AdjustResize,
              ScreenOrientation = ScreenOrientation.Portrait,
              ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize)]
    public partial class CalendarSettingsActivity : ReactiveActivity<CalendarSettingsViewModel>
    {
        protected CalendarSettingsActivity(ActivityTransitionSet transitions) : base(
            Resource.Layout.CalendarSettingsActivity,
            Resource.Style.AppTheme,
            transitions)
        {
        }

        public CalendarSettingsActivity() : base(
            Resource.Layout.CalendarSettingsActivity,
            Resource.Style.AppTheme,
            Transitions.SlideInFromRight)
        { }

        public CalendarSettingsActivity(IntPtr javaReference, JniHandleOwnership transfer)
            : base(javaReference, transfer)
        {
        }

        protected override void InitializeBindings()
        {
            toggleCalendarsView.Rx()
                .BindAction(ViewModel.ToggleCalendarIntegration)
                .DisposedBy(DisposeBag);

            toggleCalendarsSwitch.Rx()
                .BindAction(ViewModel.ToggleCalendarIntegration)
                .DisposedBy(DisposeBag);

            ViewModel
                .Calendars
                .Subscribe(userCalendarsAdapter.Rx().Items())
                .DisposedBy(DisposeBag);

            ViewModel.CalendarIntegrationEnabled
                .Subscribe(toggleCalendarsSwitch.Rx().CheckedObserver(true))
                .DisposedBy(DisposeBag);

            ViewModel.CalendarIntegrationEnabled
                .Subscribe(calendarsContainer.Rx().IsVisible())
                .DisposedBy(DisposeBag);

            userCalendarsAdapter
                .ItemTapObservable
                .Subscribe(ViewModel.SelectCalendar.Inputs)
                .DisposedBy(DisposeBag);
        }
    }
}
