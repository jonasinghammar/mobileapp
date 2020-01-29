using Toggl.Shared.Extensions;

namespace Toggl.Core.UI.Views.Settings
{
    public class AnnotationRow : ISettingRow
    {
        public string Title { get; }
        public ViewAction Action { get; }

        public AnnotationRow(string text)
        {
            Title = text;
        }
    }
}
