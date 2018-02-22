﻿using System;
using System.Collections.Generic;

namespace Toggl.Foundation.Analytics
{
    public abstract class BaseAnalyticsService : IAnalyticsService
    {
        private const string originParameter = "Origin";
        private const string pageParameter = "PageWhenSkipWasClicked";
        private const string viewModelNameParameter = "ViewModelName";

        private const string currentPageEventName = "CurrentPage";
        private const string onboardingSkipEventName = "OnboardingSkip";
        private const string startTimeEntryEventName = "TimeEntryStarted";

        public void TrackOnboardingSkipEvent(string pageName)
        {
            var dict = new Dictionary<string, string> { { pageParameter, pageName } };
            NativeTrackEvent(onboardingSkipEventName, dict);
        }

        public void TrackStartedTimeEntry(TimeEntryStartOrigin origin)
        {
            var dict = new Dictionary<string, string> { { originParameter, origin.ToString() } };
            NativeTrackEvent(startTimeEntryEventName, dict);
        }

        public void TrackCurrentPage(Type viewModelType)
        {
            var dict = new Dictionary<string, string> { { viewModelNameParameter, viewModelType.ToString() } };
            NativeTrackEvent(currentPageEventName, dict);
        }

        public void TrackLoginEvent()
        {
        }

        public void TrackNonFatalException(Exception ex)
        {
        }

        public void TrackSignUpEvent()
        {
        }

        public void TrackSyncError(Exception exception)
        {
            NativeTrackException(exception);
        }

        protected abstract void NativeTrackEvent(string eventName, Dictionary<string, string> parameters);

        protected abstract void NativeTrackException(Exception exception);
    }
}