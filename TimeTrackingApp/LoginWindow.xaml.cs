using System.Windows;
using System.Windows.Input;
using Firebase.Auth;

namespace TimeTrackingApp
{
    public partial class LoginWindow : Window
    {
        public LoginWindow() => InitializeComponent();

        private async void Login_Click(object sender, RoutedEventArgs e)
        {
            string login = LoginBox.Text;
            string password = PasswordBox.Password;

            try
            {
                var (userId, role) = await FirebaseService.AuthenticateUser(login, password);

                if (userId == null)
                {
                    MessageBox.Show("Пользователь с таким логином или паролем не найден.");
                    return;
                }

                var mainWindow = new MainWindow();
                mainWindow.Show();
                mainWindow.ShowRoleInterface(role, userId);
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка входа: {ex.Message}. Попробуйте снова.");
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Header_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove(); // Позволяет двигать окно
        }
    }
}