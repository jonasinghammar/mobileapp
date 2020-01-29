using Android.Content;
using Android.Widget;
using Toggl.Shared;

namespace Toggl.Droid.Widgets
{
    public sealed class SuggestionsWidgetNotLoggedInFormFactor : WidgetNotLoggedInFormFactor, ISuggestionsWidgetFormFactor
    {
        public bool ContainsListView { get; } = false;
        protected override string LabelText { get; } = Resources.LoginToShowSuggestions;

        public SuggestionsWidgetNotLoggedInFormFactor(int columnCount)
            : base(columnCount) { }

        public RemoteViews Setup(Context context, int widgetId)
            => Setup(context);
    }
}
