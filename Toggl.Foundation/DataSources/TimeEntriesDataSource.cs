﻿using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Linq;
using System.Linq;
using System.Reactive.Subjects;
using Toggl.Foundation.Models;
using Toggl.Multivac;
using Toggl.Multivac.Extensions;
using Toggl.PrimeRadiant;
using Toggl.PrimeRadiant.Models;
using Toggl.Foundation.DTOs;

namespace Toggl.Foundation.DataSources
{
    internal sealed class TimeEntriesDataSource : ITimeEntriesSource
    {
        private readonly IIdProvider idProvider;
        private readonly ITimeService timeService;
        private readonly IRepository<IDatabaseTimeEntry> repository;
        private readonly Subject<IDatabaseTimeEntry> timeEntryCreatedSubject = new Subject<IDatabaseTimeEntry>();
        private readonly Subject<IDatabaseTimeEntry> timeEntryUpdatedSubject = new Subject<IDatabaseTimeEntry>();
        private readonly BehaviorSubject<IDatabaseTimeEntry> currentTimeEntrySubject = new BehaviorSubject<IDatabaseTimeEntry>(null);

        public IObservable<IDatabaseTimeEntry> CurrentlyRunningTimeEntry { get; }

        public IObservable<IDatabaseTimeEntry> TimeEntryCreated { get; }

        public IObservable<IDatabaseTimeEntry> TimeEntryUpdated { get; }

        public TimeEntriesDataSource(IIdProvider idProvider, IRepository<IDatabaseTimeEntry> repository, ITimeService timeService)
        {
            Ensure.Argument.IsNotNull(idProvider, nameof(idProvider));
            Ensure.Argument.IsNotNull(repository, nameof(repository));
            Ensure.Argument.IsNotNull(timeService, nameof(timeService));

            this.repository = repository;
            this.idProvider = idProvider;
            this.timeService = timeService;

            CurrentlyRunningTimeEntry = currentTimeEntrySubject.AsObservable().DistinctUntilChanged();
            TimeEntryUpdated = timeEntryUpdatedSubject.AsObservable();
            TimeEntryCreated = timeEntryCreatedSubject.AsObservable().Merge(CurrentlyRunningTimeEntry.Where(te => te != null));

            repository
                .GetAll(te => te.Stop == null)
                .Subscribe(onRunningTimeEntry);
        }

        public IObservable<IEnumerable<IDatabaseTimeEntry>> GetAll()
            => repository.GetAll(te => !te.IsDeleted);

        public IObservable<IEnumerable<IDatabaseTimeEntry>> GetAll(Func<IDatabaseTimeEntry, bool> predicate)
            => repository.GetAll(predicate).Select(tes => tes.Where(te => !te.IsDeleted));

        public IObservable<IDatabaseTimeEntry> GetById(long id)
            => repository.GetById(id);

        public IObservable<Unit> Delete(long id)
            => repository.GetById(id)
                         .Select(TimeEntry.DirtyDeleted)
                         .SelectMany(repository.Update)
                         .Select(TimeEntry.DirtyDeleted)
                         .Do(timeEntryUpdatedSubject.OnNext)
                         .IgnoreElements()
                         .Cast<Unit>();

        public IObservable<IDatabaseTimeEntry> Start(DateTimeOffset startTime, string description, bool billable, 
            long? projectId)
            => idProvider.GetNextIdentifier()
                  .Apply(TimeEntry.Builder.Create)
                  .SetStart(startTime)
                  .SetDescription(description)
                  .SetAt(timeService.CurrentDateTime)
                  .SetBillable(billable)
                  .SetProjectId(projectId)
                  .SetIsDirty(true)
                  .Build()
                  .Apply(repository.Create)
                  .Do(safeSetCurrentlyRunningTimeEntry);

        public IObservable<IDatabaseTimeEntry> Stop(DateTimeOffset stopTime)
            => repository
                    .GetAll(te => te.Stop == null)
                    .SelectMany(timeEntries =>
                        timeEntries.Single()
                            .With(stopTime)
                            .Apply(repository.Update)
                            .Do(onTimeEntryStopped));

        public IObservable<IDatabaseTimeEntry> Update(EditTimeEntryDto dto)
            => repository.GetById(dto.Id)
                         .Select(te => createUpdatedTimeEntry(te, dto))
                         .SelectMany(repository.Update)
                         .Do(timeEntryUpdatedSubject.OnNext);

        private void onTimeEntryStopped(IDatabaseTimeEntry timeEntry)
        {
            timeEntryUpdatedSubject.OnNext(TimeEntry.Dirty(timeEntry));
            safeSetCurrentlyRunningTimeEntry(null);
        }

        private void onRunningTimeEntry(IEnumerable<IDatabaseTimeEntry> timeEntries)
            => safeSetCurrentlyRunningTimeEntry(timeEntries.SingleOrDefault());
            
        private void safeSetCurrentlyRunningTimeEntry(IDatabaseTimeEntry timeEntry)
        {
            var next = timeEntry == null ? null : TimeEntry.Clean(timeEntry);
            currentTimeEntrySubject.OnNext(next);
        }

        private TimeEntry createUpdatedTimeEntry(IDatabaseTimeEntry timeEntry, EditTimeEntryDto dto)
            => TimeEntry.Builder.Create(dto.Id)
                        .SetDescription(dto.Description)
                        .SetStop(timeEntry.Stop)
                        .SetStart(timeEntry.Start)
                        .SetTagIds(timeEntry.TagIds)
                        .SetTaskId(timeEntry.TaskId)
                        .SetUserId(timeEntry.UserId)
                        .SetBillable(timeEntry.Billable)
                        .SetTagNames(timeEntry.TagNames)
                        .SetIsDeleted(timeEntry.IsDeleted)
                        .SetProjectId(timeEntry.ProjectId)
                        .SetCreatedWith(timeEntry.CreatedWith)
                        .SetWorkspaceId(timeEntry.WorkspaceId)
                        .SetServerDeletedAt(timeEntry.ServerDeletedAt)
                        .SetAt(timeService.CurrentDateTime)
                        .SetIsDirty(true)
                        .Build();
    }
}
