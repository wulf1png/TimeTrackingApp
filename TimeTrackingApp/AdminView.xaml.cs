using System;
using System.Windows;
using System.Windows.Controls;
using Firebase.Auth;
using Firebase.Database;
using Firebase.Database.Query;
using static TimeTrackingApp.FirebaseService;

namespace TimeTrackingApp
{
    public partial class AdminView : UserControl
    {
        private readonly FirebaseClient client = new("https://ttabd-97e9a-default-rtdb.europe-west1.firebasedatabase.app/");

        public AdminView()
        {
            InitializeComponent();
            LoadLogs();
        }

        /// <summary>
        /// Получить следующий UserId и одновременно увеличить счётчик на 1.
        /// Не идеально атомарно, но работает в 99% сценариев с низкой конкуренцией.
        /// </summary>
        private async Task<long> AllocateNextUserIdAsync()
        {
            var node = client.Child("metadata").Child("nextUserId");

            // Попробуем прочитать текущее значение
            long current = 0;
            try
            {
                current = await node.OnceSingleAsync<long>();
            }
            catch
            {
                // Если узел не существует или там не число — считаем, что первый id будет 1
                current = 1;
            }

            long assigned = current;
            long next = current + 1;

            // Запишем обновлённое значение обратно
            await node.PutAsync(next);

            return assigned;
        }

        private async void Register_Click(object sender, RoutedEventArgs e)
        {
            // 1) Зарезервировать новый числовой Uid
            var assignedId = await AllocateNextUserIdAsync();

            // 2) Подготовить данные пользователя
            var userData = new
            {
                firstName = FirstNameBox.Text,
                lastName = LastNameBox.Text,
                patronymic = PatronymicBox.Text,
                email = EmailBox.Text,
                phone = PhoneBox.Text,
                login = LoginBox.Text,
                password = PasswordBox.Password,
                role = ((ComboBoxItem)RoleBox.SelectedItem)?.Content.ToString()
            };

            // 3) Сохранить их под ключом "1", "2", "3" и т.д.
            await client
                .Child("users")
                .Child(assignedId.ToString())
                .PutAsync(userData);

            MessageBox.Show($"Пользователь зарегистрирован с Uid = {assignedId}");
        }

        private async void LoadLogs()
        {
            try
            {
                var logs = await client.Child("logs").OnceAsync<string>();
                foreach (var log in logs)
                {
                    LogsBox.Items.Add(log.Object);
                }
            }
            catch
            {
                LogsBox.Items.Add("Ошибка загрузки логов.");
            }
        }

    }
}