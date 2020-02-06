using System;

namespace Toggl.Storage.Queries.Models
{
    public sealed class StartTimeEntryDTO
    {
        public string Description { get; }
        public bool IsBillable { get; }
        public long WorkspaceId { get; }
        public long? ProjectId { get; }
        public long? TaskId { get; }
        public long[] TagIds { get; }
        public DateTimeOffset StartTime { get; }

        public StartTimeEntryDTO(string description,
            bool isBillable,
            long workspaceId,
            long? projectId,
            long? taskId,
            long[] tagIds,
            DateTimeOffset startTime)
        {
            Description = description;
            IsBillable = isBillable;
            WorkspaceId = workspaceId;
            ProjectId = projectId;
            TaskId = taskId;
            TagIds = tagIds;
            StartTime = startTime;
        }
    }
}
