using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;
using Toggl.Core.Analytics;
using Toggl.Core.DataSources;
using Toggl.Core.Interactors;
using Toggl.Core.Models;
using Toggl.Core.Models.Interfaces;
using Toggl.Core.Services;
using Toggl.Core.UI.Extensions;
using Toggl.Core.UI.Helper;
using Toggl.Core.UI.Navigation;
using Toggl.Core.UI.Services;
using Toggl.Core.UI.ViewModels.DateRangePicker;
using Toggl.Core.UI.Views;
using Toggl.Shared;
using Toggl.Shared.Extensions;
using DateRangeSelectionResult = Toggl.Core.UI.ViewModels.DateRangePicker.DateRangePickerViewModel.DateRangeSelectionResult;

namespace Toggl.Core.UI.ViewModels.Reports
{
    public sealed class ReportsViewModel : ViewModel
    {
        public const string DownArrowCharacter = "▾";
        private long? selectedWorkspaceId;
        private Either<DateRangePeriod, DateRange> selection;

        private readonly IInteractorFactory interactorFactory;
        private readonly IDateRangeShortcutsService dateRangeShortcutsService;
        private readonly ITimeService timeService;
        private readonly ISchedulerProvider schedulerProvider;
        private readonly ITogglDataSource dataSource;
        private readonly IAnalyticsService analyticsService;

        public IObservable<IImmutableList<IReportElement>> Elements { get; set; }
        public IObservable<bool> HasMultipleWorkspaces { get; set; }
        public IObservable<string> CurrentWorkspaceName { get; private set; }

        public IObservable<string> FormattedTimeRange { get; set; }

        public OutputAction<IThreadSafeWorkspace> SelectWorkspace { get; private set; }
        public OutputAction<DateRangeSelectionResult> SelectTimeRange { get; private set; }
        public InputAction<DateRangeSelectionResult> SetTimeRange { get; private set; }

        public ReportsViewModel(
            ITogglDataSource dataSource,
            INavigationService navigationService,
            IInteractorFactory interactorFactory,
            ISchedulerProvider schedulerProvider,
            IRxActionFactory rxActionFactory,
            IAnalyticsService analyticsService,
            ITimeService timeService,
            IDateRangeShortcutsService dateRangeShortcutsService)
            : base(navigationService)
        {
            Ensure.Argument.IsNotNull(dataSource, nameof(dataSource));
            Ensure.Argument.IsNotNull(navigationService, nameof(navigationService));
            Ensure.Argument.IsNotNull(interactorFactory, nameof(interactorFactory));
            Ensure.Argument.IsNotNull(rxActionFactory, nameof(rxActionFactory));
            Ensure.Argument.IsNotNull(schedulerProvider, nameof(schedulerProvider));
            Ensure.Argument.IsNotNull(analyticsService, nameof(analyticsService));
            Ensure.Argument.IsNotNull(timeService, nameof(timeService));
            Ensure.Argument.IsNotNull(dateRangeShortcutsService, nameof(dateRangeShortcutsService));

            this.dataSource = dataSource;
            this.interactorFactory = interactorFactory;
            this.schedulerProvider = schedulerProvider;
            this.timeService = timeService;
            this.analyticsService = analyticsService;
            this.dateRangeShortcutsService = dateRangeShortcutsService;

            HasMultipleWorkspaces = interactorFactory.ObserveAllWorkspaces().Execute()
                .Select(workspaces => workspaces.Where(w => !w.IsInaccessible))
                .Select(w => w.Count() > 1)
                .DistinctUntilChanged()
                .AsDriver(schedulerProvider);

            SelectWorkspace = rxActionFactory.FromAsync(selectWorkspace);
            SelectTimeRange = rxActionFactory.FromAsync(selectTimeRange);
            SetTimeRange = rxActionFactory.FromAction<DateRangeSelectionResult>(setTimeRange);
        }

        public override async Task Initialize()
        {
            var workspaceSelector = interactorFactory.GetDefaultWorkspace().Execute()
                .Concat(SelectWorkspace.Elements.WhereNotNull());

            var beginningOfWeek = (await interactorFactory.GetCurrentUser().Execute()).BeginningOfWeek;

            var initialSelection = new DateRangeSelectionResult(
                    dateRangeShortcutsService.GetShortcutFrom(DateRangePeriod.ThisWeek).DateRange,
                    DateRangeSelectionSource.Initial);

            var timeRangeSelector = SelectTimeRange.Elements
                .Merge(SetTimeRange.Inputs)
                .StartWith(initialSelection)
                .WhereNotNull();

            CurrentWorkspaceName = workspaceSelector
                .Select(ws => ws.Name)
                .StartWith("")
                .DistinctUntilChanged()
                .AsDriver("", schedulerProvider);

            Elements = Observable.CombineLatest(workspaceSelector, timeRangeSelector, ReportProcessData.Create)
                .SelectMany(reportElements)
                .AsDriver(ImmutableList<IReportElement>.Empty, schedulerProvider);

            var dateFormatObservable = dataSource.Preferences
                .Current
                .Select(preferences => preferences.DateFormat);

            FormattedTimeRange = timeRangeSelector
                .CombineLatest(dateFormatObservable, resultSelector: formattedTimeRange)
                .DistinctUntilChanged()
                .Select(dateRange => $"{dateRange} {DownArrowCharacter}")
                .AsDriver("", schedulerProvider);

            selectedWorkspaceId = (await interactorFactory.GetDefaultWorkspace().Execute())?.Id;

            selection = Either<DateRangePeriod, DateRange>.WithLeft(DateRangePeriod.ThisWeek);
        }

        private async Task<IThreadSafeWorkspace> selectWorkspace()
        {
            var allWorkspaces = await interactorFactory.GetAllWorkspaces().Execute();

            var accessibleWorkspaces = allWorkspaces
                .Where(ws => !ws.IsInaccessible)
                .Select(ws => new SelectOption<IThreadSafeWorkspace>(ws, ws.Name))
                .ToImmutableList();

            var currentWorkspaceIndex = accessibleWorkspaces.IndexOf(w => w.Item.Id == selectedWorkspaceId);

            var workspace = await View.Select(Resources.SelectWorkspace, accessibleWorkspaces, currentWorkspaceIndex);

            if (workspace == null || workspace.Id == selectedWorkspaceId)
                return null;

            selectedWorkspaceId = workspace.Id;

            return workspace;
        }

        private async Task<DateRangeSelectionResult> selectTimeRange()
        {
            var dateRangeSelection = await Navigate<DateRangePickerViewModel, Either<DateRangePeriod, DateRange>, DateRangeSelectionResult>(selection);
            if (dateRangeSelection?.SelectedRange == null)
                return null;

            selection = Either<DateRangePeriod, DateRange>.WithRight(dateRangeSelection.SelectedRange.Value);

            return dateRangeSelection;
        }

        private void setTimeRange(DateRangeSelectionResult result)
        {
            selection = Either<DateRangePeriod, DateRange>.WithRight(result.SelectedRange.Value);
        }

        private ImmutableList<IReportElement> createLoadingStateReportElements()
            => elements(
                ReportSummaryElement.LoadingState,
                ReportBarChartElement.LoadingState,
                ReportDonutChartDonutElement.LoadingState);

        private IObservable<ImmutableList<IReportElement>> reportElements(ReportProcessData processData)
            => reportElementsProcess(processData)
            .ToObservable()
            .StartWith(createLoadingStateReportElements());

        private string formattedTimeRange(DateRangeSelectionResult dateRangeSelectionResult, DateFormat dateFormat)
        {
            var range = dateRangeSelectionResult.SelectedRange.Value;
            var knownShortcut = dateRangeShortcutsService.GetShortcutFrom(range);
            if (knownShortcut != null)
                return knownShortcut.Text;

            var startDateText = range.Beginning.ToString(dateFormat.Short, CultureInfo.InvariantCulture);
            var endDateText = range.End.ToString(dateFormat.Short, CultureInfo.InvariantCulture);
            return $"{startDateText} - {endDateText}";
        }

        private async Task<ImmutableList<IReportElement>> reportElementsProcess(ReportProcessData reportProcessData)
        {
            var analyticsData = new ReportsAnalyticsEventData();

            try
            {
                var filter = reportProcessData.Filter;
                analyticsData.StartTimestamp = timeService.CurrentDateTime.UtcDateTime;
                analyticsData.Source = reportProcessData.SelectionSource;
                analyticsData.TotalDays = (filter.TimeRange.Maximum - filter.TimeRange.Minimum).Days + 1;
                analyticsData.IsSuccessful = true;

                var user = await interactorFactory.GetCurrentUser().Execute();

                var reportsTotal = await interactorFactory
                    .GetReportsTotals(user.Id, filter.Workspace.Id, filter.TimeRange)
                    .Execute();

                var summaryData = await interactorFactory
                    .GetProjectSummary(filter.Workspace.Id, filter.TimeRange.Minimum, filter.TimeRange.Maximum)
                    .Execute();

                analyticsData.ProjectsNotSyncedCount = summaryData.ProjectsNotSyncedCount;

                var preferences = await interactorFactory
                    .ObserveCurrentPreferences()
                    .Execute()
                    .FirstAsync();

                var durationFormat = preferences.DurationFormat;
                var dateFormat = preferences.DateFormat;

                if (summaryData.Segments.None())
                    return elements(new ReportNoDataElement());

                return elements(
                    new ReportWorkspaceNameElement(filter.Workspace.Name),
                    new ReportSummaryElement(summaryData, durationFormat),
                    new ReportProjectsBarChartElement(reportsTotal, dateFormat),
                    new ReportProjectsDonutChartElement(summaryData, durationFormat));
            }
            catch (Exception ex)
            {
                analyticsData.IsSuccessful = false;
                return elements(new ReportErrorElement(ex));
            }
            finally
            {
                trackReportElementProcessCompletion(analyticsData);
            }
        }

        private ImmutableList<IReportElement> elements(params IReportElement[] elements)
            => elements.Flatten();

        private void trackReportElementProcessCompletion(ReportsAnalyticsEventData eventData)
        {
            var loadingTime = timeService.CurrentDateTime.UtcDateTime - eventData.StartTimestamp;

            if (eventData.IsSuccessful)
            {
                analyticsService.ReportsSuccess.Track(eventData.Source, eventData.TotalDays, eventData.ProjectsNotSyncedCount, loadingTime.TotalMilliseconds);
            }
            else
            {
                analyticsService.ReportsFailure.Track(eventData.Source, eventData.TotalDays, loadingTime.TotalMilliseconds);
            }
        }

        private struct ReportsAnalyticsEventData
        {
            public DateTime StartTimestamp { get; set; }
            public DateRangeSelectionSource Source { get; set; }
            public int TotalDays { get; set; }
            public int ProjectsNotSyncedCount { get; set; }
            public bool IsSuccessful { get; set; }
        }
    }
}
