using Foundation;
using ObjCRuntime;
using System;
using System.Reactive;
using Toggl.iOS.Extensions;
using Toggl.iOS.Extensions.Reactive;
using Toggl.Shared;
using UIKit;

namespace Toggl.iOS
{
    public sealed partial class CalendarSettingsTableViewHeader : UIView
    {
        public IObservable<Unit> LinkCalendarsSwitchTapped { get; private set; }

        public CalendarSettingsTableViewHeader(IntPtr handle) : base(handle)
        {
        }

        public override void AwakeFromNib()
        {
            base.AwakeFromNib();
            TextLabel.Text = Resources.AllowCalendarAccess;
            DescriptionLabel.Text = Resources.CalendarAccessExplanation;
            LinkCalendarsSwitchTapped = LinkCalendarsSwitch.Rx().Changed();

            EnableCalendarAccessView.InsertSeparator();
        }

        public static CalendarSettingsTableViewHeader Create()
        {
            var arr = NSBundle.MainBundle.LoadNib(nameof(CalendarSettingsTableViewHeader), null, null);
            return Runtime.GetNSObject<CalendarSettingsTableViewHeader>(arr.ValueAt(0));
        }

        public void SetCalendarIntegrationStatus(bool enabled)
        {
            if (LinkCalendarsSwitch.On != enabled)
                LinkCalendarsSwitch.On = enabled;
        }
    }
}
