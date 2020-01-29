using Toggl.Core.UI.Views.Settings;
using Toggl.Shared.Extensions;

namespace Toggl.Droid.ViewHolders.Settings
{
    public class ToggleRowWithDescription : ToggleRow
    {
        public string Description { get; }

        public ToggleRowWithDescription(string title, string description, bool value, ViewAction action) : base(title, value, action)
        {
            Description = description;
        }
    }
}
