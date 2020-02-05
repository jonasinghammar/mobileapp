using CoreGraphics;
using Foundation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Toggl.Core;
using Toggl.Core.Calendar;
using Toggl.Core.Extensions;
using Toggl.Core.UI.Calendar;
using Toggl.Core.UI.Collections;
using Toggl.Core.UI.Helper;
using Toggl.iOS.Cells.Calendar;
using Toggl.iOS.Views.Calendar;
using Toggl.Shared;
using Toggl.Shared.Extensions;
using UIKit;
using FoundationResources = Toggl.Shared.Resources;

namespace Toggl.iOS.ViewSources
{
    public sealed class CalendarCollectionViewSource : UICollectionViewSource, ICalendarCollectionViewLayoutDataSource
    {
        private static readonly string twelveHoursFormat = Resources.TwelveHoursFormat;
        private static readonly string twentyFourHoursFormat = Resources.TwentyFourHoursFormat;
        private static readonly string editingTwelveHoursFormat = Resources.EditingTwelveHoursFormat;
        private static readonly string editingTwentyFourHoursFormat = Resources.EditingTwentyFourHoursFormat;
        private const string newItemId = "";

        private readonly string itemReuseIdentifier = nameof(CalendarItemView);
        private readonly string hourReuseIdentifier = nameof(HourSupplementaryView);
        private readonly string editingHourReuseIdentifier = nameof(HourSupplementaryView);
        private readonly string currentTimeReuseIdentifier = nameof(CurrentTimeSupplementaryView);

        private readonly ITimeService timeService;
        private readonly ObservableGroupedOrderedCollection<CalendarItem> collection;

        private IList<CalendarItem> calendarItems;
        private IList<CalendarItemLayoutAttributes> layoutAttributes;
        private TimeFormat timeOfDayFormat = TimeFormat.TwelveHoursFormat;
        private DateTime date;
        private string selectedItemId;
        private string runningTimeEntryId;

        private readonly CompositeDisposable disposeBag = new CompositeDisposable();
        private readonly ISubject<CalendarItem> itemTappedSubject = new Subject<CalendarItem>();

        private UICollectionView collectionView;
        private CalendarCollectionViewLayout layout => collectionView.CollectionViewLayout as CalendarCollectionViewLayout;
        private CalendarLayoutCalculator layoutCalculator;
        public NSIndexPath IndexPathForSelectedItem => indexPathFor(selectedItemId);
        public NSIndexPath IndexPathForRunningTimeEntry => indexPathFor(runningTimeEntryId);

        public bool IsEditing { get; private set; }

        public IObservable<CalendarItem> ItemTapped => itemTappedSubject.AsObservable();

        public CalendarCollectionViewSource(
            ITimeService timeService,
            UICollectionView collectionView,
            IObservable<TimeFormat> timeOfDayFormat,
            ObservableGroupedOrderedCollection<CalendarItem> collection)
        {
            Ensure.Argument.IsNotNull(timeService, nameof(timeService));
            Ensure.Argument.IsNotNull(timeOfDayFormat, nameof(timeOfDayFormat));
            Ensure.Argument.IsNotNull(collection, nameof(collection));
            this.timeService = timeService;
            this.collection = collection;
            this.collectionView = collectionView;

            layoutCalculator = new CalendarLayoutCalculator(timeService);
            calendarItems = new List<CalendarItem>();
            layoutAttributes = new List<CalendarItemLayoutAttributes>();

            registerCells();

            timeOfDayFormat
                .Subscribe(timeOfDayFormatChanged)
                .DisposedBy(disposeBag);

            collection
                .CollectionChange
                .ObserveOn(IosDependencyContainer.Instance.SchedulerProvider.MainScheduler)
                .Subscribe(_ => onCollectionChanges())
                .DisposedBy(disposeBag);

            timeService
                .MidnightObservable
                .Subscribe(dateChanged)
                .DisposedBy(disposeBag);

            timeService
                .CurrentDateTimeObservable
                .DistinctUntilChanged(offset => offset.Minute)
                .ObserveOn(IosDependencyContainer.Instance.SchedulerProvider.MainScheduler)
                .Subscribe(_ => updateLayoutAttributesIfNeeded())
                .DisposedBy(disposeBag);

            onCollectionChanges();
        }

        public override UICollectionViewCell GetCell(UICollectionView collectionView, NSIndexPath indexPath)
        {
            var cell = collectionView.DequeueReusableCell(itemReuseIdentifier, indexPath) as CalendarItemView;
            var item = calendarItems[(int) indexPath.Item];
            cell.Layout = layout;
            cell.Item = item;
            cell.IsEditing = IsEditing && selectedItemId == item.Id;
            return cell;
        }

        public override nint GetItemsCount(UICollectionView collectionView, nint section)
            => calendarItems.Count;

        public override void ItemSelected(UICollectionView collectionView, NSIndexPath indexPath)
        {
            var item = calendarItems[indexPath.Row];
            itemTappedSubject.OnNext(item);
        }

        public override UICollectionReusableView GetViewForSupplementaryElement(UICollectionView collectionView, NSString elementKind, NSIndexPath indexPath)
        {
            if (elementKind == CalendarCollectionViewLayout.HourSupplementaryViewKind)
            {
                var reusableView = collectionView.DequeueReusableSupplementaryView(elementKind, hourReuseIdentifier, indexPath) as HourSupplementaryView;
                var hour = date.AddHours((int)indexPath.Item);
                reusableView.SetLabel(hour.ToString(supplementaryHourFormat(), DateFormatCultureInfo.CurrentCulture));
                return reusableView;
            }
            else if (elementKind == CalendarCollectionViewLayout.EditingHourSupplementaryViewKind)
            {
                var reusableView = collectionView.DequeueReusableSupplementaryView(elementKind, editingHourReuseIdentifier, indexPath) as EditingHourSupplementaryView;
                var attrs = LayoutAttributesForItemAtIndexPath(IndexPathForSelectedItem);
                var hour = (int)indexPath.Item == 0 ? attrs.StartTime.ToLocalTime() : attrs.EndTime.ToLocalTime();
                reusableView.SetLabel(hour.ToString(editingHourFormat(), DateFormatCultureInfo.CurrentCulture));
                return reusableView;
            }

            return collectionView.DequeueReusableSupplementaryView(elementKind, currentTimeReuseIdentifier, indexPath);
        }

        public IEnumerable<NSIndexPath> IndexPathsOfCalendarItemsBetweenHours(int minHour, int maxHour)
        {
            if (calendarItems.None())
            {
                return Enumerable.Empty<NSIndexPath>();
            }

            var now = timeService.CurrentDateTime;
            var indices = calendarItems
                .Select((value, index) => new { value, index })
                .Where(t => t.value.StartTime.ToLocalTime().Hour >= minHour || t.value.EndTime(now).ToLocalTime().Hour <= maxHour)
                .Select(t => t.index);

            return indices.Select(index => NSIndexPath.FromItemSection(index, 0));
        }

        public CalendarItemLayoutAttributes LayoutAttributesForItemAtIndexPath(NSIndexPath indexPath)
            => layoutAttributes[(int)indexPath.Item];

        public CalendarItem? CalendarItemAtPoint(CGPoint point)
        {
            var indexPath = collectionView.IndexPathForItemAtPoint(point);
            if (indexPath != null && indexPath.Item < calendarItems.Count)
            {
                return itemAt(indexPath);
            }
            return null;
        }

        public List<DateTimeOffset> AllItemsStartAndEndTime()
        {
            var startTimes = calendarItems.Select(item => item.StartTime).Distinct();
            var endTimes = calendarItems.Where(item => item.EndTime.HasValue).Select(item => (DateTimeOffset)item.EndTime).Distinct();
            return startTimes.Concat(endTimes).ToList();
        }

        public CGRect? FrameOfEditingItem()
        {
            if (!IsEditing)
                return null;

            return layout.LayoutAttributesForItem(IndexPathForSelectedItem).Frame;
        }

        public List<CalendarItemLayoutAttributes> GapsBetweenTimeEntriesOf2HoursOrLess()
        {
            var timeEntries = calendarItems
                .Where(item => item.CalendarId == newItemId)
                .OrderBy(te => te.StartTime)
                .ToList();

            return layoutCalculator.CalculateTwoHoursOrLessGapsLayoutAttributes(timeEntries);
        }

        public void StartEditing()
        {
            IsEditing = true;
            layout.IsEditing = true;
        }

        public void StartEditing(CalendarItem calendarItem)
        {
            StartEditing();
            selectedItemId = calendarItem.Id;
            reloadItem(calendarItem);
        }

        public void StopEditing()
        {
            IsEditing = false;
            layout.IsEditing = false;
            selectedItemId = null;
            onCollectionChanges();
        }

        public void InsertItemView(DateTimeOffset startTime, TimeSpan duration)
        {
            if (!IsEditing)
                throw new InvalidOperationException("Set IsEditing before calling insert/update/remove");

            var item = new CalendarItem(newItemId, CalendarItemSource.TimeEntry, startTime, duration, FoundationResources.NewTimeEntry, CalendarIconKind.None);
            selectedItemId = newItemId;

            itemTappedSubject.OnNext(item);
            calendarItems.Add(item);

            layoutAttributes = calculateLayoutAttributes();
            collectionView.ReloadData();
        }

        public void UpdateItemView(DateTimeOffset startTime, TimeSpan? duration)
        {
            if (!IsEditing)
                throw new InvalidOperationException("Set IsEditing before calling insert/update/remove");

            var position = indexFor(selectedItemId);
            calendarItems[position] = itemAt(position)
                .WithStartTime(startTime)
                .WithDuration(duration);

            layoutAttributes = calculateLayoutAttributes();

            updateEditingHours();
            layout.InvalidateLayoutForVisibleItems();
        }

        public void UpdateItemView(CalendarItem calendarItem)
            => UpdateItemView(calendarItem.StartTime, calendarItem.Duration);

        public void RemoveItemView(CalendarItem calendarItem)
        {
            if (!IsEditing)
                throw new InvalidOperationException("Set IsEditing before calling insert/update/remove");

            var indexToRemove = indexFor(calendarItem);
            if (indexToRemove == -1)
                return;

            var indexPathToRemove = NSIndexPath.FromItemSection(indexToRemove, 0);
            calendarItems.RemoveAt(indexToRemove);
            layoutAttributes = calculateLayoutAttributes();
            collectionView.ReloadData();
        }

        public override void Scrolled(UIScrollView scrollView)
        {
            if (!IsEditing)
                return;

            layoutAttributes = calculateLayoutAttributes();
            layout.InvalidateLayoutForVisibleItems();
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (!disposing) return;

            disposeBag.Dispose();
        }

        private void timeOfDayFormatChanged(TimeFormat timeFormat)
        {
            timeOfDayFormat = timeFormat;
            collectionView.ReloadData();
        }

        private void onCollectionChanges()
        {
            calendarItems = collection.IsEmpty ? new List<CalendarItem>() : collection[0].ToList();
            runningTimeEntryId = calendarItems.FirstOrDefault(item => item.Duration == null).Id;

            layoutAttributes = calculateLayoutAttributes();
            collectionView.ReloadData();
        }

        private void dateChanged(DateTimeOffset dateTimeOffset)
        {
            date = dateTimeOffset.ToLocalTime().Date;
        }

        private void updateLayoutAttributesIfNeeded()
        {
            if (selectedItemId == null)
                return;

            layoutAttributes = calculateLayoutAttributes();
            layout.InvalidateLayout();
        }

        private void registerCells()
        {
            collectionView.RegisterNibForCell(CalendarItemView.Nib, itemReuseIdentifier);

            collectionView.RegisterNibForSupplementaryView(HourSupplementaryView.Nib,
                                                           CalendarCollectionViewLayout.HourSupplementaryViewKind,
                                                           new NSString(hourReuseIdentifier));

            collectionView.RegisterNibForSupplementaryView(EditingHourSupplementaryView.Nib,
                                                           CalendarCollectionViewLayout.EditingHourSupplementaryViewKind,
                                                           new NSString(editingHourReuseIdentifier));

            collectionView.RegisterNibForSupplementaryView(CurrentTimeSupplementaryView.Nib,
                                                           CalendarCollectionViewLayout.CurrentTimeSupplementaryViewKind,
                                                           new NSString(currentTimeReuseIdentifier));
        }

        private IList<CalendarItemLayoutAttributes> calculateLayoutAttributes()
            => layoutCalculator.CalculateLayoutAttributes(calendarItems);

        private void updateEditingHours()
        {
            if (!IsEditing)
                return;

            var startEditingHour = collectionView
                .GetSupplementaryView(CalendarCollectionViewLayout.EditingHourSupplementaryViewKind, NSIndexPath.FromItemSection(0, 0)) as EditingHourSupplementaryView;
            var endEditingHour = collectionView
                .GetSupplementaryView(CalendarCollectionViewLayout.EditingHourSupplementaryViewKind, NSIndexPath.FromItemSection(1, 0)) as EditingHourSupplementaryView;

            var attrs = LayoutAttributesForItemAtIndexPath(IndexPathForSelectedItem);
            if (startEditingHour != null)
            {
                var hour = attrs.StartTime.ToLocalTime();
                startEditingHour.SetLabel(hour.ToString(editingHourFormat(), DateFormatCultureInfo.CurrentCulture));
            }
            if (endEditingHour != null)
            {
                var hour = attrs.EndTime.ToLocalTime();
                endEditingHour.SetLabel(hour.ToString(editingHourFormat(), DateFormatCultureInfo.CurrentCulture));
            }
        }

        private string supplementaryHourFormat()
            => timeOfDayFormat.IsTwentyFourHoursFormat ? twentyFourHoursFormat : twelveHoursFormat;

        private string editingHourFormat()
            => timeOfDayFormat.IsTwentyFourHoursFormat ? editingTwentyFourHoursFormat : editingTwelveHoursFormat;

        private void reloadItem(string id)
        {
            var indexPath = indexPathFor(id);
            if (indexPath != null)
            {
                collectionView.ReloadItems(new[] {indexPath});
            }
        }

        private void reloadItem(CalendarItem item)
            => reloadItem(item.Id);

        private CalendarItem itemAt(int index)
            => calendarItems[index];

        private CalendarItem itemAt(NSIndexPath indexPath)
            => calendarItems[(int)indexPath.Item];

        private int indexFor(string id)
            => calendarItems.IndexOf(i => id == i.Id);

        private int indexFor(CalendarItem item)
            => indexFor(item.Id);

        private NSIndexPath indexPathFor(string id)
        {
            var index = indexFor(id);
            return index >= 0 ? NSIndexPath.FromItemSection(index, 0) : null;
        }
    }
}
