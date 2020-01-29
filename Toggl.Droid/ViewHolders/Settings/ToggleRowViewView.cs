using System;
using System.Reactive;
using System.Reactive.Linq;
using Android.Content;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Toggl.Core.UI.Views.Settings;
using Toggl.Shared.Extensions;

namespace Toggl.Droid.ViewHolders.Settings
{
    public sealed class ToggleRowViewView : SettingsRowView<ToggleRow>
    {
        public static ToggleRowViewView Create(Context context) 
            => new ToggleRowViewView(LayoutInflater.From(context).Inflate(Resource.Layout.SettingsToggleRowView, null, false));

        private readonly TextView title;
        private readonly Switch switchView;
        
        public ToggleRowViewView(IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
        {
        }

        private ToggleRowViewView(View itemView) : base(itemView)
        {
            title = ItemView.FindViewById<TextView>(Resource.Id.Title);
            switchView = ItemView.FindViewById<Switch>(Resource.Id.Switch);
            ItemView.Click += OnItemViewClick;
            switchView.Click += OnItemViewClick;
        }

        protected override void OnRowDataChanged()
        {
            title.Text = RowData.Title;
            if (RowData.Value != switchView.Checked)
            {
                switchView.Checked = RowData.Value;
            }
        }

        public IDisposable Bind(IObservable<bool> toggleValueProvider, string presetTitle, ViewAction action)
            => toggleValueProvider
                .Select(value => new ToggleRow(presetTitle, value, action))
                .Subscribe(SetRowData);

        private void OnItemViewClick(object sender, EventArgs args)
        {
            RowData?.Action.Inputs.OnNext(Unit.Default);
        }
        
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (!disposing || ItemView == null) return;
            ItemView.Click -= OnItemViewClick;
            switchView.Click -= OnItemViewClick;
        }
    }
}
