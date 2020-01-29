using Toggl.Shared.Extensions;

namespace Toggl.Core.UI.Views.Settings
{
    public class ToggleRow : ISettingRow
    {
        public string Title { get; }
        public ViewAction Action { get; }

        public bool Value { get; }

        public ToggleRow(string title, bool value, ViewAction action)
        {
            Title = title;
            Value = value;
            Action = action;
        }
    }
}