using Toggl.Core.UI.Views.Settings;
using Toggl.Shared.Extensions;

namespace Toggl.Droid.ViewHolders.Settings
{
    public class HeaderRow : ISettingRow
    {
        public string Title { get; }
        public ViewAction Action { get; }

        public HeaderRow(string title)
        {
            Title = title;
        }
    }
}
