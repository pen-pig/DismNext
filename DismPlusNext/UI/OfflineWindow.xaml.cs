using System;
using System.IO;
using System.Linq;
using System.Windows;
using DismPlusNext.Engine;
using DismPlusNext.ViewModels;
using Microsoft.Win32;

namespace DismPlusNext.UI
{
    /// <summary>离线映像服务窗口：选择 WIM/ESD、查看索引、挂载/卸载。挂载成功后回调 MainViewModel.EnterOffline。</summary>
    public partial class OfflineWindow : Window
    {
        private readonly MainViewModel _vm;
        private string _mountedDir;

        public OfflineWindow(MainViewModel vm)
        {
            InitializeComponent();
            _vm = vm;
            TxtMount.Text = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DismPlusNext", "Mount");
        }

        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "映像文件 (*.wim;*.esd)|*.wim;*.esd|所有文件 (*.*)|*.*",
                Title = "选择 WIM / ESD 映像",
            };
            if (dlg.ShowDialog(this) == true) TxtWim.Text = dlg.FileName;
        }

        private void BtnInfo_Click(object sender, RoutedEventArgs e)
        {
            string wim = TxtWim.Text.Trim();
            if (string.IsNullOrEmpty(wim)) { TxtInfoHint.Text = "请先选择映像文件。"; return; }
            var (ok, indices, outp) = ImageServicing.GetWimInfo(wim);
            if (!ok) { TxtInfoHint.Text = "获取失败：" + outp.Trim().Split('\n').LastOrDefault() ?? outp; GridIndices.ItemsSource = null; return; }
            GridIndices.ItemsSource = indices;
            TxtInfoHint.Text = $"共 {indices.Count} 个卷。";
        }

        private void BtnMount_Click(object sender, RoutedEventArgs e)
        {
            string wim = TxtWim.Text.Trim();
            string mountDir = TxtMount.Text.Trim();
            if (string.IsNullOrEmpty(wim) || string.IsNullOrEmpty(mountDir)) { MessageBox.Show("请填写映像路径与挂载目录。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            int index = 1;
            if (GridIndices.SelectedItem is ImageServicing.WimIndexInfo sel && sel.Index > 0) index = sel.Index;
            else if (GridIndices.ItemsSource is System.Collections.Generic.List<ImageServicing.WimIndexInfo> list && list.Count > 0) index = list[0].Index;

            var (ok, outp) = ImageServicing.Mount(wim, index, mountDir, false);
            if (!ok) { MessageBox.Show("挂载失败：\n" + outp, "挂载失败", MessageBoxButton.OK, MessageBoxImage.Error); return; }

            var (ok2, msg) = OfflineContext.Create(mountDir, true);
            if (!ok2)
            {
                ImageServicing.Unmount(mountDir, false);
                MessageBox.Show("加载注册表配置单元失败：\n" + msg, "离线上下文失败", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            _mountedDir = mountDir;
            _vm.EnterOffline();
            BtnMount.IsEnabled = false;
            BtnUnmount.IsEnabled = true;
            MessageBox.Show($"已挂载索引 {index} 并加载配置单元。\n现在『空间回收』与『系统优化』将作用于该映像。\n完成后点击『卸载并提交』。", "挂载成功", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnUnmount_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_mountedDir)) return;
            var (ok, outp) = ImageServicing.Unmount(_mountedDir, true);
            OfflineContext.Current?.Dispose();
            _vm.EnterOffline();
            if (!ok) { MessageBox.Show("卸载失败：\n" + outp, "卸载失败", MessageBoxButton.OK, MessageBoxImage.Error); return; }
            _mountedDir = null;
            BtnUnmount.IsEnabled = false;
            BtnMount.IsEnabled = true;
            MessageBox.Show("已提交更改并卸载映像，退出离线模式。", "完成", MessageBoxButton.OK, MessageBoxImage.Information);
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_mountedDir))
            {
                var (ok, _) = ImageServicing.Unmount(_mountedDir, true);
                OfflineContext.Current?.Dispose();
                _vm.EnterOffline();
            }
            Close();
        }
    }
}
