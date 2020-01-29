using Toggl.Shared.Extensions;

namespace Toggl.Core.UI.Views.Settings
{
    public class CustomRow<TCustomValue> : ISettingRow
    {
        public string Title { get; }
        public ViewAction Action { get; }
        public TCustomValue CustomValue { get; }

        public CustomRow(TCustomValue customValue, ViewAction action = null)
        {
            CustomValue = customValue;
            Action = action;
        }
    }
}
