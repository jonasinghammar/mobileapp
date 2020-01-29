using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Reactive.Linq;
using CoreGraphics;
using Foundation;
using Toggl.Core.UI.ViewModels.DateRangePicker;
using Toggl.iOS.Extensions;
using Toggl.iOS.Extensions.Reactive;
using Toggl.iOS.ViewSources;
using Toggl.Shared;
using Toggl.Shared.Extensions;
using UIKit;
using static Toggl.Core.UI.ViewModels.DateRangePicker.DateRangePickerViewModel;

namespace Toggl.iOS.ViewControllers
{
    public partial class DateRangePickerViewController : ReactiveViewController<DateRangePickerViewModel>
    {
        private CGSize popoverPreferedSize = new CGSize(319, 397);

        private DateRangePickerViewSource calendarCollectionViewSource;
        private DateRangePickerCollectionViewLayout calendarCollectionViewLayout;

        private bool firstLayoutNotDone = true;

        public DateRangePickerViewController(DateRangePickerViewModel viewModel)
            : base(viewModel, nameof(DateRangePickerViewController))
        {
        }

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();

            PreferredContentSize = popoverPreferedSize;

            AcceptButton.SetTitle(Resources.Done, UIControlState.Normal);
            CloseButton.SetTitle(Resources.Cancel, UIControlState.Normal);

            setupDayHeaders(ViewModel.WeekDaysLabels);

            calendarCollectionViewSource = new DateRangePickerViewSource(CalendarCollectionView);
            calendarCollectionViewLayout = new DateRangePickerCollectionViewLayout();
            CalendarCollectionView.Delegate = calendarCollectionViewSource;
            CalendarCollectionView.DataSource = calendarCollectionViewSource;
            CalendarCollectionView.CollectionViewLayout = calendarCollectionViewLayout;

            var quickSelectCollectionViewSource = new DateRangePickerShortcutsSource(QuickSelectCollectionView);
            QuickSelectCollectionView.Source = quickSelectCollectionViewSource;

            ViewModel.Months
                .Subscribe(calendarCollectionViewSource.UpdateMonths)
                .DisposedBy(DisposeBag);

            ViewModel.Shortcuts
                .Subscribe(quickSelectCollectionViewSource.UpdateShortcuts)
                .DisposedBy(DisposeBag);

            calendarCollectionViewSource.CurrentMonthObservable
                .Select(monthTitleString)
                .Subscribe(CurrentMonthLabel.Rx().AttributedText())
                .DisposedBy(DisposeBag);

            calendarCollectionViewSource.DayTaps
                .Subscribe(ViewModel.SelectDate.Inputs)
                .DisposedBy(DisposeBag);

            quickSelectCollectionViewSource.ShortcutTaps
                .Do(updateSiriShortcut)
                .Select(s => s.DateRangePeriod)
                .Subscribe(ViewModel.SetDateRangePeriod.Inputs)
                .DisposedBy(DisposeBag);

            AcceptButton.Rx().Tap()
                .Subscribe(ViewModel.Accept.Inputs)
                .DisposedBy(DisposeBag);

            CloseButton.Rx().Tap()
                .Subscribe(ViewModel.Cancel.Inputs)
                .DisposedBy(DisposeBag);
        }

        public override void TraitCollectionDidChange(UITraitCollection previousTraitCollection)
        {
            base.TraitCollectionDidChange(previousTraitCollection);
            calendarCollectionViewLayout.InvalidateLayout();
            calendarCollectionViewSource.ScrollToCurrentPage();
        }

        public override void ViewDidLayoutSubviews()
        {
            if (firstLayoutNotDone)
            {
                calendarCollectionViewLayout.InvalidateLayout();
                View.SetNeedsLayout();
                calendarCollectionViewSource.ScrollToCurrentPage();

                firstLayoutNotDone = false;
            }
            base.ViewDidLayoutSubviews();
        }

        private NSAttributedString monthTitleString(DateRangePickerMonthInfo month)
        {
            var rangeStart = month.MonthDisplay.Length - month.Year.ToString().Length;
            var rangeLength = month.Year.ToString().Length;
            var range = new NSRange(rangeStart, rangeLength);

            var attributedString = new NSMutableAttributedString(
                month.MonthDisplay,
                new UIStringAttributes { ForegroundColor = ColorAssets.Text });
            attributedString.AddAttributes(
                new UIStringAttributes { ForegroundColor = ColorAssets.Text2 },
                range);

            return attributedString;
        }

        private void setupDayHeaders(ImmutableList<string> dayHeaders)
        {
            DayHeader0.Text = dayHeaders[0];
            DayHeader1.Text = dayHeaders[1];
            DayHeader2.Text = dayHeaders[2];
            DayHeader3.Text = dayHeaders[3];
            DayHeader4.Text = dayHeaders[4];
            DayHeader5.Text = dayHeaders[5];
            DayHeader6.Text = dayHeaders[6];
        }

        private void updateSiriShortcut(Shortcut shortcut)
            => IosDependencyContainer.Instance.IntentDonationService.DonateShowReport(shortcut.DateRangePeriod);
    }
}

