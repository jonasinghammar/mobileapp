using System;
using System.Collections.Immutable;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Android.OS;
using Android.Text;
using Android.Views;
using AndroidX.Fragment.App;
using Toggl.Core.UI.ViewModels.Calendar;
using Toggl.Core.UI.ViewModels.Calendar.ContextualMenu;
using Toggl.Core.UI.Views;
using Toggl.Droid.Adapters;
using Toggl.Droid.Extensions;
using Toggl.Droid.Extensions.Reactive;
using Toggl.Droid.ViewHolders;
using Toggl.Shared.Extensions;
using Toggl.Shared.Extensions.Reactive;
using TimeEntryExtensions = Toggl.Droid.Extensions.TimeEntryExtensions;

namespace Toggl.Droid.Fragments.Calendar
{
    public partial class CalendarDayViewPageFragment : Fragment, IView
    {
        private readonly TimeSpan defaultTimeEntryDurationForCreation = TimeSpan.FromMinutes(30);
        private readonly BehaviorSubject<string> timeEntryPeriodObserver = new BehaviorSubject<string>(string.Empty);
        private readonly BehaviorSubject<ISpannable> timeEntryDetailsObserver = new BehaviorSubject<ISpannable>(new SpannableString(string.Empty));
        
        private IDisposable dismissActionDisposeBag;
        private CompositeDisposable DisposeBag = new CompositeDisposable();
        private CalendarContextualMenuActionsAdapter menuActionsAdapter;

        public CalendarDayViewModel ViewModel { get; set; }
        public BehaviorRelay<int> CurrentPageRelay { get; set; }
        public BehaviorRelay<int> HourHeightRelay { get; set; }
        public BehaviorRelay<int> ScrollOffsetRelay { get; set; }
        public BehaviorRelay<bool> MenuVisibilityRelay { get; set; }
        public BehaviorRelay<string> TimeTrackedOnDay { get; set; }
        public IObservable<int> ScrollingPage { get; set; }
        public IObservable<bool> ScrollToStartSign { get; set; }
        public IObservable<Unit> InvalidationListener { get; set; }
        public IObservable<Unit> BackPressListener { get; set; }

        public int PageNumber { get; set; }

        public override View OnCreateView(LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate(Resource.Layout.CalendarDayFragmentPage, container, false);
            initializeViews(view);
            return view;
        }

        public override void OnViewCreated(View view, Bundle savedInstanceState)
        {
            base.OnViewCreated(view, savedInstanceState);
            if (ViewModel == null) return;

            ViewModel.AttachView(this);
            ViewModel.ContextualMenuViewModel.AttachView(this);

            ScrollingPage.
                Subscribe(scrollingPage => handleCalendarDayViewVisibility(scrollingPage, true))
                .DisposedBy(DisposeBag);
            
            CurrentPageRelay
                .Subscribe(currentPage => handleCalendarDayViewVisibility(currentPage, false))
                .DisposedBy(DisposeBag);
            
            ViewModel.ContextualMenuViewModel
                .MenuVisible
                .Subscribe(handleMenuVisibility)
                .DisposedBy(DisposeBag);

            ViewModel.TimeTrackedOnDay
                .CombineLatest(CurrentPageRelay, CommonFunctions.First)
                .Subscribe(notifyTotalDurationIfCurrentPage)
                .DisposedBy(DisposeBag);
            
            ViewModel.ContextualMenuViewModel
                .TimeEntryPeriod
                .Subscribe(timeEntryPeriodObserver)
                .DisposedBy(DisposeBag);
            
            ViewModel.ContextualMenuViewModel
                .TimeEntryInfo
                .Select(convertTimeEntryInfoToSpannable)
                .Subscribe(timeEntryDetailsObserver)
                .DisposedBy(DisposeBag);

            InvalidationListener?
                .Subscribe(_ => invalidatePage())
                .DisposedBy(DisposeBag);
            
            BackPressListener?
                .Subscribe(_ => discardCurrentItemInEditMode())
                .DisposedBy(DisposeBag);
        }
        
        private void handleCalendarDayViewVisibility(int page, bool ignorePageNumber)
        {
            if (calendarDayView != null || !ignorePageNumber && page != PageNumber) return;
            
            initializeCalendarDayView();
            initializeDayViewBindings();
        }

        private void initializeDayViewBindings()
        {
            calendarDayView.SetCurrentDate(ViewModel.Date);
            calendarDayView.SetOffset(ScrollOffsetRelay?.Value ?? 0);
            calendarDayView.SetHourHeight(HourHeightRelay?.Value ?? 56.DpToPixels(Context));
            calendarDayView.UpdateItems(ViewModel.CalendarItems);
            
            ViewModel.TimeOfDayFormat
                .Subscribe(calendarDayView.UpdateCalendarHoursFormat)
                .DisposedBy(DisposeBag);

            ViewModel.CalendarItems.CollectionChange
                .Subscribe(_ => calendarDayView.UpdateItems(ViewModel.CalendarItems))
                .DisposedBy(DisposeBag);

            calendarDayView.CalendarItemTappedObservable
                .Subscribe(ViewModel.ContextualMenuViewModel.OnCalendarItemUpdated.Inputs)
                .DisposedBy(DisposeBag);

            calendarDayView.EmptySpansTouchedObservable
                .Select(emptySpan => (emptySpan, defaultTimeEntryDurationForCreation))
                .Subscribe(ViewModel.OnDurationSelected.Inputs)
                .DisposedBy(DisposeBag);

            ScrollToStartSign?
                .Subscribe(scrollToStart)
                .DisposedBy(DisposeBag);

            calendarDayView.ScrollOffsetObservable
                .Subscribe(updateScrollOffsetIfCurrentPage)
                .DisposedBy(DisposeBag);

            calendarDayView.HourHeight
                .Subscribe(updateHourHeightIfCurrentPage)
                .DisposedBy(DisposeBag);
            
            ViewModel.ContextualMenuViewModel
                .CalendarItemInEditMode
                .Subscribe(calendarDayView.SetCurrentItemInEditMode)
                .DisposedBy(DisposeBag);

            ViewModel.ContextualMenuViewModel
                .MenuVisible
                .Where(menuIsVisible => !menuIsVisible)
                .Subscribe(_ => calendarDayView.ClearEditMode())
                .DisposedBy(DisposeBag);

            ViewModel.ContextualMenuViewModel
                .DiscardChanges
                .Subscribe(_ => calendarDayView.DiscardEditModeChanges())
                .DisposedBy(DisposeBag);
        }
        private void handleMenuVisibility(bool visible)
        {
            var shouldBeVisible = visible && PageNumber == CurrentPageRelay.Value;
            if (shouldBeVisible)
            {
                if (contextualMenuContainer == null)
                {
                    initializeContextualMenuView();
                    initializeContextualMenuBindings();
                }
                contextualMenuContainer.Visibility = ViewStates.Visible;
                MenuVisibilityRelay?.Accept(true);
            }

            if (!shouldBeVisible && contextualMenuContainer != null)
            {
                contextualMenuContainer.Visibility = ViewStates.Gone;
            }
        }

        private void initializeContextualMenuBindings()
        {
            ViewModel.ContextualMenuViewModel
                .CurrentMenu
                .Subscribe(updateMenuBindings)
                .DisposedBy(DisposeBag);

            ViewModel.ContextualMenuViewModel
                .MenuVisible
                .Subscribe(notifyMenuVisibilityIfCurrentPage)
                .DisposedBy(DisposeBag);

            timeEntryPeriodObserver
                .Subscribe(periodText.Rx().TextObserver())
                .DisposedBy(DisposeBag);

            timeEntryDetailsObserver
                .Subscribe(timeEntryDetails.Rx().TextFormattedObserver())
                .DisposedBy(DisposeBag);
        }

        private void discardCurrentItemInEditMode()
        {
            ViewModel.ContextualMenuViewModel.OnCalendarItemUpdated.Execute(null);
        }

        private void invalidatePage()
        {
            View.PostInvalidateOnAnimation();
            calendarDayView?.PostInvalidateOnAnimation();
            contextualMenuContainer?.PostInvalidateOnAnimation();
        }

        private void updateMenuBindings(CalendarContextualMenu contextualMenu)
        {
            menuActionsAdapter.Items = contextualMenu.Actions;
            dismissActionDisposeBag?.Dispose();
            dismissActionDisposeBag = dismissButton.Rx().Tap()
                .Subscribe(contextualMenu.Dismiss.Inputs);
        }

        private void notifyMenuVisibilityIfCurrentPage(bool menuIsVisible)
        {
            if (CurrentPageRelay?.Value == PageNumber)
            {
                MenuVisibilityRelay?.Accept(menuIsVisible);
            }
        }

        private void notifyTotalDurationIfCurrentPage(string durationString)
        {
            if (CurrentPageRelay?.Value == PageNumber)
            {
                TimeTrackedOnDay?.Accept(durationString);
            }
        }

        private ISpannable convertTimeEntryInfoToSpannable(TimeEntryDisplayInfo timeEntryDisplayInfo)
        {
            var description = string.IsNullOrEmpty(timeEntryDisplayInfo.Description)
                ? Shared.Resources.NoDescription
                : timeEntryDisplayInfo.Description;
            var hasProject = !string.IsNullOrEmpty(timeEntryDisplayInfo.Project);
            var builder = new SpannableStringBuilder();
            var projectTaskClient = TimeEntryExtensions.ToProjectTaskClient(
                Context,
                hasProject,
                timeEntryDisplayInfo.Project,
                timeEntryDisplayInfo.ProjectTaskColor,
                timeEntryDisplayInfo.Task,
                timeEntryDisplayInfo.Client,
                false,
                false
            );

            builder.Append(description);
            builder.Append(" ");
            builder.Append(projectTaskClient);
            return builder;
        }

        private void updateScrollOffsetIfCurrentPage(int scrollOffset)
        {
            if (PageNumber != CurrentPageRelay?.Value)
                return;

            ScrollOffsetRelay?.Accept(scrollOffset);
        }

        private void updateHourHeightIfCurrentPage(int scrollOffset)
        {
            if (PageNumber != CurrentPageRelay?.Value)
                return;

            HourHeightRelay?.Accept(scrollOffset);
        }

        private void scrollToStart(bool shouldSmoothScroll)
        {
            if (PageNumber != CurrentPageRelay?.Value)
                return;

            calendarDayView?.ScrollToCurrentHour(shouldSmoothScroll);
        }

        public override void OnDestroyView()
        {
            ViewModel?.DetachView();
            dismissActionDisposeBag?.Dispose();
            DisposeBag.Dispose();
            DisposeBag = new CompositeDisposable();
            base.OnDestroyView();
        }

        private class CalendarContextualMenuActionsAdapter : BaseRecyclerAdapter<CalendarMenuAction>
        {
            private IImmutableList<CalendarMenuAction> items = ImmutableList<CalendarMenuAction>.Empty;

            public override IImmutableList<CalendarMenuAction> Items
            {
                get => items;
                set => SetItems(value ?? ImmutableList<CalendarMenuAction>.Empty);
            }
            
            protected override void SetItems(IImmutableList<CalendarMenuAction> newItems)
            {
                items = newItems;
                NotifyDataSetChanged();
            }

            public override CalendarMenuAction GetItem(int viewPosition)
            {
                return items[viewPosition];
            }

            protected override BaseRecyclerViewHolder<CalendarMenuAction> CreateViewHolder(ViewGroup parent, LayoutInflater inflater, int viewType)
            {
                var view = inflater.Inflate(Resource.Layout.ContextualMenuActionCell, parent, false);
                return CalendarMenuActionCellViewHolder.Create(view);
            }

            public override int ItemCount => items.Count;
        }
    }
}
