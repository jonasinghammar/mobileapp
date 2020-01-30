using System;

namespace Toggl.Droid.Helper
{
    public interface ITabLayoutReadyListener
    {
        void OnLayoutReady(Type tabType);
    }
}
