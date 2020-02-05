using Foundation;
using System;
using Toggl.Core.UI.ViewModels.MainLog;
using Toggl.iOS.Cells;
using Toggl.iOS.Extensions;
using Toggl.Shared;
using UIKit;

namespace Toggl.iOS.Views
{
    public partial class TimeEntriesLogHeaderView : BaseTableHeaderFooterView<MainLogSectionViewModel>
    {
        public static readonly string Identifier = "timeEntryLogHeaderCell";

        public static readonly NSString Key = new NSString(nameof(TimeEntriesLogHeaderView));
        public static readonly UINib Nib;

        static TimeEntriesLogHeaderView()
        {
            Nib = UINib.FromName(nameof(TimeEntriesLogHeaderView), NSBundle.MainBundle);
        }

        protected TimeEntriesLogHeaderView(IntPtr handle)
            : base(handle)
        {
        }

        public override void AwakeFromNib()
        {
            base.AwakeFromNib();

            ContentView.InsertSeparator();

            ContentView.BackgroundColor = ColorAssets.TableBackground;
            DateLabel.TextColor = ColorAssets.Text2;
            DurationLabel.TextColor = ColorAssets.Text2;

            IsAccessibilityElement = true;
            AccessibilityTraits = UIAccessibilityTrait.Header;
            DurationLabel.Font = DurationLabel.Font.GetMonospacedDigitFont();
        }

        protected override void UpdateView()
        {
            if (Item is DaySummaryViewModel daySummary)
            {
                DateLabel.Text = daySummary.Title;
                DurationLabel.Text = daySummary.TotalTrackedTime;
                updateAccessibilityProperties();
            }
        }

        private void updateAccessibilityProperties()
        {
            if (Item is DaySummaryViewModel daySummary)
            {
                AccessibilityLabel = $"{daySummary.Title}, {Resources.TrackedTime}: {daySummary.TotalTrackedTime}";
            }
        }
    }
}
