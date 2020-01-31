using System;
using Android.App;
using Android.Content.PM;
using Android.Runtime;
using Android.Views;
using Toggl.Droid.Presentation;

namespace Toggl.Droid.Activities
{
    [Activity(Theme = "@style/Theme.Splash",
        WindowSoftInputMode = SoftInput.AdjustResize,
        ScreenOrientation = ScreenOrientation.Portrait,
        ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize)]
    public class IndependentCalendarSettingsActivity : CalendarSettingsActivity
    {
        public IndependentCalendarSettingsActivity()
            : base(Transitions.SlideInFromBottom)
        {
        }

        public IndependentCalendarSettingsActivity(IntPtr javaReference, JniHandleOwnership transfer)
            : base(javaReference, transfer)
        {
        }
    }
}
