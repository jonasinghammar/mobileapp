using System;
using Android.Content.Res;
using Android.Runtime;
using Android.Views;
using AndroidX.Core.View;
using AndroidX.RecyclerView.Widget;
using Toggl.Core.UI.Views.Settings;
using Toggl.Droid.Activities;
using Toggl.Droid.Extensions.Reactive;

namespace Toggl.Droid.ViewHolders.Settings
{
    public abstract class SettingsRowView<T> : RecyclerView.ViewHolder
    where T : ISettingRow
    {
        protected T RowData { get; private set; }

        protected SettingsRowView(IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
        {
        }

        protected SettingsRowView(View itemView) : base(itemView)
        {
            ItemView.Tag = this;
        }

        public void SetRowData(T newRowData)
        {
            RowData = newRowData;
            OnRowDataChanged();
        }

        protected abstract void OnRowDataChanged();
    }
}
