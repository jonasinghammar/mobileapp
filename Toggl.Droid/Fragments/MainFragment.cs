using Android.App;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.OS;
using Android.Text;
using Android.Views;
using Android.Widget;
using System;
using System.Collections.Immutable;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Toggl.Core.Analytics;
using Toggl.Core.Extensions;
using Toggl.Core.Models.Interfaces;
using Toggl.Core.Sync;
using Toggl.Core.UI.Extensions;
using Toggl.Core.UI.Helper;
using Toggl.Core.UI.ViewModels;
using Toggl.Core.UI.ViewModels.MainLog;
using Toggl.Droid.Adapters;
using Toggl.Droid.Extensions;
using Toggl.Droid.Extensions.Reactive;
using Toggl.Droid.Helper;
using Toggl.Droid.Presentation;
using Toggl.Droid.ViewHelpers;
using Toggl.Shared.Extensions;
using static Android.Content.Context;
using static Toggl.Core.Sync.SyncProgress;
using static Toggl.Core.Analytics.EditTimeEntryOrigin;
using FoundationResources = Toggl.Shared.Resources;
using AndroidX.Core.Content;
using AndroidX.RecyclerView.Widget;
using Google.Android.Material.Snackbar;
using Toggl.Core;
using Toggl.Core.UI.Collections;
using Toggl.Core.UI.ViewModels.MainLog.Identity;
using Toggl.Droid.Views;

namespace Toggl.Droid.Fragments
{
    public sealed partial class MainFragment : ReactiveTabFragment<MainViewModel>, IScrollableToStart
    {
        private const int snackbarDuration = 5000;
        private NotificationManager notificationManager;
        private MainLogRecyclerAdapter mainLogRecyclerAdapter;
        private MainRecyclerViewTouchCallback touchCallback;
        private LinearLayoutManager layoutManager;
        private ISubject<bool> visibilityChangedSubject = new BehaviorSubject<bool>(false);
        private IObservable<bool> visibilityChanged => visibilityChangedSubject.AsObservable();

        private Drawable addDrawable;
        private Drawable playDrawable;

        public override View OnCreateView(LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate(Resource.Layout.MainFragment, container, false);

            InitializeViews(view);
            SetupToolbar(view);
            mainRecyclerView.AttachMaterialScrollBehaviour(appBarLayout);

            return view;
        }

        public override void OnViewCreated(View view, Bundle savedInstanceState)
        {
            base.OnViewCreated(view, savedInstanceState);

            refreshLayout.SetProgressBackgroundColorSchemeResource(Resource.Color.cardBackground);
            refreshLayout.SetColorSchemeResources(new[] { Resource.Color.primaryText });

            stopButton.Rx().BindAction(ViewModel.StopTimeEntry, _ => TimeEntryStopOrigin.Manual).DisposedBy(DisposeBag);

            playButton.Rx().BindAction(ViewModel.StartTimeEntry, _ => true).DisposedBy(DisposeBag);
            playButton.Rx().BindAction(ViewModel.StartTimeEntry, _ => false, ButtonEventType.LongPress).DisposedBy(DisposeBag);

            runningEntryCardFrame.Rx().Tap()
                .WithLatestFrom(ViewModel.CurrentRunningTimeEntry,
                    (_, te) => new EditTimeEntryInfo(RunningTimeEntryCard, te.Id))
                .Subscribe(ViewModel.SelectTimeEntry.Inputs)
                .DisposedBy(DisposeBag);

            ViewModel.ElapsedTime
                .Subscribe(timeEntryCardTimerLabel.Rx().TextObserver())
                .DisposedBy(DisposeBag);

            ViewModel.CurrentRunningTimeEntry
                .Select(te => te?.Description ?? "")
                .Subscribe(timeEntryCardDescriptionLabel.Rx().TextObserver())
                .DisposedBy(DisposeBag);

            ViewModel.CurrentRunningTimeEntry
                .Select(te => string.IsNullOrWhiteSpace(te?.Description))
                .Subscribe(timeEntryCardAddDescriptionLabel.Rx().IsVisible())
                .DisposedBy(DisposeBag);

            ViewModel.CurrentRunningTimeEntry
                .Select(te => string.IsNullOrWhiteSpace(te?.Description))
                .Invert()
                .Subscribe(timeEntryCardDescriptionLabel.Rx().IsVisible())
                .DisposedBy(DisposeBag);

            ViewModel.CurrentRunningTimeEntry
                .Select(CreateProjectClientTaskLabel)
                .Subscribe(timeEntryCardProjectClientTaskLabel.Rx().TextFormattedObserver())
                .DisposedBy(DisposeBag);

            var projectVisibilityObservable = ViewModel.CurrentRunningTimeEntry
                .Select(te => te?.Project != null);

            projectVisibilityObservable
                .Subscribe(timeEntryCardProjectClientTaskLabel.Rx().IsVisible())
                .DisposedBy(DisposeBag);

            projectVisibilityObservable
                .Subscribe(timeEntryCardDotContainer.Rx().IsVisible())
                .DisposedBy(DisposeBag);

            var projectColorObservable = ViewModel.CurrentRunningTimeEntry
                .Select(te => te?.Project?.Color ?? "#000000")
                .Select(Color.ParseColor);

            projectColorObservable
                .Subscribe(timeEntryCardDotView.Rx().DrawableColor())
                .DisposedBy(DisposeBag);

            addDrawable = ContextCompat.GetDrawable(Context, Resource.Drawable.ic_add);
            playDrawable = ContextCompat.GetDrawable(Context, Resource.Drawable.ic_play_big);

            ViewModel.IsInManualMode
                .Select(isManualMode => isManualMode ? addDrawable : playDrawable)
                .Subscribe(playButton.SetDrawableImageSafe)
                .DisposedBy(DisposeBag);

            ViewModel.IsTimeEntryRunning
                .Subscribe(visible => playButton.SetExpanded(visible))
                .DisposedBy(DisposeBag);

            ViewModel.SyncProgressState
                .Subscribe(onSyncChanged)
                .DisposedBy(DisposeBag);

            mainLogRecyclerAdapter = new MainLogRecyclerAdapter();
            touchCallback = new MainRecyclerViewTouchCallback(mainLogRecyclerAdapter);

            setupRecycler();

            mainLogRecyclerAdapter.ToggleGroupExpansion
                .Subscribe(ViewModel.TimeEntriesViewModel.ToggleGroupExpansion.Inputs)
                .DisposedBy(DisposeBag);

            mainLogRecyclerAdapter.EditTimeEntry
                .Select(editEventInfo)
                .Subscribe(ViewModel.SelectTimeEntry.Inputs)
                .DisposedBy(DisposeBag);

            mainLogRecyclerAdapter.ContinueTimeEntry
                .Subscribe(ViewModel.ContinueTimeEntry.Inputs)
                .DisposedBy(DisposeBag);

            mainLogRecyclerAdapter.DeleteTimeEntrySubject
                .Select(vm => vm.RepresentedTimeEntriesIds)
                .Subscribe(ViewModel.TimeEntriesViewModel.DelayDeleteTimeEntries.Inputs)
                .DisposedBy(DisposeBag);

            mainLogRecyclerAdapter.ContinueSuggestion
                .Select(vm => vm.Suggestion)
                .Subscribe(ViewModel.SuggestionsViewModel.StartTimeEntry.Inputs)
                .DisposedBy(DisposeBag);

            ViewModel.TimeEntriesViewModel.TimeEntriesPendingDeletion
                .Subscribe(showUndoDeletion)
                .DisposedBy(DisposeBag);

            ViewModel.SyncProgressState
                .Subscribe(updateSyncingIndicator)
                .DisposedBy(DisposeBag);

            refreshLayout.Rx().Refreshed()
                .Subscribe(ViewModel.Refresh.Inputs)
                .DisposedBy(DisposeBag);

            ViewModel.MainLogItems
                .Subscribe(updateMainLog)
                .DisposedBy(DisposeBag);

            ViewModel.IsTimeEntryRunning
                .Subscribe(updateRecyclerViewPadding)
                .DisposedBy(DisposeBag);

            notificationManager = Activity.GetSystemService(NotificationService) as NotificationManager;
            Activity.BindRunningTimeEntry(notificationManager, ViewModel.CurrentRunningTimeEntry, ViewModel.ShouldShowRunningTimeEntryNotification)
                .DisposedBy(DisposeBag);
            Activity.BindIdleTimer(notificationManager, ViewModel.IsTimeEntryRunning, ViewModel.ShouldShowStoppedTimeEntryNotification)
                .DisposedBy(DisposeBag);

            ViewModel.SwipeActionsEnabled
                .Subscribe(onSwipeActionsChanged)
                .DisposedBy(DisposeBag);

            setupItemTouchHelper(touchCallback);

            ViewModel.TimeEntriesCount
                .Subscribe(timeEntriesCountSubject)
                .DisposedBy(DisposeBag);

            ViewModel.ShouldReloadTimeEntryLog
                .Subscribe(reload)
                .DisposedBy(DisposeBag);

            ViewModel.ShouldShowWelcomeBack
                .Subscribe(onWelcomeBackViewVisibilityChanged)
                .DisposedBy(DisposeBag);

            ViewModel.ShouldShowEmptyState
                .Subscribe(onEmptyStateVisibilityChanged)
                .DisposedBy(DisposeBag);

            playButton.Rx().TouchEvents()
                .Subscribe(handlePlayFabEvent)
                .DisposedBy(DisposeBag);

            playButton.Rx().LongPress()
                .Subscribe(_ => playButton.StopAnimation())
                .DisposedBy(DisposeBag);

            setupOnboardingSteps();
        }

        private void updateMainLog(IImmutableList<AnimatableSectionModel<MainLogSectionViewModel, MainLogItemViewModel, IMainLogKey>> items)
        {
            mainLogRecyclerAdapter.UpdateCollection(items);
            NotifyLayoutIsReady();
        }

        private void handlePlayFabEvent(View.TouchEventArgs eventArgs)
        {
            if (eventArgs == null)
            {
                return;
            }

            eventArgs.Handled = false;

            if (eventArgs.Event.Action == MotionEventActions.Up)
            {
                playButton.StopAnimation();
            }
            else
            {
                playButton.TryStartAnimation();
            }
        }

        public void ScrollToStart()
        {
            mainRecyclerView?.SmoothScrollToPosition(0);
        }

        public ISpannable CreateProjectClientTaskLabel(IThreadSafeTimeEntry te)
        {
            if (te == null)
                return new SpannableString(string.Empty);

            var hasProject = te.ProjectId != null;
            var projectIsPlaceholder = te.Project?.IsPlaceholder() ?? false;
            var taskIsPlaceholder = te.Task?.IsPlaceholder() ?? false;
            return Extensions.TimeEntryExtensions.ToProjectTaskClient(
                Context,
                hasProject,
                te.Project?.Name,
                te.Project?.Color,
                te.Task?.Name,
                te.Project?.Client?.Name,
                projectIsPlaceholder,
                taskIsPlaceholder,
                displayPlaceholders: true);
        }

        public void SetFragmentIsVisible(bool isVisible)
        {
            visibilityChangedSubject.OnNext(isVisible);
        }

        private void reload()
        {
            mainLogRecyclerAdapter.NotifyDataSetChanged();
        }

        private void setupRecycler()
        {
            layoutManager = new LinearLayoutManager(Context);
            layoutManager.ItemPrefetchEnabled = true;
            layoutManager.InitialPrefetchItemCount = 4;
            mainRecyclerView.SetLayoutManager(layoutManager);
            mainRecyclerView.SetAdapter(mainLogRecyclerAdapter);
        }

        private void setupItemTouchHelper(MainRecyclerViewTouchCallback callback)
        {
            var itemTouchHelper = new ItemTouchHelper(callback);
            itemTouchHelper.AttachToRecyclerView(mainRecyclerView);
        }

        private void updateSyncingIndicator(SyncProgress state)
        {
            refreshLayout.Refreshing = state == Syncing;
        }

        private void updateRecyclerViewPadding(bool isRunning)
        {
            var newPadding = isRunning ? 104.DpToPixels(Context) : 70.DpToPixels(Context);
            mainRecyclerView.SetPadding(0, 0, 0, newPadding);
        }

        private void onSyncChanged(SyncProgress syncProgress)
        {
            switch (syncProgress)
            {
                case Unknown:
                    return;

                case Failed:
                case OfflineModeDetected:

                    var errorMessage = syncProgress == OfflineModeDetected
                                     ? FoundationResources.Offline
                                     : FoundationResources.SyncFailed;

                    var snackbar = Snackbar.Make(coordinatorLayout, errorMessage, Snackbar.LengthLong)
                        .SetAction(FoundationResources.TapToRetry, onRetryTapped);
                    snackbar.SetDuration(snackbarDuration);
                    snackbar.ShowWithoutBottomInsetPadding();
                    break;
            }

            void onRetryTapped(View view)
            {
                ViewModel.Refresh.Execute();
            }
        }

        private EditTimeEntryInfo editEventInfo(TimeEntryLogItemViewModel item)
        {
            var origin = item.IsTimeEntryGroupHeader
                ? GroupHeader
                : item.BelongsToGroup
                    ? GroupTimeEntry
                    : SingleTimeEntry;

            return new EditTimeEntryInfo(origin, item.RepresentedTimeEntriesIds);
        }

        private void onSwipeActionsChanged(bool enabled)
        {
            touchCallback.AreSwipeActionsEnabled = enabled;
        }

        private void onEmptyStateVisibilityChanged(bool shouldShowEmptyState)
        {
            if (shouldShowEmptyState)
            {
                if (emptyStateView == null)
                {
                    emptyStateView = emptyStateViewStub.Inflate();
                }

                emptyStateView.Visibility = ViewStates.Visible;
            }
            else if (emptyStateView != null)
            {
                emptyStateView.Visibility = ViewStates.Gone;
            }
        }

        private void showUndoDeletion(int? numberOfTimeEntriesPendingDeletion)
        {
            if (!numberOfTimeEntriesPendingDeletion.HasValue)
                return;

            var undoText = numberOfTimeEntriesPendingDeletion > 1
                ? String.Format(FoundationResources.MultipleEntriesDeleted, numberOfTimeEntriesPendingDeletion)
                : FoundationResources.EntryDeleted;

            Snackbar.Make(coordinatorLayout, undoText, snackbarDuration)
                .SetAction(FoundationResources.UndoButtonTitle, view => ViewModel.TimeEntriesViewModel.CancelDeleteTimeEntry.Execute())
                .ShowWithoutBottomInsetPadding();
        }

        private void onWelcomeBackViewVisibilityChanged(bool shouldShowWelcomeBackView)
        {
            if (shouldShowWelcomeBackView)
            {
                if (welcomeBackView == null)
                {
                    welcomeBackView = welcomeBackStub.Inflate();

                    welcomeBackTitle = welcomeBackView.FindViewById<TextView>(Resource.Id.WelcomeBackTitle);
                    welcomeBackSubText = welcomeBackView.FindViewById<TextView>(Resource.Id.WelcomeBackSubText);

                    welcomeBackTitle.Text = FoundationResources.LogEmptyStateTitle;
                    welcomeBackSubText.Text = FoundationResources.LogEmptyStateText;
                }

                welcomeBackView.Visibility = ViewStates.Visible;
            }
            else if (welcomeBackView != null)
            {
                welcomeBackView.Visibility = ViewStates.Gone;
            }
        }
    }
}
