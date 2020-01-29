using Toggl.Shared.Extensions;

namespace Toggl.Core.UI.Views.Settings
{
    public interface ISettingRow
    {
        string Title { get; }
        ViewAction Action { get; }
    }
}
