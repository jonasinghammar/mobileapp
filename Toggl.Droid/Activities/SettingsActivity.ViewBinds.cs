using Android.Widget;
using AndroidX.Core.Widget;
using AndroidX.RecyclerView.Widget;
using Google.Android.Material.AppBar;
using Toggl.Droid.Extensions;
using Toggl.Droid.ViewHolders.Settings;
using Toggl.Shared.Extensions;
using Toolbar = AndroidX.AppCompat.Widget.Toolbar;

namespace Toggl.Droid.Activities
{
    public partial class SettingsActivity
    {
        private AppBarLayout appBarLayout;
        private NestedScrollView scrollView;
        private Toolbar toolbar;

        private LinearLayout settingsContainer;
        private InfoRowViewView nameRow;
        private InfoRowViewView emailRow;
        private NavigationRowViewView workspaceRow;
        private NavigationRowViewView dateFormatRow;
        private ToggleRowViewView use24HoursFormatRow;
        private NavigationRowViewView durationFormatRow;
        private NavigationRowViewView beginningOfWeekRow;
        private ToggleRowViewView isGroupingTimeEntriesRow;
        private ToggleRowViewView swipeActionsRow;
        private ToggleRowViewView runningTimeEntryRow;
        private ToggleRowViewView stoppedTimerRow;
        private ToggleRowViewWithDescriptionView isManualModeEnabledRowView;
        private NavigationRowViewView calendarSettingsRow;
        private NavigationRowViewView smartRemindersRow;
        private NavigationRowViewView submitFeedbackRow;
        private NavigationRowViewView aboutRow;
        private NavigationRowViewView helpRow;
        private LogoutRowViewView logoutRowViewView;

        private HeaderRowView profileHeaderRow;
        private HeaderRowView dateTimeHeaderRow;
        private HeaderRowView timerDefaultsHeaderRow;
        private HeaderRowView calendarHeaderRow;
        private HeaderRowView generalHeaderRow;


        protected override void InitializeViews()
        {
            appBarLayout = FindViewById<AppBarLayout>(Resource.Id.AppBarLayout);
            scrollView = FindViewById<NestedScrollView>(Resource.Id.ScrollView);
            settingsContainer = FindViewById<LinearLayout>(Resource.Id.settingsContainer);

            profileHeaderRow = HeaderRowView.Create(this);
            profileHeaderRow.SetRowData(new HeaderRow(Shared.Resources.YourProfile));
            
            dateTimeHeaderRow = HeaderRowView.Create(this);
            dateTimeHeaderRow.SetRowData(new HeaderRow(Shared.Resources.DateAndTime));
            
            timerDefaultsHeaderRow = HeaderRowView.Create(this);
            timerDefaultsHeaderRow.SetRowData(new HeaderRow(Shared.Resources.TimerDefaults));
            
            calendarHeaderRow = HeaderRowView.Create(this);
            calendarHeaderRow.SetRowData(new HeaderRow(Shared.Resources.Calendar));
            
            generalHeaderRow = HeaderRowView.Create(this);
            generalHeaderRow.SetRowData(new HeaderRow(Shared.Resources.General));
            
            nameRow = InfoRowViewView.Create(this);
            emailRow = InfoRowViewView.Create(this);
            workspaceRow = NavigationRowViewView.Create(this);
            dateFormatRow = NavigationRowViewView.Create(this);
            use24HoursFormatRow = ToggleRowViewView.Create(this);
            durationFormatRow = NavigationRowViewView.Create(this);
            beginningOfWeekRow = NavigationRowViewView.Create(this);
            isGroupingTimeEntriesRow = ToggleRowViewView.Create(this);
            swipeActionsRow = ToggleRowViewView.Create(this);
            isManualModeEnabledRowView = ToggleRowViewWithDescriptionView.Create(this);
            runningTimeEntryRow = ToggleRowViewView.Create(this);
            stoppedTimerRow = ToggleRowViewView.Create(this);
            calendarSettingsRow = NavigationRowViewView.Create(this, viewLayout: Resource.Layout.SettingsButtonRowView);
            smartRemindersRow = NavigationRowViewView.Create(this);
            submitFeedbackRow = NavigationRowViewView.Create(this, viewLayout: Resource.Layout.SettingsButtonRowView);
            aboutRow = NavigationRowViewView.Create(this, viewLayout: Resource.Layout.SettingsButtonRowWithDetailsView);
            helpRow = NavigationRowViewView.Create(this, viewLayout: Resource.Layout.SettingsButtonRowView);
            logoutRowViewView = LogoutRowViewView.Create(this);

            addSettingsRowViewsToSettingsContainer(
                profileHeaderRow,
                nameRow,
                emailRow,
                workspaceRow,
                DividerRowView.Create(this),
                dateTimeHeaderRow,
                dateFormatRow,
                use24HoursFormatRow,
                durationFormatRow,
                beginningOfWeekRow,
                DividerRowView.Create(this),
                timerDefaultsHeaderRow,
                isGroupingTimeEntriesRow,
                swipeActionsRow,
                runningTimeEntryRow,
                stoppedTimerRow,
                isManualModeEnabledRowView,
                DividerRowView.Create(this),
                calendarHeaderRow,
                calendarSettingsRow,
                smartRemindersRow,
                DividerRowView.Create(this),
                generalHeaderRow,
                submitFeedbackRow,
                aboutRow,
                helpRow,
                logoutRowViewView
            );

            toolbar = FindViewById<Toolbar>(Resource.Id.Toolbar);
            SetSupportActionBar(toolbar);
            SupportActionBar.Title = Shared.Resources.Settings;
            SupportActionBar.SetDisplayHomeAsUpEnabled(true);
            SupportActionBar.SetDisplayShowHomeEnabled(true);

            var baseMargin = 24.DpToPixels(this);
            logoutRowViewView.ItemView.DoOnApplyWindowInsets((v, insets, initialPadding) =>
            {
                var bottomMargin = baseMargin + insets.SystemWindowInsetBottom;
                var currentLayoutParams = logoutRowViewView.ItemView.LayoutParameters as LinearLayout.LayoutParams;
                logoutRowViewView.ItemView.LayoutParameters = currentLayoutParams.WithMargins(bottom: bottomMargin);
            });
        }

        private void addSettingsRowViewsToSettingsContainer(params RecyclerView.ViewHolder[] rowViews)
        {
            rowViews.ForEach(rowView => settingsContainer.AddView(rowView.ItemView));
        }
    }
}