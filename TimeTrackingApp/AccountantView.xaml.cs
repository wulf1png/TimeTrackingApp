using ClosedXML.Excel;
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media.Animation;
using System.Windows.Media;
using System.Windows.Threading;
using static TimeTrackingApp.FirebaseService;
using System.Windows.Shapes;


namespace TimeTrackingApp
{

    public partial class AccountantView : UserControl, INotifyPropertyChanged
    {
        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set { _isLoading = value; OnPropertyChanged(); }
        }



        public ObservableCollection<EmployeeInfo> Employees { get; } = new();

        public ICollectionView EmployeesView { get; }
        public string SelectedEmployeeId { get; set; }
        public DateTime FromDate { get; set; } = DateTime.Today.AddDays(-7);
        public DateTime ToDate { get; set; } = DateTime.Today;

        private bool _isDropDownOpen;
        public bool IsDropDownOpen
        {
            get => _isDropDownOpen;
            set { _isDropDownOpen = value; OnPropertyChanged(); }
        }




        private ObservableCollection<ReportItem> _reportItems = new();

        private string _searchText;
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText == value) return;
                _searchText = value;
                OnPropertyChanged();
                EmployeesView.Refresh();
                // при вводе сразу открываем дропдаун
                IsDropDownOpen = !string.IsNullOrWhiteSpace(_searchText);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public AccountantView(string userId)
        {
            InitializeComponent();
            DataContext = this;

            EmployeesView = CollectionViewSource.GetDefaultView(Employees);
            EmployeesView.Filter = o =>
            {
                if (string.IsNullOrWhiteSpace(SearchText))
                    return true;
                var emp = (EmployeeInfo)o;
                // Сравниваем по ФИО (FullName) без учёта регистра
                return emp.FullName.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) >= 0;
            };
            _ = LoadEmployeesAsync();
        }



        private async Task LoadEmployeesAsync()
        {
            var list = await FirebaseService.GetAllEmployeesAsync();
            Employees.Clear();
            foreach (var emp in list) Employees.Add(emp);
            SearchText = "";     // чтобы увидеть весь список сразу
            IsDropDownOpen = false;
            if (Employees.Any())
                SelectedEmployeeId = Employees.First().Uid;
        }



        private async void LoadReport_Click(object sender, RoutedEventArgs e)
        {
            // Показываем спиннер, начинаем загрузку
            IsLoading = true;

            Dispatcher.BeginInvoke(async () =>
            {
            try
            {

                // Даем UI-потоку время на рендеринг и отображение спиннера
                await Dispatcher.Yield(DispatcherPriority.Render);

            if (string.IsNullOrEmpty(SelectedEmployeeId)) return;

            var report = new ObservableCollection<ReportItem>();
            var specialStatuses = new[] { "отпуск (опл.)", "отпуск (неопл.)", "больничный" };

            for (var date = FromDate.Date; date <= ToDate.Date; date = date.AddDays(1))
            {
                // Даем UI-потоку прокрутить спиннер
                await Dispatcher.Yield(DispatcherPriority.Background);

                // 1) Загрузка запланированных сегментов
                var segments = await FirebaseService.GetExtendedSegmentsAsync(SelectedEmployeeId, date);
                double plannedHours = 0;
                foreach (var s in segments)
                    plannedHours += (s.End - s.Start).TotalHours;

                // 2) Загрузка фактических записей
                var records = await FirebaseService.GetWorkStatusRecordsAsync(SelectedEmployeeId, date);
                double actualHours = 0;
                foreach (var r in records)
                {
                    var end = r.End != default ? r.End : (date == DateTime.Today ? DateTime.Now : r.Start);
                    var start = r.Start < date ? date : r.Start;
                    var finish = end > date.AddDays(1) ? date.AddDays(1) : end;
                    actualHours += (finish - start).TotalHours;
                }

                var emp = Employees.First(x => x.Uid == SelectedEmployeeId);

                // 3) Определяем, есть ли в этот день «спецстатус»
                string specialStatus = segments
                    .Select(s => s.Status.Trim().ToLower())
                    .FirstOrDefault(st => specialStatuses.Contains(st));
                if (specialStatus != null)
                {
                    specialStatus = segments
                        .First(s => s.Status.Trim().ToLower() == specialStatus)
                        .Status;
                }

                // 4) Считаем нарушения — только если нет спецстатуса
                double violationHours = 0;
                if (specialStatus == null)
                {
                    foreach (var seg in segments)
                    {
                        int violMin = 0;
                        for (var t = seg.Start; t < seg.End; t = t.AddMinutes(1))
                        {
                            var rec = records.FirstOrDefault(r =>
                            {
                                var recEnd = r.End != default
                                    ? r.End
                                    : (date == DateTime.Today ? DateTime.Now : r.Start);
                                return r.Start <= t && recEnd > t;
                            });
                            if (rec == null || rec.Status != seg.Status)
                                violMin++;
                        }
                        violationHours += violMin / 60.0;
                    }
                }
                report.Add(new ReportItem
                {
                    EmployeeName = emp.FullName,
                    Date = date,
                    PlannedHours = plannedHours,
                    ActualHours = actualHours,
                    ViolationHours = violationHours,
                    SpecialStatus = specialStatus
                });
            }

            // Привязываем к DataGrid
            ReportDataGrid.ItemsSource = report;

                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Ошибка загрузки данных: {ex.Message}");
                }
                finally
                {
                    // 3) Скрываем overlay и спиннер
                    IsLoading = false;
                }
            }, DispatcherPriority.Background);

            // 4) Стартуем анимацию спиннера сразу же
            StartSpinnerAnimation();
        }


        private void StartSpinnerAnimation()
        {
            // Применяем шаблон для каждого спиннера
            SpinnerControl_Table.ApplyTemplate();
           

            // Получаем элемент Path из каждого ContentControl
            var arc1 = SpinnerControl_Table.Template.FindName("Arc1", SpinnerControl_Table) as System.Windows.Shapes.Path;

            // Проверяем, что элементы найдены
            if (arc1 == null)
            {
                Debug.WriteLine("Не удалось найти один или несколько элементов 'Arc'!");
                return;
            }

            // Создаем новый RotateTransform для каждого Path
            var rotateTransform1 = new RotateTransform();
            var rotateTransform2 = new RotateTransform();
            var rotateTransform3 = new RotateTransform();

            // Устанавливаем новый RotateTransform для каждого элемента
            arc1.RenderTransform = rotateTransform1;
            

            // Применяем анимацию вращения ко всем спиннерам
            var rotateAnim = new DoubleAnimation(0, 360, TimeSpan.FromSeconds(1.2))
            {
                RepeatBehavior = RepeatBehavior.Forever
            };

            // Начинаем анимацию
            rotateTransform1.BeginAnimation(RotateTransform.AngleProperty, rotateAnim);
            
        }


        private void ExportToExcel_Click(object sender, RoutedEventArgs e)
        {
            if (_reportItems == null || !_reportItems.Any())
            {
                MessageBox.Show("Сначала загрузите отчёт.");
                return;
            }

            var dlg = new SaveFileDialog
            {
                Filter = "Excel Workbook|*.xlsx",
                FileName = $"Report_{SelectedEmployeeId}_{FromDate:yyyyMMdd}_{ToDate:yyyyMMdd}.xlsx"
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                using var wb = new XLWorkbook();
                var ws = wb.Worksheets.Add("Отчёт");

                // Заголовки
                ws.Cell(1, 1).Value = "Сотрудник";
                ws.Cell(1, 2).Value = "Дата";
                ws.Cell(1, 3).Value = "План, ч.";
                ws.Cell(1, 4).Value = "Факт, ч.";
                ws.Cell(1, 5).Value = "Нарушения, ч.";
                ws.Cell(1, 6).Value = "Спец. статус";

                // Стиль заголовков
                var headerRange = ws.Range(1, 1, 1, 6);
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;
                headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                headerRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

                int row = 2;
                foreach (var item in _reportItems)
                {
                    ws.Cell(row, 1).Value = item.EmployeeName;
                    ws.Cell(row, 2).Value = item.Date;
                    ws.Cell(row, 2).Style.DateFormat.Format = "dd.MM.yyyy";
                    ws.Cell(row, 3).Value = Math.Round(item.PlannedHours, 1);
                    ws.Cell(row, 4).Value = Math.Round(item.ActualHours, 1);
                    ws.Cell(row, 5).Value = Math.Round(item.ViolationHours, 1);
                    ws.Cell(row, 6).Value = item.SpecialStatus ?? "-";

                    row++;
                }

                // Итоги
                ws.Cell(row, 2).Value = "Итого:";
                ws.Cell(row, 2).Style.Font.Bold = true;
                ws.Cell(row, 3).FormulaA1 = $"=SUM(C2:C{row - 1})";
                ws.Cell(row, 4).FormulaA1 = $"=SUM(D2:D{row - 1})";
                ws.Cell(row, 5).FormulaA1 = $"=SUM(E2:E{row - 1})";
                ws.Range(row, 3, row, 5).Style.NumberFormat.Format = "0.0";
                ws.Range(row, 2, row, 5).Style.Font.Bold = true;

                // Автоширина и границы
                ws.Columns().AdjustToContents();
                ws.RangeUsed().Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                ws.RangeUsed().Style.Border.InsideBorder = XLBorderStyleValues.Thin;

                wb.SaveAs(dlg.FileName);
                MessageBox.Show("Экспорт в Excel выполнен успешно.");
            }
            catch (IOException ex)
            {
                MessageBox.Show($"Ошибка при сохранении файла:\n{ex.Message}");
            }
        }

        private async void GenerateT13_Click(object sender, RoutedEventArgs e)
        {
            if (_reportItems == null || !_reportItems.Any())
            {
                MessageBox.Show("Сначала загрузите отчёт.");
                return;
            }

            // 1) Год, месяц и число дней
            int year = FromDate.Year;
            int month = FromDate.Month;  
            int daysInMonth = DateTime.DaysInMonth(year, month);

            // 2) Шаблон
            var templatePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates", "t-13.xlsx");
            if (!File.Exists(templatePath))
            {
                MessageBox.Show($"Не найден шаблон по пути:\n{templatePath}");
                return;
            }

            var dlg = new SaveFileDialog
            {
                Filter = "Excel Workbook|*.xlsx",
                FileName = $"T13{SelectedEmployeeId}{FromDate:yyMM}.xlsx"
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                using var wb = new XLWorkbook(templatePath);
                var ws = wb.Worksheets.First();

                // 3) Шапка
                var emp = Employees.First(x => x.Uid == SelectedEmployeeId);
                ws.Cell("I24").Value = $"{emp.lastName} {emp.firstName} {emp.patronymic}";
                ws.Cell("AJ24").Value = emp.Uid;
                ws.Cell("FT13").Value = $"{FromDate:dd.MM.yyyy}";
                ws.Cell("GG13").Value = $"{ToDate:dd.MM.yyyy}";
                ws.Cell("A24").Value = 1;
                ws.Cell("EA13").Value = System.IO.Path.GetFileNameWithoutExtension(dlg.FileName);
                ws.Cell("ES13").Value = DateTime.Now.ToString("dd.MM.yyyy");

                // 4) Подсчёт первой/второй половин месяца
                var firstHalf = _reportItems.Where(i => i.Date.Day <= 15);
                var secondHalf = _reportItems.Where(i => i.Date.Day >= 16 && i.Date.Day <= daysInMonth);

                int days1 = firstHalf.Count(i => i.ActualHours > 0);
                double hrs1 = firstHalf.Sum(i => i.ActualHours);
                int days2 = secondHalf.Count(i => i.ActualHours > 0);
                double hrs2 = secondHalf.Sum(i => i.ActualHours);

                ws.Cell("DI24").Value = days1;
                ws.Cell("DI25").Value = hrs1;
                ws.Cell("DI26").Value = days2;
                ws.Cell("DI27").Value = hrs2;
                ws.Cell("DT24").Value = days1 + days2;
                ws.Cell("DT26").Value = hrs1 + hrs2;

                // 5) Считаем коды неявок
                int cntN = 0; double hoursN = 0;
                int cntOT = 0; double hoursOT = 0;
                int cntDO = 0; double hoursDO = 0;
                int cntB = 0; double hoursB = 0;

                foreach (var item in _reportItems)
                {
                    var date = item.Date;
                    bool isWeekend = date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

                    if (!isWeekend && item.PlannedHours > 0 && item.ActualHours == 0 && item.SpecialStatus == null)
                    {
                        cntN++;
                        hoursN += item.PlannedHours;
                    }
                    else if (item.SpecialStatus?.ToLower().Trim() == "отпуск (опл.)")
                    {
                        cntOT++;
                        hoursOT += item.PlannedHours;
                    }
                    else if (item.SpecialStatus?.ToLower().Trim() == "отпуск (неопл.)")
                    {
                        cntDO++;
                        hoursDO += item.PlannedHours;
                    }
                    else if (item.SpecialStatus?.ToLower().Trim() == "больничный")
                    {
                        cntB++;
                        hoursB += item.PlannedHours;
                    }
                }

                // Заполняем ячейки в Excel
                ws.Cell("GO24").Value = "Н";
                ws.Cell("HD24").Value = $"{cntN} ({hoursN:0.#})";

                ws.Cell("GO25").Value = "ОТ";
                ws.Cell("HD25").Value = $"{cntOT} ({hoursOT:0.#})";

                ws.Cell("GO26").Value = "ДО";
                ws.Cell("HD26").Value = $"{cntDO} ({hoursDO:0.#})";

                ws.Cell("GO27").Value = "Б";
                ws.Cell("HD27").Value = $"{cntB} ({hoursB:0.#})";
                // (код заполнения AW–AZ, BA–BD и т.д.)

                // 7) Готовим блоки по 4 столбца на каждый день
                var blocks = new[]
                {
            new[]{ "AW","AX","AY","AZ" }, new[]{ "BA","BB","BC","BD" },
            new[]{ "BE","BF","BG","BH" }, new[]{ "BI","BJ","BK","BL" },
            new[]{ "BM","BN","BO","BP" }, new[]{ "BQ","BR","BS","BT" },
            new[]{ "BU","BV","BW","BX" }, new[]{ "BY","BZ","CA","CB" },
            new[]{ "CC","CD","CE","CF" }, new[]{ "CG","CH","CI","CJ" },
            new[]{ "CK","CL","CM","CN" }, new[]{ "CO","CP","CQ","CR" },
            new[]{ "CS","CT","CU","CV" }, new[]{ "CW","CX","CY","CZ" },
            new[]{ "DA","DB","DC","DD" }, new[]{ "DE","DF","DG","DH" }
        };

                // 8) Заполняем явки/отсутствия и часы
                for (int day = 1; day <= daysInMonth; day++)
                {
                    var date = new DateTime(year, month, day);
                    bool isWeekend = date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
                    var item = _reportItems.FirstOrDefault(i => i.Date.Day == day);

                    int idx = day == 31 ? 15 : (day - 1) % 15;
                    var cols = blocks[idx];
                    int rowYn = day <= 15 ? 24 : 26;
                    int rowH = day <= 15 ? 25 : 27;

                    // 8.1) Я/В/ОТ/ДО/Б
                    // 8.1) Я/Н/ОТ/ДО/Б
                    string mark;
                    var segs = await FirebaseService.GetExtendedSegmentsAsync(SelectedEmployeeId, date);
                    bool hasWorked = item != null && item.ActualHours > 0;

                    // 1) Выходные без каких-либо сегментов — "В"
                    if (isWeekend && segs.Count == 0)
                        mark = "В";
                    // 2) Любой отпуск или больничный — независимо от дня
                    else if (segs.Any(s => s.Status == "Отпуск (опл.)"))
                        mark = "ОТ";
                    else if (segs.Any(s => s.Status == "Отпуск (неопл.)"))
                        mark = "ДО";
                    else if (segs.Any(s => s.Status == "Больничный"))
                        mark = "Б";
                    // 3) Неявка (только будни без сегментов и без факта)
                    else if (!isWeekend && segs.Count == 0 && !hasWorked)
                        mark = "Н";
                    // 4) Всё остальное с хоть каким-то фактом — "Я"
                    else if (hasWorked)
                        mark = "Я";
                    // 5) На всякий случай всё остальное тоже считаем неявкой
                    else
                        mark = "Н";

                    foreach (var c in cols)
                        ws.Cell($"{c}{rowYn}").Value = mark;

                    // 8.2) Часы
                    string hrs = "";
                    if (mark == "Я")
                        hrs = TimeSpan.FromHours(item.ActualHours).ToString("h\\:mm");
                    // остальные — остаются пустыми

                    foreach (var c in cols)
                        ws.Cell($"{c}{rowH}").Value = hrs;
                }

                // 9) «X» после конца месяца
                for (int day = daysInMonth + 1; day <= 31; day++)
                {
                    int idx = day == 31 ? 15 : (day - 1) % 15;
                    var cols = blocks[idx];
                    int rowYn = day <= 15 ? 24 : 26;
                    int rowH = day <= 15 ? 25 : 27;
                    foreach (var c in cols)
                    {
                        ws.Cell($"{c}{rowYn}").Value = "X";
                        ws.Cell($"{c}{rowH}").Value = "X";
                    }
                }

                // 10) Сохраняем
                wb.SaveAs(dlg.FileName);
                MessageBox.Show("Табель Т-13 успешно сгенерирован.");
            }
            catch (IOException ex)
            {
                MessageBox.Show($"Ошибка при сохранении файла:\n{ex.Message}");
            }
        }

    }

        public class ReportItem
    {
        public string EmployeeName { get; set; }
        public DateTime Date { get; set; }
        public double PlannedHours { get; set; }
        public double ActualHours { get; set; }
        public double ViolationHours { get; set; }
        public string SpecialStatus { get; set; }
        public List<char> Statuses { get; set; }

        public string PlannedFormatted
        {
            get
            {
                if (PlannedHours <= 0) return "";
                return TimeSpan.FromHours(PlannedHours).ToString("h\\:mm");
            }
        }
        public string ActualFormatted => !string.IsNullOrEmpty(SpecialStatus) ? SpecialStatus : ActualHours.ToString("0.##");
        public string ViolationFormatted
        {
            get
            {
                if (ViolationHours <= 0) return "";
                return TimeSpan.FromHours(ViolationHours).ToString("h\\:mm");
            }
        }
    }
}