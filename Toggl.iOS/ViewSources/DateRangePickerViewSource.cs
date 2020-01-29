using Foundation;
using System;
using System.Collections.Immutable;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Toggl.Core.UI.ViewModels.DateRangePicker;
using Toggl.iOS.Views;
using UIKit;

namespace Toggl.iOS.ViewSources
{
    public sealed class DateRangePickerViewSource : UICollectionViewSource
    {
        private const string cellIdentifier = nameof(DateRangePickerCell);

        private readonly UICollectionView collectionView;
        private readonly ISubject<DateRangePickerMonthInfo> currentMonthSubject = new Subject<DateRangePickerMonthInfo>();
        private readonly ISubject<DateTime> dayTaps = new Subject<DateTime>();

        private ImmutableList<DateRangePickerMonthInfo> months = ImmutableList<DateRangePickerMonthInfo>.Empty;
        private int page;

        public IObservable<DateTime> DayTaps { get; }
        public IObservable<DateRangePickerMonthInfo> CurrentMonthObservable { get; }

        public DateRangePickerViewSource(UICollectionView collectionView)
        {
            this.collectionView = collectionView;

            collectionView.RegisterNibForCell(DateRangePickerCell.Nib, cellIdentifier);

            DayTaps = dayTaps.AsObservable();
            CurrentMonthObservable = currentMonthSubject.AsObservable().DistinctUntilChanged();
        }

        public override UICollectionViewCell GetCell(UICollectionView collectionView, NSIndexPath indexPath)
        {
            var month = months[indexPath.Section];
            var date = month.DisplayDates.Beginning.AddDays(indexPath.Row);
            var data = new DatePickerCellData(
                date,
                month.Today == date,
                month.Selection?.Contains(date) ?? false,
                date.Month == month.Month,
                month.IsDateTheFirstSelectedDate(date),
                month.IsDateTheLastSelectedDate(date),
                month.IsSelectionPartial);

            var cell = collectionView.DequeueReusableCell(cellIdentifier, indexPath) as DateRangePickerCell;

            cell.Item = data;
            return cell;
        }

        public void UpdateMonths(ImmutableList<DateRangePickerMonthInfo> newMonths)
        {
            var shouldScroll = !months.IsEmpty;
            months = newMonths;
            collectionView.ReloadData();
            ScrollToCurrentPage(shouldScroll);
        }

        public void ScrollToCurrentPage(bool animated = false)
        {
            if (months.IsEmpty) 
                return;

            page = months.FindLastIndex(m => m.IsSelectionBeginningBoundary);

            if (page < 0)
                page = months.Count - 1;

            var targetIndexPath = NSIndexPath.FromItemSection(0, page);
            collectionView.ScrollToItem(targetIndexPath, UICollectionViewScrollPosition.Left, animated);
            currentMonthSubject.OnNext(months[page]);
        }

        public override nint NumberOfSections(UICollectionView collectionView)
            => months.Count;

        public override nint GetItemsCount(UICollectionView collectionView, nint section)
            => months[(int)section].DisplayDates.Length;

        public override void ItemSelected(UICollectionView collectionView, NSIndexPath indexPath)
            => dayTaps.OnNext(months[indexPath.Section].DisplayDates.Beginning.AddDays(indexPath.Row));

        public override void DecelerationEnded(UIScrollView scrollView)
        {
            page = (int)((collectionView.ContentOffset.X + collectionView.Frame.Width / 2) / collectionView.Frame.Width);
            currentMonthSubject.OnNext(months[page]);
        }
    }
}
