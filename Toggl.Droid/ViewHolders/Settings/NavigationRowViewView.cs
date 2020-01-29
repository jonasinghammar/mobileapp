using System;
using System.Reactive;
using System.Reactive.Linq;
using Android.Content;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Toggl.Core.UI.Views.Settings;
using Toggl.Droid.Extensions;
using Toggl.Shared.Extensions;

namespace Toggl.Droid.ViewHolders.Settings
{
    public sealed class NavigationRowViewView : SettingsRowView<NavigationRow>
    {
        public static NavigationRowViewView Create(Context context, int viewLayout = Resource.Layout.SettingsNavigationRowView) 
            => new NavigationRowViewView(LayoutInflater.From(context).Inflate(viewLayout, null, false));

        private readonly TextView title;
        private readonly TextView description;
        
        public NavigationRowViewView(IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
        {
        }

        private NavigationRowViewView(View itemView) : base(itemView)
        {
            title = ItemView.FindViewById<TextView>(Resource.Id.Title);
            description = ItemView.FindViewById<TextView>(Resource.Id.Description);
            ItemView.Click += OnItemViewClick;
        }

        protected override void OnRowDataChanged()
        {
            title.Text = RowData.Title;
            
            if (description == null) 
                return;
            
            description.Text = RowData.Detail;
            description.Visibility = (!string.IsNullOrEmpty(RowData.Detail)).ToVisibility();
        }

        private void OnItemViewClick(object sender, EventArgs args)
        {
            RowData?.Action.Inputs.OnNext(Unit.Default);
        }
        
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (!disposing || ItemView == null) return;
            ItemView.Click -= OnItemViewClick;
        }

        public IDisposable Bind(IObservable<string> descriptionProvider, string presetTitle, ViewAction action)
            => descriptionProvider
                .Select(detail => new NavigationRow(presetTitle, detail, action))
                .Subscribe(SetRowData);
    }
}
