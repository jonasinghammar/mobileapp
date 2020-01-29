using System;
using Android.Content;
using Android.Runtime;
using Android.Views;
using Toggl.Core.UI.Views.Settings;
using Toggl.Shared.Extensions;

namespace Toggl.Droid.ViewHolders.Settings
{
    public class DividerRowView : SettingsRowView<ISettingRow>
    {
        public static DividerRowView Create(Context context)
            => new DividerRowView(LayoutInflater.From(context).Inflate(Resource.Layout.SettingsDividerRowView, null));

        public DividerRowView(IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
        {
        }

        public DividerRowView(View itemView) : base(itemView)
        {
        }

        protected override void OnRowDataChanged()
        {
        }
    }
}
