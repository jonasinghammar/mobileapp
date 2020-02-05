using System;
using Toggl.Core.DataSources;
using Toggl.Core.Models.Interfaces;

namespace Toggl.Core.Interactors
{
    internal sealed class ObservePreferencesInteractor : IInteractor<IObservable<IThreadSafePreferences>>
    {
        private readonly ITogglDataSource dataSource;

        public ObservePreferencesInteractor(ITogglDataSource dataSource)
        {
            this.dataSource = dataSource;
        }

        public IObservable<IThreadSafePreferences> Execute()
            => dataSource.Preferences.Current;
    }
}
