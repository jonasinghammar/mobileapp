using System;
using System.Collections.Generic;
using Android.Views;
using Toggl.Droid.Extensions;
using Google.Android.Material.BottomNavigation;
using Toggl.Droid.Fragments;

namespace Toggl.Droid.Activities
{
    public sealed partial class MainTabBarActivity
    {
        private BottomNavigationView navigationView;
        private Dictionary<Type, bool> readyLayouts = new Dictionary<Type, bool>();
        private Dictionary<Type, int> placeholderLayoutIds = new Dictionary<Type, int>
        {
            { typeof(MainFragment), Resource.Id.MainLogPlaceholder }
        };

        protected override void InitializeViews()
        {
            navigationView = FindViewById<BottomNavigationView>(Resource.Id.MainTabBarBottomNavigationView);
            navigationView.FitBottomPaddingInset();

            var menu = navigationView.Menu;
            var timerTab = menu.FindItem(Resource.Id.MainTabTimerItem);
            timerTab.SetTitle(Shared.Resources.Timer);

            var reportsTab = menu.FindItem(Resource.Id.MainTabReportsItem);
            reportsTab.SetTitle(Shared.Resources.Reports);

            var calendarTab = menu.FindItem(Resource.Id.MainTabCalendarItem);
            calendarTab.SetTitle(Shared.Resources.Calendar);
        }
    }
}
