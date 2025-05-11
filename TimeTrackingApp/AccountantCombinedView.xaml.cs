using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace TimeTrackingApp
{
    /// <summary>
    /// Логика взаимодействия для AccountantCombinedView.xaml
    /// </summary>
    public partial class AccountantCombinedView : UserControl
    {
        public AccountantCombinedView(string userId)
        {
            InitializeComponent();

            var emp = new EmployeeView(userId)
            {
                Background = Brushes.Transparent // Устанавливаем фон прозрачным
            };
            var man = new AccountantView(userId);

            MainPanel1.Children.Add(emp);

            //MainPanel.Children.Add(new Separator
            //{
            //    //Margin = new Thickness(0, 10, 0, 10)
            //});

            MainPanel2.Children.Add(man);
        }
    }
}
