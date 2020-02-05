using CoreGraphics;
using Foundation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Toggl.Core;
using Toggl.Core.UI.Calendar;
using Toggl.Shared;
using Toggl.Shared.Extensions;
using UIKit;
using Constants = Toggl.Core.Helper.Constants;
using Math = System.Math;

namespace Toggl.iOS.Views.Calendar
{
    public sealed class CalendarCollectionViewLayout : UICollectionViewLayout
    {
        private const int hoursPerDay = Constants.HoursPerDay;
        private float minHourHeight = 28;
        private float maxHourHeight = 28 * 4;
        private const float maxWidth = 834;

        public float HourHeight { get; private set; } = 56;

        private ISubject<Unit> scalingEndedSubject = new Subject<Unit>();
        public IObservable<Unit> ScalingEnded => scalingEndedSubject.AsObservable();

        private static readonly nfloat leftPadding = 76;
        private static readonly nfloat hourSupplementaryLabelHeight = 20;
        private static readonly nfloat currentTimeSupplementaryLeftOffset = -18;
        private static readonly nfloat verticalItemSpacing = 1;

        private nfloat sideMargin
            => CollectionView.Frame.Width >= maxWidth
                ? (CollectionView.Frame.Width - maxWidth) / 2
                : 0;
        private nfloat rightPadding
            => CollectionView.TraitCollection.HorizontalSizeClass == UIUserInterfaceSizeClass.Regular
                ? 20
                : 16;
        private nfloat horizontalItemSpacing
            => CollectionView.TraitCollection.HorizontalSizeClass == UIUserInterfaceSizeClass.Regular
                ? 11
                : 4;

        private DateTime date;
        private readonly ITimeService timeService;
        private readonly ICalendarCollectionViewLayoutDataSource dataSource;

        private readonly CompositeDisposable disposeBag = new CompositeDisposable();

        public static NSString HourSupplementaryViewKind = new NSString("Hour");
        public static NSString EditingHourSupplementaryViewKind = new NSString("EditingHour");
        public static NSString CurrentTimeSupplementaryViewKind = new NSString("CurrentTime");
        public nfloat ContentViewHeight => hoursPerDay * HourHeight;

        private UICollectionViewLayoutAttributes currentTimeLayoutAttributes;

        private Dictionary<NSIndexPath, UICollectionViewLayoutAttributes> itemLayoutAttributes = new Dictionary<NSIndexPath, UICollectionViewLayoutAttributes>();
        private Dictionary<NSString, Dictionary<NSIndexPath, UICollectionViewLayoutAttributes>> supplementaryViewLayoutAttributes = new Dictionary<NSString, Dictionary<NSIndexPath, UICollectionViewLayoutAttributes>>();

        private bool isToday => date.ToLocalTime().Date == timeService.CurrentDateTime.ToLocalTime().Date;

        private bool isEditing;
        public bool IsEditing
        {
            get => isEditing;
            set
            {
                isEditing = value;
                InvalidateLayoutForVisibleItems();
            }
        }

        public CalendarCollectionViewLayout(
            DateTime date,
            ITimeService timeService,
            ICalendarCollectionViewLayoutDataSource dataSource)
            : base()
        {
            Ensure.Argument.IsNotNull(timeService, nameof(timeService));
            Ensure.Argument.IsNotNull(dataSource, nameof(dataSource));

            this.date = date;
            this.dataSource = dataSource;
            this.timeService = timeService;

            timeService
                .MidnightObservable
                .Subscribe(dateChanged)
                .DisposedBy(disposeBag);

            if (isToday)
                timeService
                    .CurrentDateTimeObservable
                    .DistinctUntilChanged(offset => offset.Minute)
                    .ObserveOn(IosDependencyContainer.Instance.SchedulerProvider.MainScheduler)
                    .Subscribe(_ => InvalidateCurrentTimeLayout())
                    .DisposedBy(disposeBag);

            currentTimeLayoutAttributes = UICollectionViewLayoutAttributes.CreateForSupplementaryView(CurrentTimeSupplementaryViewKind, NSIndexPath.FromItemSection(0, 0));
        }

        public override void PrepareLayout()
        {
            base.PrepareLayout();

            minHourHeight = (float)CollectionView.Bounds.Height / 26;
            maxHourHeight = minHourHeight * 5;
        }

        public override void InvalidateLayout()
        {
            itemLayoutAttributes = new Dictionary<NSIndexPath, UICollectionViewLayoutAttributes>();
            supplementaryViewLayoutAttributes = new Dictionary<NSString, Dictionary<NSIndexPath, UICollectionViewLayoutAttributes>>();
            base.InvalidateLayout();
        }

        public override void InvalidateLayout(UICollectionViewLayoutInvalidationContext context)
        {
            if (context.InvalidatedItemIndexPaths != null)
                context.InvalidatedItemIndexPaths.ForEach(indexPath => itemLayoutAttributes.Remove(indexPath));

            if (context.InvalidatedSupplementaryIndexPaths != null)
                context.InvalidatedSupplementaryIndexPaths.ForEach(pair =>
                {
                    if (!supplementaryViewLayoutAttributes.ContainsKey((NSString)pair.Key))
                        return;

                    var indexPaths = (NSArray) pair.Value;
                    for(nuint i = 0; i < indexPaths.Count; i++)
                    {
                        var indexPath = indexPaths.GetItem<NSIndexPath>(i);
                        supplementaryViewLayoutAttributes[(NSString)pair.Key].Remove(indexPath);
                    }
                });

            base.InvalidateLayout(context);
        }

        public override CGSize CollectionViewContentSize
        {
            get
            {
                var width = CollectionView.Bounds.Width - (CollectionView.ContentInset.Left + CollectionView.ContentInset.Right);
                var height = ContentViewHeight + hourSupplementaryLabelHeight;
                return new CGSize(width, height);
            }
        }

        public void ScaleHourHeight(nfloat scale, CGPoint basePoint)
        {
            Double newHourHeight = HourHeight * scale;

            if (newHourHeight >= minHourHeight && newHourHeight <= maxHourHeight)
            {
                var offset = basePoint.Y - basePoint.Y * scale;
                CollectionView.ContentOffset = new CGPoint(CollectionView.ContentOffset.X, CollectionView.ContentOffset.Y - offset);
            }

            HourHeight = (float)Math.Max(minHourHeight, Math.Min(maxHourHeight, newHourHeight));

            InvalidateLayout();
            InvalidateLayoutForVisibleItems();
            if (isToday)
                InvalidateCurrentTimeLayout();
        }

        public DateTimeOffset DateAtPoint(CGPoint point)
        {
            var seconds = (point.Y / HourHeight) * 60 * 60;
            var timespan = TimeSpan.FromSeconds(seconds);
            var nextDay = date.AddDays(1);

            var offset = date + timespan;

            if (offset < date)
                return date;
            if (offset > nextDay)
                return nextDay;

            return date + timespan;
        }

        public CGPoint PointAtDate(DateTimeOffset time)
            => new CGPoint(0, time.Hour * HourHeight + time.Minute / HourHeight);

        public void InvalidateLayoutForVisibleItems()
        {
            var context = new UICollectionViewLayoutInvalidationContext();
            context.InvalidateItems(CollectionView.IndexPathsForVisibleItems);
            context.InvalidateSupplementaryElements(EditingHourSupplementaryViewKind, indexPathsForEditingHours().ToArray());
            InvalidateLayout(context);
        }

        public void InvalidateCurrentTimeLayout()
        {
            var context = new UICollectionViewLayoutInvalidationContext();
            context.InvalidateSupplementaryElements(CurrentTimeSupplementaryViewKind, new NSIndexPath[] { NSIndexPath.FromItemSection(0, 0) });
            InvalidateLayout(context);
        }

        // We should invalidate the whole layout only when the collectionview's width changes
        public override bool ShouldInvalidateLayoutForBoundsChange(CGRect newBounds)
            => CollectionView.Bounds.Width != newBounds.Width;

        public override UICollectionViewLayoutAttributes[] LayoutAttributesForElementsInRect(CGRect rect)
        {
            var eventsIndexPaths = indexPathsForVisibleItemsInRect(rect);
            var itemsAttributes = eventsIndexPaths.Select(LayoutAttributesForItem);

            var hoursIndexPaths = indexPathsForHoursInRect(rect);
            var hoursAttributes = hoursIndexPaths.Select(layoutAttributesForHourView);

            var editingHoursIndexPaths = indexPathsForEditingHours();
            var editingHoursAttributes = editingHoursIndexPaths.Select(layoutAttributesForHourView);

            currentTimeLayoutAttributes.Frame = FrameForCurrentTime();

            var attributes = itemsAttributes
                .Concat(hoursAttributes)
                .Concat(editingHoursAttributes);

            if (isToday)
                attributes = attributes.Append(currentTimeLayoutAttributes);

            return attributes.ToArray();
        }

        public override UICollectionViewLayoutAttributes LayoutAttributesForItem(NSIndexPath indexPath)
        {
            if (itemLayoutAttributes.ContainsKey(indexPath))
                return itemLayoutAttributes[indexPath];

            var calendarItemLayoutAttributes = dataSource.LayoutAttributesForItemAtIndexPath(indexPath);

            var attributes = UICollectionViewLayoutAttributes.CreateForCell(indexPath);
            attributes.Frame = frameForItemWithLayoutAttributes(calendarItemLayoutAttributes);
            attributes.ZIndex = zIndexForItemAtIndexPath(indexPath);

            itemLayoutAttributes[indexPath] = attributes;

            return attributes;
        }

        public override UICollectionViewLayoutAttributes LayoutAttributesForSupplementaryView(NSString kind, NSIndexPath indexPath)
        {
            if (supplementaryViewLayoutAttributes.ContainsKey(kind)
                && supplementaryViewLayoutAttributes[kind].ContainsKey(indexPath))
                return supplementaryViewLayoutAttributes[kind][indexPath];

            if (kind == HourSupplementaryViewKind)
            {
                var attributes = UICollectionViewLayoutAttributes.CreateForSupplementaryView(kind, indexPath);
                attributes.Frame = frameForHour((int)indexPath.Item);
                attributes.ZIndex = 0;
                cacheSupplementaryItemAttributes(kind, indexPath, attributes);
                return attributes;
            }
            else if (kind == EditingHourSupplementaryViewKind)
            {
                var attributes = UICollectionViewLayoutAttributes.CreateForSupplementaryView(kind, indexPath);
                attributes.Frame = frameForEditingHour(indexPath);
                attributes.ZIndex = 200;
                cacheSupplementaryItemAttributes(kind, indexPath, attributes);
                return attributes;
            }
            else
            {
                if (isToday)
                {
                    currentTimeLayoutAttributes.Frame = FrameForCurrentTime();
                    currentTimeLayoutAttributes.ZIndex = 600;
                }
                else
                {
                    currentTimeLayoutAttributes.Hidden = true;
                }
                return currentTimeLayoutAttributes;
            }
        }

        public void OnScalingEnded()
            => scalingEndedSubject.OnNext(Unit.Default);

        internal CGRect FrameForCurrentTime()
        {
            var now = timeService.CurrentDateTime.LocalDateTime;

            var yHour = HourHeight * now.Hour;
            var yMins = HourHeight * now.Minute / 60;

            var width = CollectionViewContentSize.Width - leftPadding - rightPadding - currentTimeSupplementaryLeftOffset - (sideMargin * 2);
            var height = 8;
            var x = sideMargin + leftPadding + currentTimeSupplementaryLeftOffset;
            var y = yHour + yMins - height / 2;

            return new CGRect(x, y, width, height);
        }

        private nfloat minItemHeight()
            => HourHeight / 4;

        private nint zIndexForItemAtIndexPath(NSIndexPath indexPath)
        {
            var editingIndexIndexPath = dataSource.IndexPathForSelectedItem;
            var isEditing = editingIndexIndexPath != null && editingIndexIndexPath.Item == indexPath.Item;
            return isEditing ? 150 : 100;
        }

        private UICollectionViewLayoutAttributes layoutAttributesForHourView(NSIndexPath indexPath)
            => LayoutAttributesForSupplementaryView(HourSupplementaryViewKind, indexPath);

        private IEnumerable<NSIndexPath> indexPathsForVisibleItemsInRect(CGRect rect)
        {
            var minHour = (int)Math.Floor(rect.GetMinY() / HourHeight).Clamp(0, hoursPerDay);
            var maxHour = (int)Math.Floor(rect.GetMaxY() / HourHeight).Clamp(0, hoursPerDay);

            return dataSource.IndexPathsOfCalendarItemsBetweenHours(minHour, maxHour);
        }

        private IEnumerable<NSIndexPath> indexPathsForHoursInRect(CGRect rect)
        {
            var minHour = (int)Math.Floor(rect.GetMinY() / HourHeight).Clamp(0, hoursPerDay);
            var maxHour = (int)Math.Floor(rect.GetMaxY() / HourHeight).Clamp(0, hoursPerDay + 1);

            return Enumerable
                .Range(minHour, maxHour - minHour)
                .Select(hour => NSIndexPath.FromItemSection(hour, 0))
                .ToArray();
        }

        private IEnumerable<NSIndexPath> indexPathsForEditingHours()
        {
            if (IsEditing)
            {
                var editingItemIndexPath = dataSource.IndexPathForSelectedItem;
                var runningTimeEntryIndexPath = dataSource.IndexPathForRunningTimeEntry;
                var isEditingRunningTimeEntry = editingItemIndexPath != null && runningTimeEntryIndexPath != null
                                                && runningTimeEntryIndexPath.Item == editingItemIndexPath.Item;
                return isEditingRunningTimeEntry
                    ? new NSIndexPath[] { NSIndexPath.FromItemSection(0, 0) }
                    : new NSIndexPath[] { NSIndexPath.FromItemSection(0, 0), NSIndexPath.FromItemSection(1, 0) };
            }
            return Enumerable.Empty<NSIndexPath>();
        }

        private CGRect frameForItemWithLayoutAttributes(CalendarItemLayoutAttributes attrs)
        {
            var startTime = attrs.StartTime < date ? date : attrs.StartTime;
            var endTime = attrs.EndTime > date.AddDays(1) ? date.AddDays(1) : attrs.EndTime;
            var duration = endTime - startTime;

            var yHour = HourHeight * startTime.Hour;
            var yMins = HourHeight * startTime.Minute / 60;

            var totalInterItemSpacing = (attrs.TotalColumns - 1) * horizontalItemSpacing;
            var width = (CollectionViewContentSize.Width - leftPadding - rightPadding - totalInterItemSpacing - (sideMargin * 2)) / attrs.TotalColumns;
            var height = Math.Max(minItemHeight(), HourHeight * duration.TotalMinutes / 60) - verticalItemSpacing;
            var x = sideMargin + leftPadding + (width + horizontalItemSpacing) * attrs.ColumnIndex;
            var y = yHour + yMins + verticalItemSpacing;

            return new CGRect(x, y, width, height);
        }

        private CGRect frameForHour(int hour)
        {
            var width = CollectionViewContentSize.Width - rightPadding - (sideMargin * 2);
            var height = hourSupplementaryLabelHeight;
            var x = sideMargin;
            var y = HourHeight * hour - height / 2;

            return new CGRect(x, y, width, height);
        }

        private CGRect frameForEditingHour(NSIndexPath indexPath)
        {
            var attrs = dataSource.LayoutAttributesForItemAtIndexPath(dataSource.IndexPathForSelectedItem);

            var isStartTime = (int)indexPath.Item == 0;
            var time = isStartTime ? attrs.StartTime : attrs.EndTime;
            var yHour = HourHeight * time.Hour;
            var yMins = HourHeight * time.Minute / 60;

            if (!isStartTime && time.Hour == 0)
            {
                yHour = HourHeight * 24;
            }

            var width = CollectionViewContentSize.Width - rightPadding - (sideMargin * 2);
            var height = hourSupplementaryLabelHeight;
            var x = sideMargin;
            var y = yHour + yMins - height / 2;

            return new CGRect(x, y, width, height);
        }

        private void dateChanged(DateTimeOffset dateTimeOffset)
        {
            date = dateTimeOffset.ToLocalTime().Date;
            InvalidateLayout();
        }

        private void cacheSupplementaryItemAttributes(NSString kind, NSIndexPath indexPath, UICollectionViewLayoutAttributes attributes)
        {
            if (supplementaryViewLayoutAttributes.ContainsKey(kind))
            {
                supplementaryViewLayoutAttributes[kind][indexPath] = attributes;
            }
            else
            {
                supplementaryViewLayoutAttributes[kind] = new Dictionary<NSIndexPath, UICollectionViewLayoutAttributes> { [indexPath] = attributes };
            }
        }
    }
}
