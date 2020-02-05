using FluentAssertions;
using System;
using Toggl.Core.UI.Navigation;
using Toggl.Core.UI.ViewModels;
using Xunit;

namespace Toggl.Core.Tests.UI.ViewModels
{
    public class MainTabViewModelTests
    {
        public abstract class MainTabViewModelTest : BaseViewModelTests<MainTabBarViewModel>
        {
            protected override MainTabBarViewModel CreateViewModel()
                => new MainTabBarViewModel(new TestDependencyContainer
                    { MockNavigationService =
                        NSubstitute.Substitute.For<INavigationService>() });
        }

        public sealed class TheConstructor : MainTabViewModelTest
        {
            [Fact, LogIfTooSlow]
            public void ThrowsIfAnyOfTheArgumentsIsNull()
            {
                Action tryingToConstructWithEmptyParameters =
                    () => new MainTabBarViewModel(null);

                tryingToConstructWithEmptyParameters
                    .Should().Throw<Exception>();
            }
        }
    }
}
