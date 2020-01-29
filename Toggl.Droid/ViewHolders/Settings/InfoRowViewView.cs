using System;
using System.Reactive.Linq;
using Android.Content;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Toggl.Core.UI.Views.Settings;

namespace Toggl.Droid.ViewHolders.Settings
{
    public class InfoRowViewView : SettingsRowView<InfoRow>
    {
        public static InfoRowViewView Create(Context context)
            => new InfoRowViewView(LayoutInflater.From(context).Inflate(Resource.Layout.SettingsInfoRowView, null));

        private TextView title;
        private TextView description;
        
        public InfoRowViewView(IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
        {
        }

        private InfoRowViewView(View itemView) : base(itemView)
        {
            title = ItemView.FindViewById<TextView>(Resource.Id.Title);
            description = ItemView.FindViewById<TextView>(Resource.Id.Description);
        }

        protected override void OnRowDataChanged()
        {
            title.Text = RowData.Title;
            description.Text = RowData.Detail;
        }

        public IDisposable Bind(IObservable<string> descriptionProvider, string presetTitle)
            => descriptionProvider
                .Select(detail => new InfoRow(presetTitle, detail))
                .Subscribe(SetRowData);
    }
}
