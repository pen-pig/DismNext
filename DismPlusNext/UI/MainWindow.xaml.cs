using System.Windows;
using DismPlusNext.ViewModels;

namespace DismPlusNext.UI
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _vm = new MainViewModel();

        public MainWindow()
        {
            InitializeComponent();
            DataContext = _vm;
            _vm.RequestConfirm = msg =>
                MessageBox.Show(msg, "Dism-Plus-Next", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
            Loaded += (s, e) => _vm.Init();
        }

        private void OpenBackup(object sender, RoutedEventArgs e)
        {
            var w = new BackupWindow { Owner = this };
            w.ShowDialog();
        }
    }
}
