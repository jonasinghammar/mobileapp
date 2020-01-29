using Toggl.Shared.Extensions;

namespace Toggl.Core.UI.Views.Settings
{
    public class ButtonRow : ISettingRow
    {
        public string Title { get; }
        public ViewAction Action { get; }

        public ButtonRow(string title, ViewAction action)
        {
            Title = title;
            Action = action;
        }
    }
}
