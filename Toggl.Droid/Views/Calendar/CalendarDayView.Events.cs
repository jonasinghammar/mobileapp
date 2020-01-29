using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Android.Graphics;
using Android.Text;
using AndroidX.Core.Graphics;
using Toggl.Core.Calendar;
using Toggl.Core.UI.Calendar;
using Toggl.Core.UI.Collections;
using Toggl.Droid.Extensions;
using Toggl.Droid.ViewHelpers;
using Toggl.Shared.Extensions;

namespace Toggl.Droid.Views.Calendar
{
    public partial class CalendarDayView
    {
        private readonly Dictionary<string, StaticLayout> textLayouts = new Dictionary<string, StaticLayout>();
        private readonly float calendarItemColorAlpha = 0.25f;
        private readonly double minimumTextContrast = 1.6;
        private readonly RectF eventRect = new RectF();
        private readonly RectF stripeRect = new RectF();
        private readonly RectF itemInEditModeRect = new RectF();
        private readonly Paint eventsPaint = new Paint(PaintFlags.AntiAlias);
        private readonly Paint textEventsPaint = new Paint(PaintFlags.AntiAlias);
        private readonly Paint editingHoursLabelPaint = new Paint(PaintFlags.AntiAlias);
        private readonly Paint calendarIconPaint = new Paint(PaintFlags.AntiAlias);
        private readonly PathEffect dashEffect = new DashPathEffect(new []{ 10f, 10f }, 0f);
        private readonly CalendarItemStartTimeComparer calendarItemComparer = new CalendarItemStartTimeComparer();

        private float leftMargin;
        private float leftPadding;
        private float rightPadding;
        private float itemSpacing;
        private float runningTimeEntryStripesSpacing;
        private float runningTimeEntryThinStripeWidth;
        private int shortCalendarItemHeight;
        private int regularCalendarItemVerticalPadding;
        private int regularCalendarItemHorizontalPadding;
        private int shortCalendarItemVerticalPadding;
        private int shortCalendarItemHorizontalPadding;
        private int regularCalendarItemFontSize;
        private int shortCalendarItemFontSize;
        private int? runningTimeEntryIndex = null;
        private int editingHandlesHorizontalMargins;
        private int editingHandlesRadius;
        private Bitmap normalCalendarIconBitmap;
        private Bitmap smallCalendarIconBitmap;
        private int runningTimeEntryDashedHourTopPadding;
        private int calendarEventBottomLineHeight;
        private int calendarIconRightInsetMargin;
        private float commonRoundRectRadius;
        private int calendarIconSize;
        private Color lastCalendarItemBackgroundColor;
        private CalendarItemEditInfo itemEditInEditMode = CalendarItemEditInfo.None;

        private float minHourHeight => hourHeight / 4f;

        public void UpdateItems(ObservableGroupedOrderedCollection<CalendarItem> calendarItems)
        {
            var newItems = calendarItems.IsEmpty
                ? ImmutableList<CalendarItem>.Empty
                : calendarItems[0].ToImmutableList();

            originalCalendarItems = newItems;
            updateItemsAndRecalculateEventsAttrs(newItems);
        }

        partial void initEventDrawingBackingFields()
        {
            leftMargin = Context.GetDimen(Resource.Dimension.calendarEventsStartMargin);
            leftPadding = Context.GetDimen(Resource.Dimension.calendarEventsLeftPadding);
            rightPadding = Context.GetDimen(Resource.Dimension.calendarEventsRightPadding);
            itemSpacing = Context.GetDimen(Resource.Dimension.calendarEventsItemsSpacing);
            availableWidth = Width - leftMargin;

            shortCalendarItemHeight = Context.GetDimen(Resource.Dimension.shortCalendarItemHeight);
            regularCalendarItemVerticalPadding = Context.GetDimen(Resource.Dimension.regularCalendarItemVerticalPadding);
            regularCalendarItemHorizontalPadding = Context.GetDimen(Resource.Dimension.regularCalendarItemHorizontalPadding);
            shortCalendarItemVerticalPadding = Context.GetDimen(Resource.Dimension.shortCalendarItemVerticalPadding);
            shortCalendarItemHorizontalPadding = Context.GetDimen(Resource.Dimension.shortCalendarItemHorizontalPadding);
            regularCalendarItemFontSize = Context.GetDimen(Resource.Dimension.regularCalendarItemFontSize);
            shortCalendarItemFontSize = Context.GetDimen(Resource.Dimension.shortCalendarItemFontSize);

            eventsPaint.SetStyle(Paint.Style.FillAndStroke);
            textEventsPaint.TextSize = Context.GetDimen(Resource.Dimension.textEventsPaintTextSize);
            editingHoursLabelPaint.Color = Context.SafeGetColor(Resource.Color.accent);
            editingHoursLabelPaint.TextAlign = Paint.Align.Right;
            editingHoursLabelPaint.TextSize = Context.GetDimen(Resource.Dimension.editingHoursLabelPaintTextSize);
            editingHandlesHorizontalMargins = Context.GetDimen(Resource.Dimension.editingHandlesHorizontalMargins);
            editingHandlesRadius = Context.GetDimen(Resource.Dimension.editingHandlesRadius);
            runningTimeEntryStripesSpacing = Context.GetDimen(Resource.Dimension.calendarRunningTimeEntryStripesSpacing);
            runningTimeEntryThinStripeWidth = Context.GetDimen(Resource.Dimension.calendarRunningTimeEntryThinStripeWidth);
            commonRoundRectRadius = leftPadding / 2;
            runningTimeEntryDashedHourTopPadding = Context.GetDimen(Resource.Dimension.calendarRunningTimeEntryDashedHourTopPadding);
            calendarEventBottomLineHeight = Context.GetDimen(Resource.Dimension.calendarEventBottomLineHeight);
            calendarIconSize = Context.GetDimen(Resource.Dimension.calendarIconSize);
            calendarIconRightInsetMargin = Context.GetDimen(Resource.Dimension.calendarIconRightInsetMargin);
            normalCalendarIconBitmap = Context.GetVectorDrawable(Resource.Drawable.ic_calendar).ToBitmap(calendarIconSize, calendarIconSize);
            smallCalendarIconBitmap = Context.GetVectorDrawable(Resource.Drawable.ic_calendar).ToBitmap(calendarIconSize / 2, calendarIconSize / 2);
        }

        private void updateItemsAndRecalculateEventsAttrs(ImmutableList<CalendarItem> newItems)
        {
            if (availableWidth > 0)
            {
                if (itemEditInEditMode.IsValid && itemEditInEditMode.HasChanged)
                    newItems = newItems.Sort(calendarItemComparer);

                calendarItemLayoutAttributes = calendarLayoutCalculator
                    .CalculateLayoutAttributes(newItems)
                    .Select(calculateCalendarItemRect)
                    .ToImmutableList();

                textLayouts.Clear();
            }

            var runningIndex = newItems.IndexOf(item => item.Duration == null);
            runningTimeEntryIndex = runningIndex >= 0 ? runningIndex : (int?)null;
            calendarItems = newItems;
            updateItemInEditMode();

            PostInvalidate();
        }

        private void updateItemInEditMode()
        {
            var currentItemInEditMode = itemEditInEditMode;
            if (!currentItemInEditMode.IsValid) return;

            var calendarItemsToSearch = calendarItems;
            var calendarItemsAttrsToSearch = calendarItemLayoutAttributes;
            var newCalendarItemInEditModeIndex = calendarItemsToSearch.IndexOf(item => item.Id == currentItemInEditMode.CalendarItem.Id);

            if (newCalendarItemInEditModeIndex < 0)
            {
                itemEditInEditMode = CalendarItemEditInfo.None;
            }
            else
            {
                var newLayoutAttr = calendarItemsAttrsToSearch[newCalendarItemInEditModeIndex];
                itemEditInEditMode = new CalendarItemEditInfo(
                    currentItemInEditMode.CalendarItem,
                    newLayoutAttr,
                    newCalendarItemInEditModeIndex,
                    hourHeight,
                    minHourHeight,
                    timeService.CurrentDateTime);
                itemEditInEditMode.CalculateRect(itemInEditModeRect);
            }
        }

        partial void processEventsOnLayout()
        {
            updateItemsAndRecalculateEventsAttrs(calendarItems);
        }

        private CalendarItemRectAttributes calculateCalendarItemRect(CalendarItemLayoutAttributes attrs)
        {
            var totalItemSpacing = (attrs.TotalColumns - 1) * itemSpacing;
            var eventWidth = (availableWidth - leftPadding - rightPadding - totalItemSpacing) / attrs.TotalColumns;
            var left = leftMargin + leftPadding + eventWidth * attrs.ColumnIndex + attrs.ColumnIndex * itemSpacing;

            return new CalendarItemRectAttributes(attrs, left, left + eventWidth);
        }

        partial void drawCalendarItems(Canvas canvas)
        {
            var itemsToDraw = calendarItems;
            var itemsAttrs = calendarItemLayoutAttributes;
            var currentItemInEditMode = itemEditInEditMode;

            for (var eventIndex = 0; eventIndex < itemsAttrs.Count; eventIndex++)
            {
                var item = itemsToDraw[eventIndex];
                var itemAttr = itemsAttrs[eventIndex];

                if (item.Id == currentItemInEditMode.CalendarItem.Id) continue;

                itemAttr.CalculateRect(hourHeight, minHourHeight, eventRect);
                if (!(eventRect.Bottom > scrollOffset) || !(eventRect.Top - scrollOffset < Height)) continue;

                drawCalendarShape(canvas, item, eventRect, eventIndex == runningTimeEntryIndex);
                drawCalendarItemText(canvas, item, eventRect, eventIndex == runningTimeEntryIndex);
            }

            drawCalendarItemInEditMode(canvas, currentItemInEditMode);
        }

        private void drawCalendarItemInEditMode(Canvas canvas, CalendarItemEditInfo currentItemInEditMode)
        {
            if (!currentItemInEditMode.IsValid) return;

            var calendarItem = currentItemInEditMode.CalendarItem;

            if (!(itemInEditModeRect.Bottom > scrollOffset) || !(itemInEditModeRect.Top - scrollOffset < Height)) return;

            drawCalendarShape(canvas, calendarItem, itemInEditModeRect, itemIsRunning(currentItemInEditMode));
            drawCalendarItemText(canvas, calendarItem, itemInEditModeRect, itemIsRunning(currentItemInEditMode));
            drawEditingHandles(canvas, currentItemInEditMode);
            canvas.DrawText(startHourLabel, hoursX, itemInEditModeRect.Top + editingHoursLabelPaint.Descent(), editingHoursLabelPaint);
            canvas.DrawText(endHourLabel, hoursX, itemInEditModeRect.Bottom + editingHoursLabelPaint.Descent(), editingHoursLabelPaint);
        }

        private void drawEditingHandles(Canvas canvas, CalendarItemEditInfo itemInEditModeToDraw)
        {
            eventsPaint.Color = Color.White;
            eventsPaint.SetStyle(Paint.Style.FillAndStroke);

            canvas.DrawCircle(itemInEditModeRect.Right - editingHandlesHorizontalMargins, itemInEditModeRect.Top, editingHandlesRadius, eventsPaint);
            if (!itemIsRunning(itemInEditModeToDraw))
                canvas.DrawCircle(itemInEditModeRect.Left + editingHandlesHorizontalMargins, itemInEditModeRect.Bottom, editingHandlesRadius, eventsPaint);

            eventsPaint.SetStyle(Paint.Style.Stroke);
            eventsPaint.StrokeWidth = 1.DpToPixels(Context);
            eventsPaint.Color = new Color(Color.ParseColor(itemInEditModeToDraw.CalendarItem.Color));

            canvas.DrawCircle(itemInEditModeRect.Right - editingHandlesHorizontalMargins, itemInEditModeRect.Top, editingHandlesRadius, eventsPaint);
            if (!itemIsRunning(itemInEditModeToDraw))
                canvas.DrawCircle(itemInEditModeRect.Left + editingHandlesHorizontalMargins, itemInEditModeRect.Bottom, editingHandlesRadius, eventsPaint);
        }

        private bool itemIsRunning(CalendarItemEditInfo itemInEditModeToDraw)
            => itemInEditModeToDraw.OriginalIndex == runningTimeEntryIndex;

        private void drawCalendarShape(Canvas canvas, CalendarItem item, RectF calendarItemRect, bool isRunning)
        {
            if (!isRunning)
                drawRegularCalendarItemShape(canvas, item, calendarItemRect);
            else
                drawRunningTimeEntryCalendarItemShape(canvas, item, calendarItemRect);
        }

        private void drawRegularCalendarItemShape(Canvas canvas, CalendarItem item, RectF calendarItemRect)
        {
            if (item.Source == CalendarItemSource.Calendar)
                drawCalendarEventItemShape(canvas, item, calendarItemRect);
            else
                drawCalendarTimeEntryItemShape(canvas, item, calendarItemRect);
        }

        private void drawCalendarEventItemShape(Canvas canvas, CalendarItem item, RectF calendarItemRect)
        {
            var originalColor = Color.ParseColor(item.Color);
            var fadedColor = new Color(originalColor); 
            fadedColor.A = (byte)(originalColor.A * calendarItemColorAlpha);
            fadedColor = new Color(ColorUtils.CompositeColors(fadedColor, ColorObject.White));
            eventsPaint.SetStyle(Paint.Style.FillAndStroke);
            eventsPaint.Color = fadedColor;
            lastCalendarItemBackgroundColor = fadedColor;
            canvas.DrawRoundRect(calendarItemRect, commonRoundRectRadius, commonRoundRectRadius, eventsPaint);

            eventsPaint.Color = originalColor;
            canvas.DrawRoundRect(calendarItemRect.Left, calendarItemRect.Bottom - calendarEventBottomLineHeight, calendarItemRect.Right, calendarItemRect.Bottom, commonRoundRectRadius, commonRoundRectRadius, eventsPaint);

            var calendarBitmap = getProperlySizedCalendarBitmap(calendarItemRect);
            if (calendarBitmap == null)
                return;

            calendarIconPaint.SetColorFilter(new PorterDuffColorFilter(originalColor, PorterDuff.Mode.SrcIn));
            canvas.DrawBitmap(calendarBitmap, calendarItemRect.Left, calendarItemRect.Top, calendarIconPaint);
        }

        private Bitmap getProperlySizedCalendarBitmap(RectF calendarItemRect)
        {
            var containerHeight = calendarItemRect.Height();
            if (containerHeight > normalCalendarIconBitmap.Height)
                return normalCalendarIconBitmap;

            if (containerHeight > smallCalendarIconBitmap.Height)
                return smallCalendarIconBitmap;

            return null;
        }

        private void drawCalendarTimeEntryItemShape(Canvas canvas, CalendarItem item, RectF calendarItemRect)
        {
            var color = Color.ParseColor(item.Color);
            eventsPaint.SetStyle(Paint.Style.FillAndStroke);
            eventsPaint.Color = color;
            lastCalendarItemBackgroundColor = color;
            canvas.DrawRoundRect(calendarItemRect, commonRoundRectRadius, commonRoundRectRadius, eventsPaint);
        }

        private void drawRunningTimeEntryCalendarItemShape(Canvas canvas, CalendarItem item, RectF calendarItemRect)
        {
            var itemColor = Color.ParseColor(item.Color);
            var calendarFillColor = new Color(itemColor);
            calendarFillColor.A = (byte) (calendarFillColor.A * 0.05f);
            var bgColor = Context.SafeGetColor(Resource.Color.cardBackground);
            calendarFillColor = new Color(ColorUtils.CompositeColors(calendarFillColor, bgColor));

            var calendarStripeColor = new Color(itemColor);
            calendarStripeColor.A = (byte) (calendarStripeColor.A * 0.1f);

            lastCalendarItemBackgroundColor = calendarFillColor;
            drawShapeBaseBackgroundFilling(calendarItemRect, calendarFillColor);
            drawShapeBackgroundStripes(calendarItemRect, calendarStripeColor);
            drawSolidBorder(calendarItemRect, itemColor);
            drawBottomDashedBorder(calendarItemRect, calendarFillColor);

            void drawShapeBaseBackgroundFilling(RectF rectF, Color color)
            {
                eventsPaint.Color = color;
                eventsPaint.SetStyle(Paint.Style.FillAndStroke);
                canvas.DrawRoundRect(rectF, commonRoundRectRadius, commonRoundRectRadius, eventsPaint);
            }

            void drawShapeBackgroundStripes(RectF shapeRect, Color color)
            {
                canvas.Save();
                canvas.ClipRect(shapeRect);
                canvas.Rotate(45f, shapeRect.Left, shapeRect.Top);
                eventsPaint.Color = color;
                var hyp = (float) Math.Sqrt(Math.Pow(shapeRect.Height(), 2) + Math.Pow(shapeRect.Width(), 2));
                stripeRect.Set(shapeRect.Left, shapeRect.Top - hyp, shapeRect.Left + runningTimeEntryThinStripeWidth, shapeRect.Bottom + hyp);
                for (var stripeStart = 0f; stripeStart < hyp; stripeStart += runningTimeEntryStripesSpacing)
                {
                    stripeRect.Set(shapeRect.Left + stripeStart, stripeRect.Top, shapeRect.Left + stripeStart + runningTimeEntryThinStripeWidth, stripeRect.Bottom);
                    canvas.DrawRect(stripeRect, eventsPaint);
                }

                canvas.Restore();
            }

            void drawSolidBorder(RectF borderRect, Color color)
            {
                eventsPaint.SetStyle(Paint.Style.Stroke);
                eventsPaint.StrokeWidth = 1.DpToPixels(Context);
                eventsPaint.Color = color;
                canvas.DrawRoundRect(borderRect, commonRoundRectRadius, commonRoundRectRadius, eventsPaint);
            }

            void drawBottomDashedBorder(RectF dashedBorderRect, Color color)
            {
                canvas.Save();
                var currentHourPx = calculateCurrentHourOffset() - runningTimeEntryDashedHourTopPadding;
                var sevenMinutesInPixels = (float) TimeSpan.FromMinutes(7).TotalHours * hourHeight;
                var bottom = dashedBorderRect.Bottom + 2.DpToPixels(Context);
                stripeRect.Set(dashedBorderRect.Left - eventsPaint.StrokeWidth, currentHourPx, dashedBorderRect.Right + eventsPaint.StrokeWidth, bottom + eventsPaint.StrokeWidth);
                canvas.ClipRect(stripeRect);
                eventsPaint.SetPathEffect(dashEffect);

                eventsPaint.Color = color;
                stripeRect.Set(dashedBorderRect.Left, stripeRect.Top - sevenMinutesInPixels, dashedBorderRect.Right, dashedBorderRect.Bottom);

                canvas.DrawRoundRect(stripeRect, commonRoundRectRadius, commonRoundRectRadius, eventsPaint);
                eventsPaint.SetPathEffect(null);
                canvas.Restore();
            }
        }

        private void drawCalendarItemText(Canvas canvas, CalendarItem calendarItem, RectF calendarItemRect, bool isRunning)
        {
            var textLeftPadding = calendarItem.Source == CalendarItemSource.Calendar ? calendarIconSize - calendarIconRightInsetMargin : 0;
            var eventHeight = calendarItemRect.Height();
            var eventWidth = calendarItemRect.Width() - textLeftPadding;
            var fontSize = eventHeight <= shortCalendarItemHeight ? shortCalendarItemFontSize : regularCalendarItemFontSize;
            var textVerticalPadding = eventHeight <= shortCalendarItemHeight ? shortCalendarItemVerticalPadding : regularCalendarItemVerticalPadding;
            textVerticalPadding = (int) Math.Min((eventHeight - fontSize) / 2f, textVerticalPadding);
            var textHorizontalPadding = eventHeight <= shortCalendarItemHeight ? shortCalendarItemHorizontalPadding : regularCalendarItemHorizontalPadding;
            
            var textWidth = eventWidth - textHorizontalPadding * 2;
            if (textWidth <= 0) return;
            
            var eventTextLayout = getCalendarItemTextLayout(calendarItem, textWidth, fontSize, isRunning);
            var totalLineHeight = calculateLineHeight(eventHeight, eventTextLayout);

            canvas.Save();
            canvas.Translate(calendarItemRect.Left + textHorizontalPadding + textLeftPadding, calendarItemRect.Top + textVerticalPadding);
            canvas.ClipRect(0, 0, eventWidth - textHorizontalPadding, totalLineHeight);
            eventTextLayout.Draw(canvas);
            canvas.Restore();
        }

        private static int calculateLineHeight(double eventHeight, StaticLayout eventTextLayout)
        {
            var totalLineHeight = 0;
            for (var i = 0; i < eventTextLayout.LineCount; i++)
            {
                var lineBottom = eventTextLayout.GetLineBottom(i);
                if (lineBottom <= eventHeight)
                {
                    totalLineHeight = lineBottom;
                }
            }

            return totalLineHeight;
        }

        private StaticLayout getCalendarItemTextLayout(CalendarItem item, float eventWidth, int fontSize, bool isRunning)
        {
            textLayouts.TryGetValue(item.Id, out var eventTextLayout);
            if (eventTextLayout != null && !(Math.Abs(eventTextLayout.Width - eventWidth) > 0.1) && eventTextLayout.Text == item.Description)
                return eventTextLayout;

            var color = item.Source == CalendarItemSource.Calendar || isRunning
                ? Color.ParseColor(item.Color)
                : Color.White;

            var primaryTextColor = Context.SafeGetColor(Resource.Color.primaryText);
            var itemColorContrast = ColorUtils.CalculateContrast(color, lastCalendarItemBackgroundColor);
            color = itemColorContrast >= minimumTextContrast ? color : primaryTextColor;

            textEventsPaint.Color = color;
            textEventsPaint.TextSize = fontSize;

            eventTextLayout = new StaticLayout(item.Description,
                0,
                item.Description.Length,
                new TextPaint(textEventsPaint),
                (int) eventWidth,
                Android.Text.Layout.Alignment.AlignNormal,
                1.0f,
                0.0f,
                true,
                TextUtils.TruncateAt.End,
                (int) eventWidth);
            textLayouts[item.Id] = eventTextLayout;

            return eventTextLayout;
        }

        private sealed class CalendarItemStartTimeComparer : Comparer<CalendarItem>
        {
            public override int Compare(CalendarItem x, CalendarItem y)
                => x.StartTime.LocalDateTime.CompareTo(y.StartTime.LocalDateTime);
        }
    }
}
