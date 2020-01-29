using Android.Views;
using Android.Widget;
using System;
using System.Reactive.Linq;
using Android.App;
using Android.Content.PM;
using Android.Runtime;
using Toggl.Core.Sync;
using Toggl.Core.UI.Extensions;
using Toggl.Core.UI.ViewModels;
using Toggl.Droid.Extensions;
using Toggl.Droid.Extensions.Reactive;
using Toggl.Droid.Presentation;
using Toggl.Shared.Extensions;
using Toggl.Core.UI.Views.Settings;
using static Toggl.Shared.Resources;

namespace Toggl.Droid.Activities
{
    [Activity(Theme = "@style/Theme.Splash",
        ScreenOrientation = ScreenOrientation.Portrait,
        ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize)]
    public partial class SettingsActivity : ReactiveActivity<SettingsViewModel>
    {
        public SettingsActivity() : base(
            Resource.Layout.SettingsActivity,
            Resource.Style.AppTheme,
            Transitions.SlideInFromBottom)
        {
        }

        public SettingsActivity(IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
        {
        }

        protected override void InitializeBindings()
        {
            scrollView.AttachMaterialScrollBehaviour(appBarLayout);

            // profile section
            nameRow.Bind(ViewModel.Name, Name)
                .DisposedBy(DisposeBag);
            emailRow.Bind(ViewModel.Email, Email)
                .DisposedBy(DisposeBag);
            workspaceRow.Bind(ViewModel.WorkspaceName, Workspace, ViewModel.PickDefaultWorkspace)
                .DisposedBy(DisposeBag);

            // date and time section
            dateFormatRow.Bind(ViewModel.DateFormat, DateFormat, ViewModel.SelectDateFormat)
                .DisposedBy(DisposeBag);
            use24HoursFormatRow.Bind(ViewModel.UseTwentyFourHourFormat, Use24HourClock, ViewModel.ToggleTwentyFourHourSettings)
                .DisposedBy(DisposeBag);
            durationFormatRow.Bind(ViewModel.DurationFormat, DurationFormat, ViewModel.SelectDurationFormat)
                .DisposedBy(DisposeBag);
            beginningOfWeekRow.Bind(ViewModel.BeginningOfWeek, FirstDayOfTheWeek, ViewModel.SelectBeginningOfWeek)
                .DisposedBy(DisposeBag);
            isGroupingTimeEntriesRow.Bind(ViewModel.IsGroupingTimeEntries,GroupTimeEntries, ViewModel.ToggleTimeEntriesGrouping)
                .DisposedBy(DisposeBag);

            // timer defaults section 
            isManualModeEnabledRowView.Bind(ViewModel.IsManualModeEnabled,ManualMode, ManualModeDescription, ViewModel.ToggleManualMode)
                .DisposedBy(DisposeBag);
            swipeActionsRow.Bind(ViewModel.SwipeActionsEnabled, SwipeActions, ViewModel.ToggleSwipeActions)
                .DisposedBy(DisposeBag);
            runningTimeEntryRow.Bind(ViewModel.AreRunningTimerNotificationsEnabled, NotificationsRunningTimer, ViewModel.ToggleRunningTimerNotifications)
                .DisposedBy(DisposeBag);
            stoppedTimerRow.Bind(ViewModel.AreStoppedTimerNotificationsEnabled, NotificationsRunningTimer, ViewModel.ToggleStoppedTimerNotifications)
                .DisposedBy(DisposeBag);

            // calendar section 
            calendarSettingsRow.SetRowData(new NavigationRow(CalendarSettingsTitle, ViewModel.OpenCalendarSettings));
            smartRemindersRow.Bind(ViewModel.CalendarSmartReminders, SmartReminders, ViewModel.OpenCalendarSmartReminders)
                .DisposedBy(DisposeBag);
            ViewModel.IsCalendarSmartRemindersVisible
                .Subscribe(smartRemindersRow.ItemView.Rx().IsVisible())
                .DisposedBy(DisposeBag);

            // general section
            submitFeedbackRow.SetRowData(new NavigationRow(SubmitFeedback, ViewModel.SubmitFeedback)); 
            aboutRow.SetRowData(new NavigationRow(About, ViewModel.Version, ViewModel.OpenAboutView)); 
            helpRow.SetRowData(new NavigationRow(Help, ViewModel.OpenHelpView));
            
            ViewModel.LoggingOut
                .Subscribe(this.CancelAllNotifications)
                .DisposedBy(DisposeBag);

            ViewModel.IsFeedbackSuccessViewShowing
                .Subscribe(showFeedbackSuccessToast)
                .DisposedBy(DisposeBag);

            ViewModel.CurrentSyncStatus
                .Select(syncStatus => new CustomRow<PresentableSyncStatus>(syncStatus, ViewModel.TryLogout))
                .Subscribe(logoutRowViewView.SetRowData)
                .DisposedBy(DisposeBag);
        }

        private void showFeedbackSuccessToast(bool succeeded)
        {
            if (!succeeded) return;

            var toast = Toast.MakeText(this, Shared.Resources.SendFeedbackSuccessMessage, ToastLength.Long);
            toast.SetGravity(GravityFlags.CenterHorizontal | GravityFlags.Bottom, 0, 0);
            toast.Show();
        }
    }
}
