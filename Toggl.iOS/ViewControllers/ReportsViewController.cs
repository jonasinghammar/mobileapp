using System;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using CoreGraphics;
using Foundation;
using Toggl.Core.Analytics;
using Toggl.Core.UI.Extensions;
using Toggl.Core.UI.ViewModels.Reports;
using Toggl.iOS.Extensions;
using Toggl.iOS.Extensions.Reactive;
using Toggl.iOS.Helper;
using Toggl.iOS.Presentation;
using Toggl.iOS.Views.Reports;
using Toggl.iOS.ViewSources;
using Toggl.Shared;
using Toggl.Shared.Extensions;
using UIKit;
using static Toggl.Core.UI.ViewModels.DateRangePicker.DateRangePickerViewModel;

namespace Toggl.iOS.ViewControllers
{
    public partial class ReportsViewController : ReactiveViewController<ReportsViewModel>, IScrollableToTop
    {
        private UIButton titleButton;

        private ReportsCollectionViewRegularLayout regularLayout;
        private ReportsCollectionViewCompactLayout compactLayout;

        private Subject<Unit> viewDidAppearSubject = new Subject<Unit>();

        public ReportsViewController(ReportsViewModel viewModel) : base(viewModel, nameof(ReportsViewController))
        {
        }

        public override void TraitCollectionDidChange(UITraitCollection previousTraitCollection)
        {
            if (TraitCollection.HorizontalSizeClass == UIUserInterfaceSizeClass.Regular)
                CollectionView.SetCollectionViewLayout(regularLayout, false);
            else
                CollectionView.SetCollectionViewLayout(compactLayout, false);
            CollectionView.CollectionViewLayout.InvalidateLayout();
            base.TraitCollectionDidChange(previousTraitCollection);
        }

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();
            prepareViews();

            ViewModel.CurrentWorkspaceName
                .Subscribe(WorkspaceLabel.Rx().Text())
                .DisposedBy(DisposeBag);

            ViewModel.FormattedTimeRange
                .Subscribe(titleButton.Rx().TitleAdaptive())
                .DisposedBy(DisposeBag);

            ViewModel.HasMultipleWorkspaces
                .Subscribe(WorkspaceButton.Rx().IsVisible())
                .DisposedBy(DisposeBag);

            titleButton.Rx().Tap()
                .Subscribe(ViewModel.SelectTimeRange.Inputs)
                .DisposedBy(DisposeBag);

            WorkspaceButton.Rx()
                .BindAction(ViewModel.SelectWorkspace)
                .DisposedBy(DisposeBag);

            var source = new ReportsCollectionViewSource(CollectionView);
            CollectionView.Source = source;
            regularLayout = new ReportsCollectionViewRegularLayout(source);
            compactLayout = new ReportsCollectionViewCompactLayout(source);
            if (TraitCollection.HorizontalSizeClass == UIUserInterfaceSizeClass.Regular)
                CollectionView.SetCollectionViewLayout(regularLayout, false);
            else
                CollectionView.SetCollectionViewLayout(compactLayout, false);

            ViewModel.Elements
                .Subscribe(source.SetNewElements)
                .DisposedBy(DisposeBag);

            ViewModel.HasMultipleWorkspaces
                .Subscribe(compactLayout.SetHasMultipleWorkspaces)
                .DisposedBy(DisposeBag);

            var workspaceObservable = IosDependencyContainer.Instance.InteractorFactory.GetDefaultWorkspace().Execute()
                .Merge(ViewModel.SelectWorkspace.Elements)
                .WhereNotNull();

            var dateRangeSelectionResultObservable = ViewModel.SelectTimeRange.Elements
                .StartWith(new DateRangeSelectionResult(
                    new DateRange(DateTime.Now.AddDays(-7), DateTime.Now),
                    DateRangeSelectionSource.ShortcutThisWeek)
                )
                .WhereNotNull();

            //Handoff
            viewDidAppearSubject.AsObservable()
                .CombineLatest(
                    workspaceObservable,
                    dateRangeSelectionResultObservable,
                    (_, ws, tr) => createUserActivity(ws.Id, tr.SelectedRange.Value.Beginning, tr.SelectedRange.Value.End))
                .Subscribe(updateUserActivity);

            NSUserActivity createUserActivity(long workspaceId, DateTimeOffset start, DateTimeOffset end)
            {
                var userActivity = new NSUserActivity(Handoff.Action.Reports);
                userActivity.EligibleForHandoff = true;
                userActivity.WebPageUrl = Handoff.Url.Reports(workspaceId, start, end);
                return userActivity;
            }

            void updateUserActivity(NSUserActivity userActivity)
            {
                UserActivity = userActivity;
                UserActivity.BecomeCurrent();
            }
        }

        public override void ViewDidAppear(bool animated)
        {
            base.ViewDidAppear(animated);

            IosDependencyContainer.Instance.IntentDonationService.DonateShowReport();

            viewDidAppearSubject.OnNext(Unit.Default);
        }

        public void ScrollToTop() { }

        private void prepareViews()
        {
            var separator = NavigationController.NavigationBar.InsertSeparator();
            separator.BackgroundColor = ColorAssets.OpaqueSeparator;

            // Date range button
            NavigationItem.TitleView = titleButton = new UIButton(new CGRect(0, 0, 200, 40));
            titleButton.Font = UIFont.SystemFontOfSize(14, UIFontWeight.Medium);
            titleButton.SetTitleColor(ColorAssets.Text, UIControlState.Normal);

            // Workspace button settings
            WorkspaceFadeView.FadeWidth = 32;
            WorkspaceButton.Layer.ShadowColor = UIColor.Black.CGColor;
            WorkspaceButton.Layer.ShadowRadius = 10;
            WorkspaceButton.Layer.ShadowOffset = new CGSize(0, 2);
            WorkspaceButton.Layer.ShadowOpacity = 0.10f;
            WorkspaceButton.Layer.BorderColor = ColorAssets.Separator.CGColor;
            WorkspaceButton.Layer.BorderWidth = 0.35f;

            View.BackgroundColor = ColorAssets.TableBackground;
        }
    }
}

