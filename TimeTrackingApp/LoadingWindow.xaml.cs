using System.Windows;

namespace TimeTrackingApp
{
    public partial class LoadingWindow : Window
    {
        public LoadingWindow()
        {
            InitializeComponent();
        }

        public void SetProgress(double value, string status = null)
        {
            ProgressBar.Value = value;
            if (status != null)
            {
                StatusText.Text = status;
            }

            // Чтобы UI не подвисал
            DoEvents();
        }

        private void DoEvents()
        {
            Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render);
        }
    }
}