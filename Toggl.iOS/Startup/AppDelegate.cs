using Foundation;
using Toggl.Core;
using Toggl.Core.UI;
using Toggl.Core.UI.Navigation;
using Toggl.Core.UI.Parameters;
using Toggl.Core.UI.ViewModels;
using Toggl.iOS.Presentation;
using Toggl.iOS.Services;
using UIKit;
using UserNotifications;

namespace Toggl.iOS
{
    [Register(nameof(AppDelegate))]
    public sealed partial class AppDelegate : UIApplicationDelegate, IUNUserNotificationCenterDelegate
    {
        public override UIWindow Window { get; set; }

        public override bool FinishedLaunching(UIApplication application, NSDictionary launchOptions)
        {
#if !USE_PRODUCTION_API
            System.Net.ServicePointManager.ServerCertificateValidationCallback
                  += (sender, certificate, chain, sslPolicyErrors) => true;
#endif

            initializeAnalytics();

            Window = new UIWindow(UIScreen.MainScreen.Bounds);
            Window.MakeKeyAndVisible();

            setupNavigationBar();
            setupTabBar();

            IosDependencyContainer.EnsureInitialized(Window, this);
            var app = new AppStart(IosDependencyContainer.Instance);
            app.UpdateOnboardingProgress();
            app.SetFirstOpened();
            app.SetupBackgroundSync();

            var accessLevel = app.GetAccessLevel();
            loginWithCredentialsIfNecessary(accessLevel);
            navigateAccordingToAccessLevel(accessLevel, app);

            UNUserNotificationCenter.Current.Delegate = this;

            var watchservice = new WatchService();
            watchservice.TryLogWatchConnectivity();

#if ENABLE_TEST_CLOUD
            Xamarin.Calabash.Start();
#endif

            return true;
        }

        private void loginWithCredentialsIfNecessary(AccessLevel accessLevel)
        {
            if (accessLevel == AccessLevel.LoggedIn || accessLevel == AccessLevel.TokenRevoked)
            {
                IosDependencyContainer.Instance
                    .UserAccessManager
                    .LoginWithSavedCredentials();
            }
        }

        public override bool OpenUrl(UIApplication app, NSUrl url, NSDictionary options)
        {
            if (url.Scheme == ApplicationUrls.Scheme)
            {
                handleDeeplink(url);
                return true;
            }

#if USE_ANALYTICS
            var openUrlOptions = new UIKit.UIApplicationOpenUrlOptions(options);
            return Google.SignIn.SignIn.SharedInstance.HandleUrl(url, openUrlOptions.SourceApplication, openUrlOptions.Annotation);
#endif

            return false;
        }

        public override void ReceiveMemoryWarning(UIApplication application)
        {
            IosDependencyContainer.Instance.AnalyticsService.ReceivedLowMemoryWarning.Track(Platform.Daneel);
        }

        public override void ApplicationSignificantTimeChange(UIApplication application)
        {
            IosDependencyContainer.Instance.TimeService.SignificantTimeChanged();
        }

        private void navigateAccordingToAccessLevel(AccessLevel accessLevel, AppStart app)
        {
            var navigationService = IosDependencyContainer.Instance.NavigationService;

            switch (accessLevel)
            {
                case AccessLevel.AccessRestricted:
                    navigationService.Navigate<OutdatedAppViewModel>(null);
                    return;
                case AccessLevel.NotLoggedIn:
                    navigationService.Navigate<LoginViewModel, CredentialsParameter>(CredentialsParameter.Empty, null);
                    return;
                case AccessLevel.TokenRevoked:
                    navigationService.Navigate<TokenResetViewModel>(null);
                    return;
                case AccessLevel.LoggedIn:
                    app.ForceFullSync();
                    var viewModel = IosDependencyContainer.Instance
                        .ViewModelLoader
                        .Load<MainTabBarViewModel>();
                    viewModel.Initialize();
                    Window.RootViewController = ViewControllerLocator.GetViewController(viewModel);
                    return;
            }
        }
    }
}
