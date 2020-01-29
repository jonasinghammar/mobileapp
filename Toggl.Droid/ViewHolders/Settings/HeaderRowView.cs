using System;
using Android.Content;
using Android.Runtime;
using Android.Views;
using Android.Widget;

namespace Toggl.Droid.ViewHolders.Settings
{
    public class HeaderRowView : SettingsRowView<HeaderRow>
    {
        public static HeaderRowView Create(Context context)
            => new HeaderRowView(LayoutInflater.From(context).Inflate(Resource.Layout.SettingsHeaderRowView, null));

        private TextView title;
        
        public HeaderRowView(IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
        {
        }

        public HeaderRowView(View itemView) : base(itemView)
        {
            title = ItemView.FindViewById<TextView>(Resource.Id.Title);
        }

        protected override void OnRowDataChanged()
        {
            title.Text = RowData.Title.ToUpper();
        }
    }
}
