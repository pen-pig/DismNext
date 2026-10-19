using System;
using System.Windows;
using System.Windows.Controls;
using DismPlusNext.Engine;

namespace DismPlusNext.UI
{
    /// <summary>系统备份/还原窗口（基于 DISM WIM 映像）。</summary>
    public partial class BackupWindow : Window
    {
        public BackupWindow()
        {
            InitializeComponent();
        }

        private void Log(string s)
        {
            TxtLog.AppendText($"{DateTime.Now:HH:mm:ss}  {s}\n");
            TxtLog.ScrollToEnd();
        }

        private void SetBusy(bool busy)
        {
            BtnBackup.IsEnabled = !busy;
            BtnRestore.IsEnabled = !busy;
            BtnInfo.IsEnabled = !busy;
        }

        private void BtnBackup_Click(object sender, RoutedEventArgs e)
        {
            var img = TxtImgPath.Text.Trim();
            var dir = TxtCaptureDir.Text.Trim();
            var name = TxtName.Text.Trim();
            if (string.IsNullOrEmpty(img) || string.IsNullOrEmpty(dir)) { MessageBox.Show("映像路径与捕获目录不能为空"); return; }
            SetBusy(true);
            Log($"开始捕获 {dir} -> {img}");
            var (ok, outp) = BackupRestore.Capture(img, dir, name, Log);
            Log(ok ? "备份完成。" : "备份失败：" + outp);
            SetBusy(false);
        }

        private void BtnRestore_Click(object sender, RoutedEventArgs e)
        {
            if (ChkConfirm.IsChecked != true) { MessageBox.Show("请先勾选确认框。"); return; }
            var img = TxtRestoreImg.Text.Trim();
            var dir = TxtApplyDir.Text.Trim();
            if (!int.TryParse(TxtIndex.Text.Trim(), out int idx)) idx = 1;
            if (string.IsNullOrEmpty(img) || string.IsNullOrEmpty(dir)) { MessageBox.Show("映像路径与目标目录不能为空"); return; }
            if (MessageBox.Show($"确认将映像 {img}（索引 {idx}）还原到 {dir}？此操作会覆盖目标。",
                "确认还原", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            SetBusy(true);
            Log($"开始还原 {img} (索引 {idx}) -> {dir}");
            var (ok, outp) = BackupRestore.Apply(img, idx, dir, Log);
            Log(ok ? "还原完成。" : "还原失败：" + outp);
            SetBusy(false);
        }

        private void BtnInfo_Click(object sender, RoutedEventArgs e)
        {
            var img = TxtInfoImg.Text.Trim();
            if (string.IsNullOrEmpty(img)) { MessageBox.Show("映像路径不能为空"); return; }
            LstImages.Items.Clear();
            var (ok, imgs, outp) = BackupRestore.ListImages(img);
            if (!ok) { Log("读取失败：" + outp); return; }
            foreach (var im in imgs)
                LstImages.Items.Add($"[{im.Index}] {im.Name} ({im.Version})");
            if (imgs.Count == 0) LstImages.Items.Add("（无卷信息或文件不存在）");
        }
    }
}
