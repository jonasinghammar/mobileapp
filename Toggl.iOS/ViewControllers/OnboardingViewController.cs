﻿using CoreAnimation;
using CoreGraphics;
using Foundation;
using System;
using System.Reactive.Linq;
using Toggl.Core.UI.ViewModels;
using Toggl.iOS.Extensions;
using Toggl.iOS.Extensions.Reactive;
using Toggl.Shared;
using Toggl.Shared.Extensions;
using UIKit;

namespace Toggl.iOS.ViewControllers
{
    public sealed partial class OnboardingViewController : ReactiveViewController<OnboardingViewModel>, IUIScrollViewDelegate
    {
        private readonly TrackPage trackPagePlaceholder = TrackPage.Create();
        private readonly MostUsedPage mostUsedPagePlaceholder = MostUsedPage.Create();
        private readonly ReportsPage reportsPagePlaceholder = ReportsPage.Create();
        private readonly CAGradientLayer gradientLayer = new CAGradientLayer();

        public OnboardingViewController(OnboardingViewModel viewModel)
            : base(viewModel, nameof(OnboardingViewController))
        {
        }

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();

            Skip.SetTitle(Resources.Skip.ToUpper(), UIControlState.Normal);
            Next.SetTitle(Resources.Next.ToUpper(), UIControlState.Normal);
            Previous.SetTitle(Resources.Back.ToUpper(), UIControlState.Normal);

            preparePlaceholders();

            if (UIDevice.CurrentDevice.CheckSystemVersion(11, 0))
            {
                var navigationBarHeight = NavigationController.NavigationBar.Frame.Height;
                AdditionalSafeAreaInsets = new UIEdgeInsets(-navigationBarHeight, 0, 0, 0);
            }

            gradientLayer.StartPoint = new CGPoint(0, 0);
            gradientLayer.EndPoint = new CGPoint(0, 1);
            gradientLayer.Locations = new NSNumber[] { 0, 1.0 };
            View.Layer.InsertSublayer(gradientLayer, 0);

            PageControl.Pages = ViewModel.NumberOfPages;
            FirstPageLabel.Text = Resources.OnboardingTrackPageCopy;
            SecondPageLabel.Text = Resources.OnboardingMostUsedPageCopy;
            ThirdPageLabel.Text = Resources.OnboardingReportsPageCopy;

            Skip.Rx()
                .BindAction(ViewModel.SkipOnboarding)
                .DisposedBy(DisposeBag);

            Next.Rx()
                .BindAction(ViewModel.GoToNextPage)
                .DisposedBy(DisposeBag);

            Previous.Rx()
                .BindAction(ViewModel.GoToPreviousPage)
                .DisposedBy(DisposeBag);

            ScrollView.Rx()
                .DecelerationEnded()
                .Select(_ => (int)(ScrollView.ContentOffset.X / ScrollView.Frame.Width))
                .Subscribe(newPage => ViewModel?.ChangePage(newPage))
                .DisposedBy(DisposeBag);

            ViewModel.BackgroundColor
                .Select(color => color.ToNativeColor())
                .Subscribe(View.Rx().AnimatedBackgroundColor())
                .DisposedBy(DisposeBag);

            ViewModel.BorderColor
                .Select(color => color.ToNativeColor())
                .Subscribe(PhoneFrame.Rx().AnimatedBackgroundColor())
                .DisposedBy(DisposeBag);

            ViewModel.CurrentPage
                .Subscribe(setBackgroundForPage)
                .DisposedBy(DisposeBag);

            ViewModel.IsLastPage
                .Invert()
                .Subscribe(Skip.Rx().IsVisible())
                .DisposedBy(DisposeBag);

            ViewModel.IsFirstPage
                .Invert()
                .Subscribe(Previous.Rx().IsVisible())
                .DisposedBy(DisposeBag);

            ViewModel.IsTrackPage
                .Subscribe(trackPagePlaceholder.Rx().IsVisible())
                .DisposedBy(DisposeBag);

            ViewModel.IsReportPage
                .Subscribe(reportsPagePlaceholder.Rx().IsVisible())
                .DisposedBy(DisposeBag);

            ViewModel.IsSummaryPage
                .Subscribe(mostUsedPagePlaceholder.Rx().IsVisible())
                .DisposedBy(DisposeBag);

            ViewModel.CurrentPage
                .Subscribe(p => PageControl.CurrentPage = p)
                .DisposedBy(DisposeBag);

            ViewModel.CurrentPage
                .Subscribe(p =>
                {
                    var scrollPoint = new CGPoint(ScrollView.Frame.Size.Width * p, 0);
                    ScrollView.SetContentOffset(scrollPoint, true);
                })
                .DisposedBy(DisposeBag);
        }

        public override void ViewWillAppear(bool animated)
        {
            base.ViewWillAppear(animated);
            NavigationController.NavigationBar.UserInteractionEnabled = false;
            NavigationController.NavigationBarHidden = true;
        }

        public override bool PrefersStatusBarHidden()
            => true;

        private void preparePlaceholders()
        {
            PhoneContents.AddSubview(trackPagePlaceholder);
            PhoneContents.AddSubview(mostUsedPagePlaceholder);
            PhoneContents.AddSubview(reportsPagePlaceholder);
        }

        public override void ViewDidLayoutSubviews()
        {
            base.ViewDidLayoutSubviews();

            if (trackPagePlaceholder != null)
                trackPagePlaceholder.Frame = PhoneContents.Bounds;
            if (mostUsedPagePlaceholder != null)
                mostUsedPagePlaceholder.Frame = PhoneContents.Bounds;
            if (reportsPagePlaceholder != null)
                reportsPagePlaceholder.Frame = PhoneContents.Bounds;

            gradientLayer.Frame = View.Bounds;
        }

        public override void ViewWillTransitionToSize(CGSize toSize, IUIViewControllerTransitionCoordinator coordinator)
        {
            base.ViewWillTransitionToSize(toSize, coordinator);

            if (PageControl == null)
                return;

            var scrollPoint = new CGPoint(toSize.Width * PageControl.CurrentPage, 0);
            ScrollView.SetContentOffset(scrollPoint, true);
        }

        private void setBackgroundForPage(int page)
        {
            UIColor startColor;
            var endColor = UIColor.FromRGBA(1.000f, 1.000f, 1.000f, 0.000f);

            switch (page)
            {
                case OnboardingViewModel.TrackPage:
                    startColor = UIColor.FromRGBA(0.108f, 0.681f, 0.963f, 1.000f);
                    break;
                case OnboardingViewModel.MostUsedPage:
                    startColor = UIColor.FromRGBA(0.735f, 0.403f, 0.945f, 1.000f);
                    break;
                case OnboardingViewModel.ReportsPage:
                    startColor = UIColor.FromRGBA(0.943f, 0.764f, 0.252f, 1.000f);
                    break;
                default:
                    startColor = UIColor.FromRGBA(0.108f, 0.681f, 0.963f, 1.000f);
                    break;
            }

            gradientLayer.Colors = new[]
            {
                startColor.CGColor,
                endColor.CGColor
            };
        }
    }
}
