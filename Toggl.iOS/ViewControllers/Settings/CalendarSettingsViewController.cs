using System;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Toggl.Core.UI.ViewModels.Settings;
using Toggl.iOS.Extensions;
using Toggl.iOS.Extensions.Reactive;
using Toggl.iOS.ViewSources;
using Toggl.Shared.Extensions;
using Colors = Toggl.Core.UI.Helper.Colors;
using Toggl.Shared;
using UIKit;

namespace Toggl.iOS.ViewControllers.Settings
{
    public sealed partial class CalendarSettingsViewController : ReactiveViewController<CalendarSettingsViewModel>
    {
        private const int tableViewHeaderHeight = 106;

        public CalendarSettingsViewController(CalendarSettingsViewModel viewModel)
            : base(viewModel, nameof(CalendarSettingsViewController))
        {
        }

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();

            NavigationItem.Title = Resources.CalendarSettingsTitle;

            var header = CalendarSettingsTableViewHeader.Create();
            UserCalendarsTableView.TableHeaderView = header;
            UserCalendarsTableView.AllowsSelection = false;
            header.TranslatesAutoresizingMaskIntoConstraints = false;
            header.HeightAnchor.ConstraintEqualTo(tableViewHeaderHeight).Active = true;
            header.WidthAnchor.ConstraintEqualTo(UserCalendarsTableView.WidthAnchor).Active = true;

            ViewModel.CalendarIntegrationEnabled
                .Subscribe(header.SetCalendarIntegrationStatus)
                .DisposedBy(DisposeBag);

            var source = new SelectUserCalendarsTableViewSource(UserCalendarsTableView, ViewModel.SelectCalendar);
            UserCalendarsTableView.Source = source;

            ViewModel.Calendars
                .Subscribe(UserCalendarsTableView.Rx().ReloadSections(source))
                .DisposedBy(DisposeBag);

            header.LinkCalendarsSwitchTapped
                .Subscribe(ViewModel.ToggleCalendarIntegration.Execute)
                .DisposedBy(DisposeBag);

            source.Rx().ModelSelected()
                .Subscribe(ViewModel.SelectCalendar.Inputs)
                .DisposedBy(DisposeBag);

            IosDependencyContainer.Instance.BackgroundService
                .AppResumedFromBackground
                .Select(_ => IosDependencyContainer.Instance.UserPreferences.CalendarIntegrationEnabled())
                .Subscribe(header.SetCalendarIntegrationStatus)
                .DisposedBy(DisposeBag);

            if (ViewModel is IndependentCalendarSettingsViewModel)
            {
                NavigationItem.RightBarButtonItem = ReactiveNavigationController.CreateSystemItem(
                    Resources.Done, UIBarButtonItemStyle.Done, Close);
            }
        }

        public override void ViewDidDisappear(bool animated)
        {
            ViewModel.Save.Execute();
            base.ViewDidDisappear(animated);
        }

        public override Task<bool> DismissFromNavigationController()
            => Task.FromResult(true);
    }
}
