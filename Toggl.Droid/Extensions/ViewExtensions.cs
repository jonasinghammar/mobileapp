using System;
using Android.Content;
using Android.Views;
using Android.Views.InputMethods;
using System.Collections.Generic;
using System.Linq;
using AndroidX.Core.Widget;
using Google.Android.Material.AppBar;
using Toggl.Droid.Helper;
using Toggl.Shared;

namespace Toggl.Droid.Extensions
{
    public static class ViewExtensions
    {
        public static IEnumerable<View> GetChildren(this ViewGroup view)
        {
            for (int i = 0; i < view.ChildCount; i++)
                yield return view.GetChildAt(i);
        }

        public static IEnumerable<T> GetChildren<T>(this ViewGroup view)
            => view.GetChildren().OfType<T>();

        public static void SetFocus(this View view)
        {
            var service = (InputMethodManager)view.Context.GetSystemService(Context.InputMethodService);

            view.Post(() =>
            {
                view.RequestFocus();
                service.ShowSoftInput(view, ShowFlags.Forced);
            });
        }

        public static void RemoveFocus(this View view)
        {
            var service = (InputMethodManager)view.Context.GetSystemService(Context.InputMethodService);

            view.Post(() =>
            {
                view.ClearFocus();
                service.HideSoftInputFromWindow(view.WindowToken, 0);
            });
        }

        public static void AttachMaterialScrollBehaviour(this View scrollView, AppBarLayout appBarLayout)
        {
            if (MarshmallowApis.AreNotAvailable)
                return;

            scrollView.SetOnScrollChangeListener(new MaterialScrollBehaviorListener(appBarLayout));

            appBarLayout.Post(() => appBarLayout.Elevation = 0);
        }

        private class MaterialScrollBehaviorListener : Java.Lang.Object, View.IOnScrollChangeListener
        {
            private readonly AppBarLayout appBarLayout;
            private const int defaultToolbarElevationInDPs = 4;

            public MaterialScrollBehaviorListener(AppBarLayout appBarLayout)
            {
                this.appBarLayout = appBarLayout;
            }

            public void OnScrollChange(View v, int scrollX, int scrollY, int oldScrollX, int oldScrollY)
            {
                var targetElevation = v.CanScrollVertically(-1) ?  defaultToolbarElevationInDPs.DpToPixels(appBarLayout.Context) : 0f;
                appBarLayout.Elevation = targetElevation;
            }
        }

        public static Point GetLocationOnScreen(this View view)
        {
            var coordinates = new int[2];
            view.GetLocationOnScreen(coordinates);
            return new Point(coordinates[0], coordinates[1]);
        }

        public static void UpdateMargin(this View view, int? left = null, int? top = null, int? right = null, int? bottom = null)
        {
            if (!(view.LayoutParameters is ViewGroup.MarginLayoutParams)) return;
            ViewGroup.MarginLayoutParams p = (ViewGroup.MarginLayoutParams) view.LayoutParameters;
            p.SetMargins(
                left.GetValueOrDefault(p.LeftMargin),
                top.GetValueOrDefault(p.TopMargin),
                right.GetValueOrDefault(p.RightMargin),
                bottom.GetValueOrDefault(p.BottomMargin)
            );
            view.RequestLayout();
        }

        public static void UpdatePadding(this View view, int? left = null, int? top = null, int? right = null, int? bottom = null)
        {
            view.SetPadding(
                left.GetValueOrDefault(view.PaddingLeft),
                top.GetValueOrDefault(view.PaddingTop),
                right.GetValueOrDefault(view.PaddingRight),
                bottom.GetValueOrDefault(view.PaddingBottom)
                );
        }

        public static void FitBottomPaddingInset(this View view)
        {
            view.DoOnApplyWindowInsets((v, insets, initialSpacing) =>
            {
                view.UpdatePadding(bottom: initialSpacing.Padding.Bottom + insets.SystemWindowInsetBottom);
            });
        }

        public static void FitTopPaddingInset(this View view)
        {
            view.DoOnApplyWindowInsets((v, insets, initialSpacing) =>
            {
                v.UpdatePadding(top: initialSpacing.Padding.Top + insets.SystemWindowInsetTop);
            });
        }

        public static void FitBottomMarginInset(this View view)
        {
            view.DoOnApplyWindowInsets((v, insets, initialSpacing) =>
            {
                view.UpdateMargin(bottom: initialSpacing.Margin.Bottom + insets.SystemWindowInsetBottom);
            });
        }

        public static void FitTopMarginInset(this View view)
        {
            view.DoOnApplyWindowInsets((v, insets, initialSpacing) =>
            {
                view.UpdateMargin(top: initialSpacing.Margin.Top + insets.SystemWindowInsetBottom);
            });
        }

        public static void DoOnApplyWindowInsets(this View view, Action<View, WindowInsets, PaddingAndMargin> insetsHandler)
        {
            // Create a snapshot of the view's padding state
            var initialPaddingAndMargin = view.recordInitialPaddingAndMarginForView();
            // Set the actual Listener which proxies to insetsHandler, also passing in the original padding state
            view.SetOnApplyWindowInsetsListener(new OnApplyWindowInsetsListener(insetsHandler, initialPaddingAndMargin));
            // request some insets
            view.RequestApplyInsetsWhenAttached();
        }

        private class OnApplyWindowInsetsListener : Java.Lang.Object, View.IOnApplyWindowInsetsListener
        {
            private readonly Action<View, WindowInsets, PaddingAndMargin> insetsHandler;
            private readonly PaddingAndMargin paddingAndMargin;

            public OnApplyWindowInsetsListener(Action<View, WindowInsets, PaddingAndMargin> insetsHandler, PaddingAndMargin paddingAndMargin)
            {
                this.insetsHandler = insetsHandler;
                this.paddingAndMargin = paddingAndMargin;
            }

            public WindowInsets OnApplyWindowInsets(View v, WindowInsets insets)
            {
                insetsHandler(v, insets, paddingAndMargin);
                // Always return the insets, so that children can also use them
                return insets;
            }
        }

        private static PaddingAndMargin recordInitialPaddingAndMarginForView(this View view)
            => new PaddingAndMargin(recordInitialPaddingForView(view), recordInitialMarginForView(view));

        private static SpacingDimensions recordInitialPaddingForView(this View view)
            => new SpacingDimensions(view.PaddingLeft, view.PaddingTop, view.PaddingRight, view.PaddingBottom);

        private static SpacingDimensions recordInitialMarginForView(this View view)
        {
            if (!(view.LayoutParameters is ViewGroup.MarginLayoutParams)) {
                return new SpacingDimensions(0,0,0,0);
            }
            ViewGroup.MarginLayoutParams p = (ViewGroup.MarginLayoutParams) view.LayoutParameters;
            return new SpacingDimensions(p.LeftMargin, p.TopMargin, p.RightMargin, p.BottomMargin);
        }

        public struct PaddingAndMargin
        {
            public SpacingDimensions Padding { get; }
            public SpacingDimensions Margin { get; }
            public PaddingAndMargin(SpacingDimensions padding, SpacingDimensions margin)
            {
                Padding = padding;
                Margin = margin;
            }
        }

        public struct SpacingDimensions
        {
            public int Left { get; }
            public int Top { get; }
            public int Right { get; }
            public int Bottom { get; }

            public SpacingDimensions(int left, int top, int right, int bottom)
            {
                Left = left;
                Top = top;
                Right = right;
                Bottom = bottom;
            }
        }

        public static void RequestApplyInsetsWhenAttached(this View view)
        {
            if (view.IsAttachedToWindow)
            {
                // We're already attached, just request as normal
                view.RequestApplyInsets();
            }
            else
            {
                // We're not attached to the hierarchy, add a listener to
                // request when we are
                view.AddOnAttachStateChangeListener(new OnAttachStateChangeListener());
            }
        }

        private class OnAttachStateChangeListener : Java.Lang.Object, View.IOnAttachStateChangeListener
        {
            public void OnViewAttachedToWindow(View attachedView)
            {
                attachedView.RemoveOnAttachStateChangeListener(this);
                attachedView.RequestApplyInsets();
            }

            public void OnViewDetachedFromWindow(View detachedView)
            {
            }
        }
    }
}
