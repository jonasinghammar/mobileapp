using NUnit.Framework;
using System.Linq;
using Xamarin.UITest;

namespace Toggl.Tests.UI.Extensions
{
    public static partial class LoginExtensions
    {
        public static void WaitForLoginScreen(this IApp app)
        {
            //Android doesn't have the onboarding screen
        }

        public static void CheckThatLoginButtonIsDisabled(this IApp app)
        {
            var button = app.Query(Login.LoginButton).First();
            var isButtonDisabled = !button.Enabled;
            Assert.True(isButtonDisabled);
        }
    }
}
