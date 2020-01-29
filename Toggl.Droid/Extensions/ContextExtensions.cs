using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using AndroidX.Core.Content;
using Toggl.Droid.Helper;

namespace Toggl.Droid.Extensions
{
    public static class ContextExtensions
    {
        public static VectorDrawable GetVectorDrawable(this Context context, int resourceId)
            => ContextCompat.GetDrawable(context, resourceId) as VectorDrawable;

        public static Color SafeGetColor(this Context context, int resourceId)
            => new Color(ContextCompat.GetColor(context, resourceId));

        public static PendingIntent SafeGetForegroundService(this Context context, int requestCode, Intent intent, PendingIntentFlags flags)
            => OreoApis.AreAvailable
                ? PendingIntent.GetForegroundService(context, requestCode, intent, flags)
                : PendingIntent.GetService(context, requestCode, intent, flags);

        public static int GetDimen(this Context context, int dimenId)
            => context.Resources.GetDimensionPixelSize(dimenId);

        public static PendingIntent GetOpenAppPendingIntent(this Context context)
        {
            var intent = context.PackageManager.GetLaunchIntentForPackage(context.PackageName);
            intent.SetPackage(null);
            intent.SetFlags(ActivityFlags.NewTask | ActivityFlags.ResetTaskIfNeeded | ActivityFlags.TaskOnHome);
            return PendingIntent.GetActivity(context, 0, intent, PendingIntentFlags.UpdateCurrent);
        }
    }
}
