using System.Configuration;
using System.Data;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;

namespace TimeTrackingApp
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {

        public App()
        {
            var culture = new CultureInfo("ru-RU");
            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;
            FrameworkElement.LanguageProperty.OverrideMetadata(
                typeof(FrameworkElement),
                new FrameworkPropertyMetadata(
                    System.Windows.Markup.XmlLanguage.GetLanguage(culture.IetfLanguageTag)));
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
        }
        private void PreviousMonth_Click(object sender, RoutedEventArgs e)
        {
            // Твоя логика — например, смещение отображаемой даты
        }

        private void NextMonth_Click(object sender, RoutedEventArgs e)
        {
            // Твоя логика — например, смещение отображаемой даты
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show(
                $"Необработанное исключение при загрузке XAML:\n\n{e.Exception.GetType()}: {e.Exception.Message}\n\n{e.Exception.InnerException}\n\n{e.Exception.StackTrace}",
                "Ошибка",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            e.Handled = true;
        }



    }

}
