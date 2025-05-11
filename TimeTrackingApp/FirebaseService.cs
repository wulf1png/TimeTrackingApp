using Firebase.Database;
using Firebase.Database.Query;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TimeTrackingApp
{
    public static class FirebaseService
    {
        private static readonly FirebaseClient client =
            new("https://ttabd-97e9a-default-rtdb.europe-west1.firebasedatabase.app/");

        // 🔐 Аутентификация
        public static async Task<(string uid, string role)> AuthenticateUser(string login, string password)
        {
            var users = await client
                .Child("users")
                .OrderBy("login")
                .EqualTo(login)
                .OnceAsync<UserData>();

            var user = users.FirstOrDefault();
            if (user == null || user.Object.password != password)
            {
                return (null, null);
            }

            return (user.Key, user.Object.role);
        }

        public class ExtendedPlannedSegment
        {
            public string UserId { get; set; } // обязательно
            public DateTime Start { get; set; }
            public DateTime End { get; set; }
            public string Status { get; set; } // например "На работе", "Обед", "Тренинг"
        }

        public class UserData
        {
            public string login { get; set; }
            public string password { get; set; }
            public string role { get; set; }

            public string firstName { get; set; }
            public string lastName { get; set; }
            public string patronymic { get; set; }
            public string email { get; set; }
            public string phone { get; set; }
        }

        /// <summary>
        /// Возвращает всех пользователей как список пар (ключ, модель UserData),
        /// поддерживая и словарь, и массив в Firebase.
        /// </summary>
        public static async Task<List<(string Key, UserData User)>> GetAllUsersAsync()
        {
            // Попробуем прочесть как словарь { "uid": {...}, ... }
            try
            {
                var dict = await client
                    .Child("users")
                    .OnceSingleAsync<Dictionary<string, UserData>>();

                return dict
                    .Select(kv => (kv.Key, kv.Value))
                    .ToList();
            }
            catch
            {
                // Если сервер прислал JSON-массив [...]
            }

            // Тогда читаем как List<UserData>
            try
            {
                var list = await client
                    .Child("users")
                    .OnceSingleAsync<List<UserData>>();
                var result = new List<(string, UserData)>();
                for (int i = 0; i < list.Count; i++)
                {
                    var u = list[i];
                    if (u == null) continue;      // пропускам null-элементы
                    result.Add((i.ToString(), u));
                }
                return result;
            }
            catch (Exception)
            {
                // Иначе возвращаем пустой список
                return new List<(string, UserData)>();
            }
        }

        public static Task SaveUserAsync(string userId, UserData data)
            => client.Child("users").Child(userId).PutAsync(data);

        public static async Task<List<FirebaseObject<string>>> GetLogsRawAsync()
        {
            var raw = await client
                .Child("logs")
                .OnceAsync<string>();

            // OnceAsync<string>() возвращает IReadOnlyCollection<FirebaseObject<string>>
            // конвертируем в List<FirebaseObject<string>>
            return raw.ToList();
        }

        public static Task PostLogAsync(string message)
            => client.Child("logs").PostAsync(message);

        public static async Task<long> AllocateNextUserIdAsync()
        {
            var node = client.Child("metadata").Child("nextUserId");
            long current;
            try { current = await node.OnceSingleAsync<long>(); }
            catch { current = 1; }
            await node.PutAsync(current + 1);
            return current;
        }
    


// ===================== 🔧 Плановый график =====================

public class PlannedSegment
        {
            public string UserId { get; set; }
            public DateTime Date { get; set; }
            public TimeSpan Start { get; set; }
            public TimeSpan End { get; set; }
            public string Status { get; set; }  // "На работе", "Обед", "IT проблемы" и т.д.
        }

        public class PlannedShift
        {
            public string UserId { get; set; }
            public DateTime Date { get; set; }
            public DateTime Start { get; set; }
            public DateTime End { get; set; }
            public DateTime? LunchStart { get; set; }
            public DateTime? LunchEnd { get; set; }
        }

        public static async Task SavePlannedShiftAsync(PlannedShift shift)
        {
            if (shift == null) return;

            var key = shift.Date.ToString("yyyyMMdd");
            await client
                .Child("plannedShifts")
                .Child(shift.UserId)
                .Child(key)
                .PutAsync(shift);
        }

        public static async Task<PlannedShift> GetPlannedShiftAsync(string userId, DateTime date)
        {
            var key = date.ToString("yyyyMMdd");

            var result = await client
                .Child("plannedShifts")
                .Child(userId)
                .Child(key)
                .OnceSingleAsync<PlannedShift>();

            return result;
        }

        public static async Task<List<PlannedShift>> GetPlannedShiftsAsync(string userId, DateTime startDate, DateTime endDate)
        {
            var result = new List<PlannedShift>();

            for (var date = startDate.Date; date <= endDate.Date; date = date.AddDays(1))
            {
                var key = date.ToString("yyyyMMdd");
                var shift = await client
                    .Child("plannedShifts")
                    .Child(userId)
                    .Child(key)
                    .OnceSingleAsync<PlannedShift>();

                if (shift != null)
                {
                    result.Add(shift);
                }
            }

            return result;
        }

        // ===================== 🔧 Фактические статусы =====================



        public static async Task<List<WorkStatusRecord>> GetWorkStatusRecordsAsync(string userId, DateTime date)
        {
            try
            {
                var all = await client
                    .Child("workStatusRecords")
                    .OnceAsync<WorkStatusRecord>();

                return all
                    .Select(r => { r.Object.FirebaseKey = r.Key; return r.Object; })
                    .Where(r => r.UserId == userId && r.Date.Date == date.Date)
                    .ToList();
            }
            catch (FirebaseException ex)
            {
                // Просто вернём пустой список, если нет узла
                return new List<WorkStatusRecord>();
            }
        }

        public static async Task SaveWorkStatusRecordAsync(WorkStatusRecord record)
        {
            record.Date = record.Date.Date;
            if (!string.IsNullOrEmpty(record.FirebaseKey))
            {
                await client
                    .Child("workStatusRecords")
                    .Child(record.FirebaseKey)
                    .PutAsync(record);
            }
            else
            {
                var push = await client
                    .Child("workStatusRecords")
                    .PostAsync(record);
                record.FirebaseKey = push.Key;
            }
        }

        public static async Task<UserData> GetUserByIdAsync(string uid)
        {
            // Предполагается, что в Firebase под “users/{uid}” лежит объект UserData
            var result = await client
                .Child("users")
                .Child(uid)
                .OnceSingleAsync<UserData>();
            return result;
        }

        // ===================== 🔧 Нарушения (анализ на клиенте) =====================
        // Нарушения не сохраняются отдельно в БД, они вычисляются на основе расписания и факта.

        // ===================== 🔧 Сотрудники =====================

        public class EmployeeInfo
        {
            public string Uid { get; set; }
            public string login { get; set; }

            public string firstName { get; set; }
            public string lastName { get; set; }
            public string patronymic { get; set; }
            public string FullName => $"{lastName} {firstName} {patronymic}";
        }

        public static async Task<List<EmployeeInfo>> GetAllEmployeesAsync()
        {
            // Пробуем считать как словарь (если ключи строковые)
            try
            {
                var dict = await client
                    .Child("users")
                    .OnceSingleAsync<Dictionary<string, EmployeeInfo>>();

                return dict
                    .Select(kv => {
                        var e = kv.Value;
                        e.Uid = kv.Key;
                        return e;
                    })
                    .ToList();
            }
            catch (FirebaseException)
            {
                // Если не словарь, читаем как массив
                var list = await client
                    .Child("users")
                    .OnceSingleAsync<List<EmployeeInfo>>();

                // Firebase отдаёт JSON-массив, где индекс i соответствует ключу i
                // Привязать Uid = индекс.ToString()
                var result = new List<EmployeeInfo>();
                for (int i = 0; i < list.Count; i++)
                {
                    var emp = list[i];
                    if (emp == null) continue; // пробелы в массиве => null
                    emp.Uid = i.ToString();
                    result.Add(emp);
                }
                return result;
            }
        }

        public static async Task SaveWorkStatusRecord(string userId, WorkStatusRecord record)
        {
            var dateKey = record.Start.Date.ToString("yyyy-MM-dd");
            var path = $"workStatusRecords/{userId}/{dateKey}";

            // Генерация уникального ID для новой записи
            var newId = Guid.NewGuid().ToString();
            var fullPath = $"{path}/{newId}";

            await client
                .Child(fullPath)
                .PutAsync(record);
        }

        public class User
        {
            public string Email { get; set; }
            public string Role { get; set; } // employee, manager, accountant, admin
        }

        public static async Task SaveExtendedSegmentsAsync(string userId, DateTime date, List<ExtendedPlannedSegment> segments)
        {
            var key = date.ToString("yyyyMMdd");
            var baseRef = client.Child("extendedPlannedSegments").Child(userId).Child(key);

            // Удалим старые
            await baseRef.DeleteAsync();

            // Сохраняем по одному
            foreach (var segment in segments)
            {
                await baseRef.PostAsync(segment);
            }
        }

        // 📥 Получение сегментов по дню
        public static async Task<List<ExtendedPlannedSegment>> GetExtendedSegmentsAsync(string userId, DateTime date)
        {
            var key = date.ToString("yyyyMMdd");
            try
            {
                // Читаем сразу словарь: ключ — произвольный ID, значение — твой сегмент
                var dict = await client
                    .Child("extendedPlannedSegments")
                    .Child(userId)
                    .Child(key)
                    .OnceSingleAsync<Dictionary<string, ExtendedPlannedSegment>>();

                // Если узел пуст или не существует — dict будет null
                if (dict == null)
                    return new List<ExtendedPlannedSegment>();

                return dict.Values
                           .OrderBy(seg => seg.Start)
                           .ToList();
            }
            catch
            {
                // Любой сбой чтения (например, JSON = null) обрабатываем как «нет сегментов»
                return new List<ExtendedPlannedSegment>();
            
            }
        }

        public static async Task SeedFullMonthAsync(string userId, int year, int month)
        {
            var first = new DateTime(year, month, 1);
            var last = first.AddMonths(1).AddDays(-1);

            for (var date = first; date <= last; date = date.AddDays(1))
            {
                // пропустим выходные
                if (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday)
                    continue;

                // 1) Плановые сегменты
                var planned = new List<ExtendedPlannedSegment>
        {
            new ExtendedPlannedSegment
            {
                UserId = userId,
                Status = "На работе",
                Start  = date.Date.AddHours(9),
                End    = date.Date.AddHours(18)
            }
        };
                await SaveExtendedSegmentsAsync(userId, date, planned);

                // 2) Фактические статусы
                var record = new WorkStatusRecord
                {
                    UserId = userId,
                    Date = date,
                    Status = "На работе",
                    Start = date.Date.AddHours(9),
                    End = date.Date.AddHours(18)
                };
                await SaveWorkStatusRecordAsync(record);
            }
        }


        


    }
}