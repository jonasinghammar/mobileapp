using System;
using System.Collections.Generic;
using System.Reactive;
using Android.Content;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Toggl.Core.Sync;
using Toggl.Core.UI.Views.Settings;
using Toggl.Shared.Extensions;

namespace Toggl.Droid.ViewHolders.Settings
{
    public sealed class LogoutRowViewView : SettingsRowView<CustomRow<PresentableSyncStatus>>
    {
        public static LogoutRowViewView Create(Context context) 
            => new LogoutRowViewView(LayoutInflater.From(context).Inflate(Resource.Layout.SettingsLogoutRowView, null, false));

        private Dictionary<PresentableSyncStatus, View> syncStateViews = new Dictionary<PresentableSyncStatus, View>();
        private readonly TextView txtStateInProgress;

        public LogoutRowViewView(IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
        {
        }

        private LogoutRowViewView(View itemView) : base(itemView)
        {
            var syncStateSyncedHolder = ItemView.FindViewById(Resource.Id.SyncStateSyncedHolder);
            var syncStateInProgressHolder = ItemView.FindViewById(Resource.Id.SyncStateInProgressHolder);
            var txtStateSynced = ItemView.FindViewById<TextView>(Resource.Id.TxtStateSynced);
            var signOutTextView = ItemView.FindViewById<TextView>(Resource.Id.TxtSettingsLogout);
            txtStateInProgress = ItemView.FindViewById<TextView>(Resource.Id.TxtStateInProgress);
            syncStateViews.Add(PresentableSyncStatus.Synced, syncStateSyncedHolder);
            syncStateViews.Add(PresentableSyncStatus.Syncing, syncStateInProgressHolder);
            syncStateViews.Add(PresentableSyncStatus.LoggingOut, syncStateInProgressHolder);
            txtStateInProgress.Text = Shared.Resources.Syncing;
            txtStateSynced.Text = Shared.Resources.SyncCompleted;
            signOutTextView.Text = Shared.Resources.SignOutOfToggl;
            ItemView.Click += OnItemViewClick;
        }

        protected override void OnRowDataChanged()
        {
            var status = RowData.CustomValue;
            syncStateViews.Values.ForEach(view => view.Visibility = ViewStates.Gone);
            
            txtStateInProgress.Text = status == PresentableSyncStatus.Syncing 
                ? Shared.Resources.Syncing 
                : Shared.Resources.LoggingOutSecurely;
            
            var visibleView = syncStateViews[status];
            visibleView.Visibility = ViewStates.Visible;
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
    }
}
