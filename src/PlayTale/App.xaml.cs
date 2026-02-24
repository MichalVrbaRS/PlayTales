namespace PlayTale
{
    public partial class App : Application
    {
        private readonly Features.Audiobooks.Services.IPlaybackProgressTracker _playbackProgressTracker;

        public App(Features.Audiobooks.Services.IPlaybackProgressTracker playbackProgressTracker)
        {
            _playbackProgressTracker = playbackProgressTracker;
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new MainPage()) { Title = "PlayTale" };
        }

        protected override void OnSleep()
        {
            _ = _playbackProgressTracker.FlushAsync();
            base.OnSleep();
        }
    }
}
