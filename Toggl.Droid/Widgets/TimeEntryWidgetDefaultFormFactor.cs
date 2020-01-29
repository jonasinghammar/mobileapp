using Android.Content;
using Android.OS;
using Android.Widget;
using System;
using Toggl.Droid.Extensions;
using Color = Android.Graphics.Color;
using Toggl.Shared;

namespace Toggl.Droid.Widgets
{
    public sealed class TimeEntryWidgetDefaultFormFactor : TimeEntryWidgetStartStopButtonFormFactor
    {
        public override RemoteViews Setup(Context context, TimeEntryWidgetInfo widgetInfo)
        {
            var view = new RemoteViews(context.PackageName, Resource.Layout.TimeEntryWidget);

            SetupActionsForStartAndStopButtons(context, view);
            view.SetTextViewText(Resource.Id.NoRunningTimeEntryLabel, Resources.NoRunningTimeEntry);
            view.SetOnClickPendingIntent(Resource.Id.RootLayout, context.GetOpenAppPendingIntent());

            var timeEntryIsRunning = widgetInfo.IsRunning;
            var timeEntryIsStopped = !widgetInfo.IsRunning;

            view.SetViewVisibility(Resource.Id.StartButton, timeEntryIsStopped.ToVisibility());
            view.SetViewVisibility(Resource.Id.NoRunningTimeEntryLabel, timeEntryIsStopped.ToVisibility());

            view.SetViewVisibility(Resource.Id.StopButton, timeEntryIsRunning.ToVisibility());
            view.SetViewVisibility(Resource.Id.TimeEntryInfoContainer, timeEntryIsRunning.ToVisibility());

            if (timeEntryIsStopped)
                return view;

            var duration = (DateTimeOffset.Now - widgetInfo.StartTime).TotalMilliseconds;
            view.SetChronometer(Resource.Id.DurationTextView, SystemClock.ElapsedRealtime() - (long)duration, "%s", true);

            if (string.IsNullOrEmpty(widgetInfo.Description))
            {
                view.SetTextViewText(Resource.Id.DescriptionTextView, Resources.NoDescription);
                view.SetTextColor(Resource.Id.DescriptionTextView, context.SafeGetColor(Resource.Color.secondaryText));
            }
            else
            {
                view.SetTextViewText(Resource.Id.DescriptionTextView, widgetInfo.Description);
                view.SetTextColor(Resource.Id.DescriptionTextView, context.SafeGetColor(Resource.Color.primaryText));
            }

            view.SetViewVisibility(Resource.Id.DotView, widgetInfo.HasProject.ToVisibility());
            view.SetViewVisibility(Resource.Id.ProjectTextView, widgetInfo.HasProject.ToVisibility());
            if (widgetInfo.HasProject)
            {
                // Project
                var projectColor = widgetInfo.ProjectColor != null
                    ? Color.ParseColor(widgetInfo.ProjectColor)
                    : Color.Black;
                view.SetInt(Resource.Id.DotView, "setBackgroundColor", projectColor);
                view.SetTextViewText(Resource.Id.ProjectTextView, widgetInfo.ProjectName ?? "");
                view.SetTextColor(Resource.Id.ProjectTextView, projectColor);

                // Client
                view.SetViewVisibility(Resource.Id.ClientTextView, widgetInfo.HasClient.ToVisibility());
                if (widgetInfo.HasClient)
                {
                    view.SetTextViewText(Resource.Id.ClientTextView, widgetInfo.ClientName);
                }
            }

            return view;
        }
    }
}
