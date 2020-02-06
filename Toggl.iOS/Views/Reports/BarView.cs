using System;
using Toggl.Core.UI.Helper;
using Toggl.iOS.Extensions;
using UIKit;

namespace Toggl.iOS.Views.Reports
{
    internal sealed class BarView : UIView
    {

        private readonly double filledValue;

        private readonly double totalValue;

        private readonly UIView billableView;

        private readonly UIView nonBillableView;

        private bool shouldSetupConstraints = true;

        public BarView(double filledValue,
                       double totalValue,
                       UIColor filledColor = null,
                       UIColor totalColor = null)
        {
            this.filledValue = double.IsFinite(filledValue) ? filledValue : 0;
            this.totalValue = double.IsFinite(totalValue) ? totalValue : 0;

            filledColor ??= ColorAssets.ReportsBarChartFilled;
            totalColor ??= ColorAssets.ReportsBarChartTotal;

            nonBillableView = new BarSegmentView(filledColor);
            billableView = new BarSegmentView(totalColor);

            AddSubview(nonBillableView);
            AddSubview(billableView);
        }

        public override void UpdateConstraints()
        {
            base.UpdateConstraints();

            if (!shouldSetupConstraints) return;

            billableView.BottomAnchor.ConstraintEqualTo(BottomAnchor).Active = true;
            billableView.LeftAnchor.ConstraintEqualTo(LeftAnchor).Active = true;
            billableView.RightAnchor.ConstraintEqualTo(RightAnchor).Active = true;

            nonBillableView.BottomAnchor.ConstraintEqualTo(BottomAnchor).Active = true;
            nonBillableView.LeftAnchor.ConstraintEqualTo(LeftAnchor).Active = true;
            nonBillableView.RightAnchor.ConstraintEqualTo(RightAnchor).Active = true;

            billableView.HeightAnchor.ConstraintEqualTo(HeightAnchor, (nfloat)filledValue).Active = true;
            nonBillableView.HeightAnchor.ConstraintEqualTo(HeightAnchor, (nfloat)totalValue).Active = true;

            shouldSetupConstraints = false;
        }
    }
}
