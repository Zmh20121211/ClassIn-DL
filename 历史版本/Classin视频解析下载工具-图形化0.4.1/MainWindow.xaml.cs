using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Microsoft.Win32;
using System.ComponentModel;
using System.Globalization;

namespace VideoDownloader
{
    public partial class MainWindow : Window
    {
        private string downloadPath = string.Empty;
        private List<VideoItem> videoItems = new List<VideoItem>();
        private string lastClipboardText = string.Empty;
        private CancellationTokenSource? _clipboardCts;
        private CancellationTokenSource? _downloadCts;
        private bool _isClosing = false;

        // 添加API声明
        [DllImport("kernel32.dll")]
        private static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern int DeleteMenu(IntPtr hMenu, int nPosition, int wFlags);

        [DllImport("user32.dll")]
        private static extern IntPtr GetSystemMenu(IntPtr hWnd, bool bRevert);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDesktopWindow();

        private const int SC_CLOSE = 0xF060;
        private const int MF_BYCOMMAND = 0x00000000;

        public class VideoItem : INotifyPropertyChanged
        {
            public int Index { get; set; }
            public string Name { get; set; } = string.Empty;
            public string Url { get; set; } = string.Empty;

            private string _status = "等待解析";
            public string Status
            {
                get => _status;
                set
                {
                    if (_status != value)
                    {
                        _status = value;
                        OnPropertyChanged(nameof(Status));
                        OnPropertyChanged(nameof(DisplayStatus));
                    }
                }
            }

            private int _progress;
            public int Progress
            {
                get => _progress;
                set
                {
                    if (_progress != value)
                    {
                        _progress = value;
                        OnPropertyChanged(nameof(Progress));
                        OnPropertyChanged(nameof(DisplayStatus));
                    }
                }
            }

            // 新增下载信息属性
            private string _downloadedSize = "0 B";
            public string DownloadedSize
            {
                get => _downloadedSize;
                set
                {
                    if (_downloadedSize != value)
                    {
                        _downloadedSize = value;
                        OnPropertyChanged(nameof(DownloadedSize));
                        OnPropertyChanged(nameof(DisplayStatus));
                    }
                }
            }

            private string _totalSize = "0 B";
            public string TotalSize
            {
                get => _totalSize;
                set
                {
                    if (_totalSize != value)
                    {
                        _totalSize = value;
                        OnPropertyChanged(nameof(TotalSize));
                        OnPropertyChanged(nameof(DisplayStatus));
                    }
                }
            }

            private string _currentSpeed = "0 MB/s";
            public string CurrentSpeed
            {
                get => _currentSpeed;
                set
                {
                    if (_currentSpeed != value)
                    {
                        _currentSpeed = value;
                        OnPropertyChanged(nameof(CurrentSpeed));
                        OnPropertyChanged(nameof(DisplayStatus));
                    }
                }
            }

            private string _remainingTime = "未知";
            public string RemainingTime
            {
                get => _remainingTime;
                set
                {
                    if (_remainingTime != value)
                    {
                        _remainingTime = value;
                        OnPropertyChanged(nameof(RemainingTime));
                        OnPropertyChanged(nameof(DisplayStatus));
                    }
                }
            }

            public string DisplayStatus
            {
                get
                {
                    if (Status == "下载中...")
                    {
                        return $"下载中 ({Progress}%) - {DownloadedSize}/{TotalSize} @ {CurrentSpeed} - 剩余: {RemainingTime}";
                    }
                    return Status;
                }
            }

            public Brush StatusColor { get; set; } = Brushes.Gray;
            public bool IsDownloading { get; set; } = false;

            public event PropertyChangedEventHandler? PropertyChanged;

            protected virtual void OnPropertyChanged(string propertyName)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public MainWindow()
        {
            InitializeComponent();
            InitializeApp();
            this.Closed += MainWindow_Closed;
            // 禁用关闭按钮和任务管理器
            DisableCloseButton();
            DisableTaskManager();
        }

        // 禁用关闭按钮
        private void DisableCloseButton()
        {
            try
            {
                IntPtr hWnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                IntPtr hMenu = GetSystemMenu(hWnd, false);
                if (hMenu != IntPtr.Zero)
                {
                    int n = DeleteMenu(hMenu, SC_CLOSE, MF_BYCOMMAND);
                }
            }
            catch (Exception ex)
            {
                AppendLog($"禁用关闭按钮时出错: {ex.Message}");
            }
        }

        // 禁用任务管理器
        private void DisableTaskManager()
        {
            try
            {
                RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Policies\System");
                if (key != null)
                {
                    key.SetValue("DisableTaskMgr", 1, RegistryValueKind.DWord);
                    key.Close();
                }
            }
            catch (Exception ex)
            {
                AppendLog($"禁用任务管理器时出错: {ex.Message}");
            }
        }

        // 启用任务管理器
        private void EnableTaskManager()
        {
            try
            {
                RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Policies\System", true);
                if (key != null)
                {
                    key.DeleteValue("DisableTaskMgr", false);
                    key.Close();
                }
            }
            catch (Exception ex)
            {
                AppendLog($"启用任务管理器时出错: {ex.Message}");
            }
        }

        private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            // 防止重复处理
            if (_isClosing) return;
            _isClosing = true;

            // 无论是否有任务，都弹出确认提示
            var result = MessageBox.Show(
                "确定要退出程序吗？",
                "提示",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
            {
                e.Cancel = true; // 阻止窗口关闭
                AppendLog("用户取消了窗口关闭操作");
                _isClosing = false; // 重置状态
            }
            else
            {
                // 用户确认退出，取消所有下载任务
                _downloadCts?.Cancel();
                AppendLog("用户确认退出");
                // 在退出前启用任务管理器
                EnableTaskManager();
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            txtStatus.Text = "应用程序已就绪";
            txtCurrentAction.Text = "请开始操作";
        }

        private void MainWindow_Closed(object? sender, EventArgs e)
        {
            _clipboardCts?.Cancel();
            _downloadCts?.Cancel();
            // 在窗口关闭时也启用任务管理器
            EnableTaskManager();
        }

        private void InitializeApp()
        {
            downloadPath = Path.Combine(Environment.CurrentDirectory, "下载目录");
            Directory.CreateDirectory(downloadPath);
            UpdateDownloadPathDisplay();
            File.WriteAllText("解析结果.ini", string.Empty);
            GenerateBatFile();

            AppendLog("应用程序已初始化");
            AppendLog($"下载目录: {downloadPath}");
            AppendLog("准备就绪，请开始操作");
        }

        private void UpdateDownloadPathDisplay()
        {
            txtDownloadPath.ToolTip = downloadPath;
            const int maxDisplayLength = 40;

            if (downloadPath.Length > maxDisplayLength)
            {
                int startLength = 15;
                int endLength = 20;
                txtDownloadPath.Text = $"{downloadPath.Substring(0, startLength)}...{downloadPath.Substring(downloadPath.Length - endLength)}";
            }
            else
            {
                txtDownloadPath.Text = downloadPath;
            }
        }

        private void GenerateBatFile()
        {
            try
            {
                string batContent = "@echo off\r\nchcp 65001\r\n";

                foreach (var item in videoItems)
                {
                    if (!string.IsNullOrEmpty(item.Url))
                    {
                        string safeName = CleanName(item.Name);
                        batContent += $"aria2c.exe -d \"{downloadPath}\" -o \"{safeName}.mp4\" \"{item.Url}\"\r\n";
                    }
                }

                File.WriteAllText("下载视频工具.bat", batContent);
            }
            catch (Exception ex)
            {
                UpdateStatus($"更新下载脚本失败: {ex.Message}", Brushes.Red);
                AppendLog($"更新下载脚本失败: {ex.Message}");
            }
        }

        private async void CopyCommand_Click(object sender, RoutedEventArgs e)
        {
            _clipboardCts?.Cancel();
            _clipboardCts = new CancellationTokenSource();
            var token = _clipboardCts.Token;

            try
            {
                AppendLog("开始复制命令到剪贴板...");
                btnCopyCommand.IsEnabled = false;
                progressBar.Value = 10;
                bool success = await SafeSetClipboard("getLessonRecordInfo", token);

                if (success)
                {
                    Dispatcher.Invoke(() =>
                    {
                        txtCurrentAction.Text = "已将 'getLessonRecordInfo' 复制到剪贴板";
                        txtStatus.Text = "请到浏览器开发者工具中执行此命令";
                        txtStatus.Foreground = Brushes.Green;
                    });
                    progressBar.Value = 100;
                    AppendLog("已成功将命令复制到剪贴板");
                }
                else
                {
                    UpdateStatus("复制失败: 请手动复制命令", Brushes.Orange);
                    progressBar.Value = 0;
                    AppendLog("复制命令失败，请手动复制");
                }
            }
            catch (OperationCanceledException)
            {
                UpdateStatus("复制操作已取消", Brushes.Orange);
                progressBar.Value = 0;
                AppendLog("复制操作被取消");
            }
            catch (Exception ex)
            {
                UpdateStatus($"复制命令失败: {ex.Message}", Brushes.Red);
                progressBar.Value = 0;
                AppendLog($"复制命令失败: {ex.Message}");
            }
            finally
            {
                btnCopyCommand.IsEnabled = true;
            }
        }

        private Task<bool> SafeSetClipboard(string text, CancellationToken token)
        {
            var tcs = new TaskCompletionSource<bool>();

            Thread staThread = new Thread(() =>
            {
                try
                {
                    token.ThrowIfCancellationRequested();
                    int retryCount = 0;
                    bool success = false;

                    while (!success && retryCount < 5)
                    {
                        try
                        {
                            if (NativeMethods.OpenClipboard(IntPtr.Zero))
                            {
                                try
                                {
                                    NativeMethods.EmptyClipboard();
                                    IntPtr hGlobal = Marshal.StringToHGlobalUni(text);
                                    IntPtr result = NativeMethods.SetClipboardData(13, hGlobal);

                                    if (result != IntPtr.Zero)
                                    {
                                        success = true;
                                        tcs.SetResult(true);
                                    }
                                    else
                                    {
                                        Marshal.FreeHGlobal(hGlobal);
                                    }
                                }
                                finally
                                {
                                    NativeMethods.CloseClipboard();
                                }
                            }

                            if (!success)
                            {
                                Thread.Sleep(100);
                                retryCount++;
                            }
                        }
                        catch (Exception ex)
                        {
                            if (retryCount >= 4)
                            {
                                tcs.SetException(ex);
                                return;
                            }

                            Thread.Sleep(100);
                            retryCount++;
                        }
                    }

                    if (!success)
                    {
                        tcs.SetResult(false);
                    }
                }
                catch (OperationCanceledException)
                {
                    tcs.SetCanceled();
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });

            staThread.SetApartmentState(ApartmentState.STA);
            staThread.IsBackground = true;
            staThread.Start();

            return tcs.Task;
        }

        private void UpdateStatus(string message, Brush color)
        {
            Dispatcher.Invoke(() =>
            {
                txtStatus.Text = message;
                txtStatus.Foreground = color;
            });
        }

        private void Parse_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                AppendLog("开始解析剪贴板内容...");

                if (!Clipboard.ContainsText())
                {
                    UpdateStatus("剪贴板中没有文本内容", Brushes.Red);
                    AppendLog("错误: 剪贴板中没有文本内容");
                    return;
                }

                txtCurrentAction.Text = "正在解析剪贴板内容...";
                progressBar.Value = 30;

                lastClipboardText = Clipboard.GetText();
                ParseContent(lastClipboardText);
            }
            catch (Exception ex)
            {
                UpdateStatus($"解析失败: {ex.Message}", Brushes.Red);
                progressBar.Value = 0;
                AppendLog($"解析失败: {ex.Message}");
            }
        }

        // 解析内容（统一处理JSON和行解析）
        private void ParseContent(string content)
        {
            try
            {
                AppendLog("尝试使用JSON解析...");
                JsonDocument doc = JsonDocument.Parse(content);
                JsonElement root = doc.RootElement;

                if (root.TryGetProperty("data", out JsonElement data))
                {
                    // 提取课程名称
                    string lessonName = data.GetProperty("lessonName").GetString() ?? string.Empty;
                    AppendLog($"添加课程题目: {Truncate(lessonName, 100)}");

                    // 提取视频URL - 只保留最后一个有效URL
                    string lastValidUrl = string.Empty;
                    if (data.TryGetProperty("lessonData", out JsonElement lessonData) &&
                        lessonData.TryGetProperty("fileList", out JsonElement fileList) &&
                        fileList.ValueKind == JsonValueKind.Array)
                    {
                        // 遍历所有文件条目
                        foreach (JsonElement file in fileList.EnumerateArray())
                        {
                            // 检查播放集
                            if (file.TryGetProperty("Playset", out JsonElement playset) &&
                                playset.ValueKind == JsonValueKind.Array)
                            {
                                // 遍历所有播放集
                                foreach (JsonElement play in playset.EnumerateArray())
                                {
                                    if (play.TryGetProperty("Url", out JsonElement urlElement))
                                    {
                                        string url = urlElement.GetString() ?? string.Empty;
                                        url = url.Replace("\\", "");

                                        if (url.Contains(".mp4", StringComparison.OrdinalIgnoreCase))
                                        {
                                            // 只保留最后一个有效URL（覆盖之前的值）
                                            lastValidUrl = url;
                                        }
                                    }
                                }
                            }
                        }
                    }

                    // 添加到视频列表 - 只保留最后一个URL
                    videoItems.Clear();
                    int videoIndex = 1;

                    if (!string.IsNullOrEmpty(lastValidUrl))
                    {
                        videoItems.Add(new VideoItem
                        {
                            Index = videoIndex,
                            Name = $"{lessonName}",
                            Url = lastValidUrl,
                            Status = "解析完成",
                            StatusColor = Brushes.Green
                        });
                        AppendLog($"添加视频链接: {Truncate(lastValidUrl, 100)}");
                    }
                    else
                    {
                        videoItems.Add(new VideoItem
                        {
                            Index = videoIndex,
                            Name = lessonName,
                            Status = "缺少MP4 URL",
                            StatusColor = Brushes.Orange
                        });
                        AppendLog("该课程缺少MP4 URL");
                    }

                    Dispatcher.Invoke(() =>
                    {
                        lstResults.ItemsSource = null;
                        lstResults.ItemsSource = videoItems;
                        txtParsedCount.Text = videoItems.Count.ToString();
                        txtTotalCount.Text = videoItems.Count.ToString();
                    });

                    if (videoItems.Count > 0)
                    {
                        UpdateStatus($"成功解析视频", Brushes.Green);
                        AppendLog("json请求头解析完成");
                        GenerateBatFile(); // 更新下载脚本
                    }
                    else
                    {
                        UpdateStatus("未找到视频信息", Brushes.Orange);
                        AppendLog("未找到视频信息");
                    }
                }
                else
                {
                    UpdateStatus("JSON格式错误: 缺少data节点", Brushes.Red);
                    AppendLog("JSON格式错误: 缺少data节点");
                    UseOriginalLineParsing(content);
                }

                progressBar.Value = 100;
                txtCurrentAction.Text = "解析完成";
            }
            catch (JsonException)
            {
                AppendLog("JSON解析失败，尝试行解析");
                UseOriginalLineParsing(content);
            }
        }

        // 使用行解析方法
        private void UseOriginalLineParsing(string content)
        {
            AppendLog("使用行解析方法...");

            // 按行分割内容
            string[] lines = content.Split(
                new[] { "\r\n", "\r", "\n" },
                StringSplitOptions.None
            );

            // 存储解析出的视频项
            videoItems.Clear();
            int videoIndex = 1;
            bool foundLessonName = false;
            string currentLessonName = string.Empty;
            string finalUrl = string.Empty; // 用于记录最后一个有效的URL
            bool playsetEncountered = false; // 是否遇到"Playset"行的标志
            bool inFileItem = false; // 是否在文件条目中

            // 优化解析逻辑：只保留最后一个有效的MP4 URL
            foreach (string line in lines)
            {
                // 查找包含 "lessonName" 的行 - 表示新课程开始
                if (line.Contains("lessonName", StringComparison.OrdinalIgnoreCase))
                {
                    currentLessonName = ExtractValue(line, "lessonName");
                    foundLessonName = true;
                }
                // 检测文件条目开始
                else if (line.Contains("{", StringComparison.OrdinalIgnoreCase) &&
                         !string.IsNullOrEmpty(currentLessonName) &&
                         !inFileItem)
                {
                    inFileItem = true;
                    playsetEncountered = false;
                }
                // 检测文件条目结束
                else if (line.Contains("}", StringComparison.OrdinalIgnoreCase) &&
                         inFileItem &&
                         !string.IsNullOrEmpty(currentLessonName))
                {
                    inFileItem = false;
                }

                // 检查是否遇到"Playset"行
                if (inFileItem && line.Contains("Playset", StringComparison.OrdinalIgnoreCase))
                {
                    playsetEncountered = true;
                }

                // 查找所有包含 "url" 的行
                if (inFileItem && line.Contains("url", StringComparison.OrdinalIgnoreCase))
                {
                    // 检查是否包含MP4
                    if (line.Contains("mp4", StringComparison.OrdinalIgnoreCase))
                    {
                        string videoUrl = ExtractValue(line, "url");
                        videoUrl = videoUrl.Replace("\\", "");

                        // 只有当遇到Playset后才记录URL
                        if (playsetEncountered)
                        {
                            // 更新为最后一个有效URL
                            finalUrl = videoUrl;
                        }
                    }
                }
            }

            // 解析完成后，添加视频项
            if (!string.IsNullOrEmpty(finalUrl)) // 确保只添加最后一个有效URL
            {
                videoItems.Add(new VideoItem
                {
                    Index = videoIndex,
                    Name = !string.IsNullOrEmpty(currentLessonName) ? currentLessonName : "未知课程",
                    Url = finalUrl,
                    Status = "解析完成",
                    StatusColor = Brushes.Green
                });
                AppendLog($"添加课程题目: {Truncate(currentLessonName, 100)}");
                AppendLog($"添加视频链接: {Truncate(finalUrl, 100)}");
            }
            else if (!string.IsNullOrEmpty(currentLessonName))
            {
                videoItems.Add(new VideoItem
                {
                    Index = videoIndex,
                    Name = currentLessonName,
                    Status = "缺少MP4 URL",
                    StatusColor = Brushes.Orange
                });
                AppendLog($"添加课程题目: {Truncate(currentLessonName, 100)}");
                AppendLog("该课程缺少MP4 URL");
            }

            // 更新UI显示结果
            Dispatcher.Invoke(() =>
            {
                lstResults.ItemsSource = null;
                lstResults.ItemsSource = videoItems;
                txtParsedCount.Text = videoItems.Count.ToString();
                txtTotalCount.Text = videoItems.Count.ToString();
            });

            // 更新状态和日志
            if (videoItems.Count > 0)
            {
                UpdateStatus($"成功解析视频", Brushes.Green);
                AppendLog("json请求头解析完成");
                GenerateBatFile(); // 更新下载脚本
            }
            else
            {
                string status = foundLessonName
                    ? "未找到完整的视频信息"
                    : "未找到关键信息";

                UpdateStatus(status, Brushes.Orange);
                AppendLog(status);
            }

            progressBar.Value = 100;
            txtCurrentAction.Text = "解析完成";
        }

        // 从JSON行中提取值的辅助方法
        private string ExtractValue(string jsonLine, string key)
        {
            try
            {
                int keyIndex = jsonLine.IndexOf(key, StringComparison.OrdinalIgnoreCase);
                if (keyIndex < 0) return string.Empty;

                int colonIndex = jsonLine.IndexOf(':', keyIndex + key.Length);
                if (colonIndex < 0) return string.Empty;

                int startIndex = colonIndex + 1;
                while (startIndex < jsonLine.Length && char.IsWhiteSpace(jsonLine[startIndex]))
                    startIndex++;

                int endIndex = startIndex;
                if (startIndex < jsonLine.Length)
                {
                    char startChar = jsonLine[startIndex];
                    char endChar = startChar == '"' ? '"' : ',';

                    if (startChar == '"')
                        endIndex = jsonLine.IndexOf(endChar, startIndex + 1);
                    else
                        endIndex = jsonLine.IndexOfAny(new[] { ',', '}', ']' }, startIndex);
                }

                if (endIndex < 0) endIndex = jsonLine.Length;

                string value = jsonLine.Substring(startIndex, endIndex - startIndex)
                    .Trim()
                    .Trim('"', '\'', ',', ' ');

                return value;
            }
            catch
            {
                return string.Empty;
            }
        }

        private void Download_Click(object sender, RoutedEventArgs e)
        {
            if (videoItems.Count == 0)
            {
                UpdateStatus("没有可下载的视频", Brushes.Orange);
                AppendLog("错误: 没有可下载的视频");
                return;
            }

            try
            {
                // 清除之前的下载输出
                txtDownloadOutput.Clear();

                // 切换到下载输出选项卡
                var tabControl = FindParentTabControl(txtDownloadOutput);
                if (tabControl != null && tabControl.Items.Count > 1)
                {
                    tabControl.SelectedIndex = 1;
                }

                txtCurrentAction.Text = "正在启动下载...";
                progressBar.Value = 50;
                AppendLog("开始下载视频...");
                AppendDownloadOutput("开始下载视频...");

                if (!File.Exists("aria2c.exe"))
                {
                    UpdateStatus("错误: 未找到aria2c.exe", Brushes.Red);
                    AppendLog("错误: 未找到aria2c.exe");
                    AppendDownloadOutput("错误: 未找到aria2c.exe");
                    return;
                }

                // 创建取消令牌
                _downloadCts?.Cancel();
                _downloadCts = new CancellationTokenSource();

                // 启动下载任务
                Task.Run(() => DownloadVideosAsync(_downloadCts.Token), _downloadCts.Token);
            }
            catch (Exception ex)
            {
                UpdateStatus($"启动下载失败: {ex.Message}", Brushes.Red);
                progressBar.Value = 0;
                AppendLog($"启动下载失败: {ex.Message}");
                AppendDownloadOutput($"启动下载失败: {ex.Message}");
            }
        }

        // 单个视频下载按钮事件
        private void DownloadSingle_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is VideoItem item)
            {
                if (item.IsDownloading)
                {
                    AppendLog($"视频 '{item.Name}' 已在下载中");
                    return;
                }

                try
                {
                    // 清除之前的下载输出
                    txtDownloadOutput.Clear();

                    // 切换到下载输出选项卡
                    var tabControl = FindParentTabControl(txtDownloadOutput);
                    if (tabControl != null && tabControl.Items.Count > 1)
                    {
                        tabControl.SelectedIndex = 1;
                    }

                    txtCurrentAction.Text = $"正在下载: {Truncate(item.Name, 20)}";
                    AppendLog($"开始下载单个视频: {item.Name}");
                    AppendDownloadOutput($"开始下载单个视频: {item.Name}");

                    if (!File.Exists("aria2c.exe"))
                    {
                        UpdateStatus("错误: 未找到aria2c.exe", Brushes.Red);
                        AppendLog("错误: 未找到aria2c.exe");
                        AppendDownloadOutput("错误: 未找到aria2c.exe");
                        return;
                    }

                    // 创建取消令牌
                    _downloadCts?.Cancel();
                    _downloadCts = new CancellationTokenSource();

                    // 启动单个视频下载
                    Task.Run(() => DownloadSingleVideoAsync(item, _downloadCts.Token), _downloadCts.Token);
                }
                catch (Exception ex)
                {
                    UpdateStatus($"启动下载失败: {ex.Message}", Brushes.Red);
                    AppendLog($"启动下载失败: {ex.Message}");
                    AppendDownloadOutput($"启动下载失败: {ex.Message}");
                }
            }
        }

        private async Task DownloadVideosAsync(CancellationToken token)
        {
            try
            {
                // 更新状态
                Dispatcher.Invoke(() =>
                {
                    txtCurrentAction.Text = "下载进行中...";
                    btnDownload.IsEnabled = false;
                    btnParse.IsEnabled = false;
                });

                AppendDownloadOutput($"开始下载 {videoItems.Count} 个视频");
                AppendDownloadOutput("----------------------------------------");

                int successCount = 0;
                int failedCount = 0;

                foreach (var item in videoItems)
                {
                    if (token.IsCancellationRequested)
                    {
                        AppendDownloadOutput("下载已被用户取消");

                        // 更新状态为"等待下载"
                        Dispatcher.Invoke(() =>
                        {
                            item.Status = "等待下载";
                            item.Progress = 0; // 重置进度
                            item.StatusColor = Brushes.Orange;
                            item.IsDownloading = false;
                            lstResults.Items.Refresh();
                        });
                        break;
                    }

                    // 跳过已下载完成的视频
                    if (item.Status == "下载完成")
                    {
                        AppendDownloadOutput($"跳过已下载视频: {item.Name}");
                        continue;
                    }

                    // 更新状态为"下载中..." - 重置进度
                    Dispatcher.Invoke(() =>
                    {
                        item.Status = "下载中...";
                        item.Progress = 0; // 重置进度
                        item.DownloadedSize = "0 B";
                        item.TotalSize = "0 B";
                        item.CurrentSpeed = "0 MB/s";
                        item.RemainingTime = "未知";
                        item.StatusColor = Brushes.Orange;
                        item.IsDownloading = true;
                        lstResults.Items.Refresh();
                    });

                    // 添加短暂延迟让UI更新
                    await Task.Delay(10, token);

                    // 下载单个视频
                    bool result = await DownloadSingleVideoAsync(item, token);

                    if (result)
                    {
                        successCount++;
                    }
                    else
                    {
                        failedCount++;
                    }
                }

                // 更新状态
                AppendDownloadOutput("----------------------------------------");
                AppendDownloadOutput($"下载完成: 成功 {successCount} 个, 失败 {failedCount} 个");

                Dispatcher.Invoke(() =>
                {
                    progressBar.Value = 100;
                    txtCurrentAction.Text = "下载完成";
                    btnDownload.IsEnabled = true;
                    btnParse.IsEnabled = true;

                    if (failedCount == 0)
                    {
                        UpdateStatus("所有视频下载完成", Brushes.Green);
                    }
                    else
                    {
                        UpdateStatus($"下载完成，但有{failedCount}个失败", Brushes.Orange);
                    }
                });
            }
            catch (OperationCanceledException)
            {
                AppendDownloadOutput("下载已被用户取消");
                Dispatcher.Invoke(() =>
                {
                    UpdateStatus("下载已取消", Brushes.Orange);
                    btnDownload.IsEnabled = true;
                    btnParse.IsEnabled = true;

                    // 将所有下载中的视频状态重置为"等待下载"
                    foreach (var item in videoItems.Where(v => v.Status == "下载中..."))
                    {
                        item.Status = "等待下载";
                        item.Progress = 0; // 重置进度
                        item.StatusColor = Brushes.Orange;
                        item.IsDownloading = false;
                    }
                    lstResults.Items.Refresh();
                });
            }
            catch (Exception ex)
            {
                AppendDownloadOutput($"下载过程中出错: {ex.Message}");
                Dispatcher.Invoke(() =>
                {
                    UpdateStatus($"下载失败: {ex.Message}", Brushes.Red);
                    progressBar.Value = 0;
                    btnDownload.IsEnabled = true;
                    btnParse.IsEnabled = true;

                    // 将所有下载中的视频状态重置为"等待下载"
                    foreach (var item in videoItems.Where(v => v.Status == "下载中..."))
                    {
                        item.Status = "等待下载";
                        item.Progress = 0; // 重置进度
                        item.StatusColor = Brushes.Orange;
                        item.IsDownloading = false;
                    }
                    lstResults.Items.Refresh();
                });
            }
        }

        private async Task<bool> DownloadSingleVideoAsync(VideoItem item, CancellationToken token)
        {
            try
            {
                if (string.IsNullOrEmpty(item.Url))
                {
                    AppendDownloadOutput($"跳过下载: {item.Name} (无有效URL)");
                    Dispatcher.Invoke(() =>
                    {
                        item.Status = "下载失败: 无URL";
                        item.Progress = 0;
                        item.StatusColor = Brushes.Red;
                        item.IsDownloading = false;
                        lstResults.Items.Refresh();
                    });
                    return false;
                }

                // 更新状态为"下载中..." - 添加UI刷新
                Dispatcher.Invoke(() =>
                {
                    item.Status = "下载中...";
                    item.Progress = 0; // 重置进度
                    item.DownloadedSize = "0 B";
                    item.TotalSize = "0 B";
                    item.CurrentSpeed = "0 MB/s";
                    item.RemainingTime = "未知";
                    item.StatusColor = Brushes.Orange;
                    item.IsDownloading = true;
                    lstResults.Items.Refresh();
                });

                // 添加短暂延迟让UI更新
                await Task.Delay(10, token);

                string safeName = CleanName(item.Name);
                string outputFile = Path.Combine(downloadPath, $"{safeName}.mp4");

                AppendDownloadOutput($"开始下载: {safeName}");
                AppendDownloadOutput($"URL: {Truncate(item.Url, 100)}");
                AppendDownloadOutput($"保存到: {outputFile}");

                // 创建下载进程
                var startInfo = new ProcessStartInfo
                {
                    FileName = "aria2c.exe",
                    Arguments = $"-d \"{downloadPath}\" -o \"{safeName}.mp4\" \"{item.Url}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                using (var process = new Process { StartInfo = startInfo })
                {
                    // 设置输出处理 - 添加进度解析
                    process.OutputDataReceived += (s, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            AppendDownloadOutput(e.Data);

                            // 解析下载进度信息
                            ParseDownloadProgress(e.Data, item);
                        }
                    };

                    process.ErrorDataReceived += (s, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            AppendDownloadOutput($"错误: {e.Data}");
                        }
                    };

                    // 启动进程
                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    // 等待下载完成
                    await process.WaitForExitAsync(token);

                    if (process.ExitCode == 0)
                    {
                        AppendDownloadOutput($"下载完成: {safeName}");

                        // 更新UI中的状态
                        Dispatcher.Invoke(() =>
                        {
                            item.Status = "下载完成";
                            item.Progress = 100;
                            item.StatusColor = Brushes.Green;
                            item.IsDownloading = false;
                            lstResults.Items.Refresh();
                        });
                        return true;
                    }
                    else
                    {
                        AppendDownloadOutput($"下载失败: {safeName} (退出代码: {process.ExitCode})");

                        Dispatcher.Invoke(() =>
                        {
                            item.Status = "下载失败";
                            item.Progress = 0;
                            item.StatusColor = Brushes.Red;
                            item.IsDownloading = false;
                            lstResults.Items.Refresh();
                        });
                        return false;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                AppendDownloadOutput($"下载取消: {item.Name}");
                Dispatcher.Invoke(() =>
                {
                    item.Status = "等待下载";
                    item.Progress = 0;
                    item.StatusColor = Brushes.Orange;
                    item.IsDownloading = false;
                    lstResults.Items.Refresh();
                });
                return false;
            }
            catch (Exception ex)
            {
                AppendDownloadOutput($"下载过程中出错: {item.Name} - {ex.Message}");
                Dispatcher.Invoke(() =>
                {
                    item.Status = "下载失败";
                    item.Progress = 0;
                    item.StatusColor = Brushes.Red;
                    item.IsDownloading = false;
                    lstResults.Items.Refresh();
                });
                return false;
            }
        }

        // 解析下载进度信息
        private void ParseDownloadProgress(string data, VideoItem item)
        {
            try
            {
                // 示例: [#2089b8 192KiB/1.1MiB(16%) CN:1 DL:115KiB ETA:8s]
                string pattern = @"\[\#\w+\s+([\d.]+\w*B)/([\d.]+\w*B)\((\d+)%\)\s+CN:\d+\s+DL:([\d.]+\w*B)\s+ETA:([\d]+\w*)\]";
                var match = Regex.Match(data, pattern);

                if (match.Success && match.Groups.Count == 6)
                {
                    string downloadedSize = match.Groups[1].Value;
                    string totalSize = match.Groups[2].Value;
                    int progress = int.Parse(match.Groups[3].Value);
                    string currentSpeed = match.Groups[4].Value;
                    string remainingTime = match.Groups[5].Value;

                    // 转换速度为MB/s
                    string speedInMBps = ConvertToMBps(currentSpeed);

                    // 转换下载大小为十进制单位 (MB/GB)
                    downloadedSize = ConvertToDecimalSize(downloadedSize);
                    totalSize = ConvertToDecimalSize(totalSize);

                    Dispatcher.Invoke(() =>
                    {
                        item.DownloadedSize = downloadedSize;
                        item.TotalSize = totalSize;
                        item.Progress = progress;
                        item.CurrentSpeed = speedInMBps;
                        item.RemainingTime = remainingTime;
                    });
                }
                else
                {
                    // 尝试匹配进度百分比
                    if (data.Contains("%"))
                    {
                        var progressMatch = Regex.Match(data, @"\((\d+)%\)");
                        if (progressMatch.Success && progressMatch.Groups.Count > 1)
                        {
                            if (int.TryParse(progressMatch.Groups[1].Value, out int progress))
                            {
                                Dispatcher.Invoke(() => item.Progress = progress);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AppendDownloadOutput($"解析进度失败: {ex.Message}");
            }
        }

        // 将速度转换为MB/s
        private string ConvertToMBps(string speedWithUnit)
        {
            if (string.IsNullOrEmpty(speedWithUnit))
                return "0 MB/s";

            // 使用正则表达式分离数字和单位
            Match match = Regex.Match(speedWithUnit, @"([\d.]+)(\w*)");
            if (!match.Success || match.Groups.Count < 3)
                return "0 MB/s";

            double value = double.Parse(match.Groups[1].Value);
            string unit = match.Groups[2].Value.ToUpper();

            double valueInMBps = 0;

            // 转换为字节
            switch (unit)
            {
                case "B":
                    valueInMBps = value / 1000000.0;
                    break;
                case "KIB":
                    valueInMBps = value * 1024 / 1000000.0;
                    break;
                case "MIB":
                    valueInMBps = value * 1048576 / 1000000.0;
                    break;
                case "GIB":
                    valueInMBps = value * 1073741824 / 1000000000.0;
                    break;
                default:
                    // 如果单位未知，默认按字节处理
                    valueInMBps = value / 1000000.0;
                    break;
            }

            return $"{valueInMBps:0.00} MB/s";
        }

        // 将大小转换为十进制单位 (MB/GB)
        private string ConvertToDecimalSize(string sizeWithUnit)
        {
            if (string.IsNullOrEmpty(sizeWithUnit))
                return "0 B";

            // 使用正则表达式分离数字和单位
            Match match = Regex.Match(sizeWithUnit, @"([\d.]+)(\w*)");
            if (!match.Success || match.Groups.Count < 3)
                return sizeWithUnit;

            double value = double.Parse(match.Groups[1].Value);
            string unit = match.Groups[2].Value.ToUpper();

            double bytes = 0;

            // 转换为字节
            switch (unit)
            {
                case "B":
                    bytes = value;
                    break;
                case "KIB":
                    bytes = value * 1024;
                    break;
                case "MIB":
                    bytes = value * 1048576;
                    break;
                case "GIB":
                    bytes = value * 1073741824;
                    break;
                default:
                    // 如果单位未知，返回原字符串
                    return sizeWithUnit;
            }

            // 转换为十进制单位
            if (bytes >= 1000000000) // 1 GB = 1000000000 bytes
            {
                return $"{bytes / 1000000000:0.00} GB";
            }
            else if (bytes >= 1000000) // 1 MB = 1000000 bytes
            {
                return $"{bytes / 1000000:0.00} MB";
            }
            else if (bytes >= 1000) // 1 KB = 1000 bytes
            {
                return $"{bytes / 1000:0.00} KB";
            }
            else
            {
                return $"{bytes:0} B";
            }
        }

        private TabControl? FindParentTabControl(DependencyObject child)
        {
            DependencyObject parent = VisualTreeHelper.GetParent(child);
            while (parent != null && !(parent is TabControl))
            {
                parent = VisualTreeHelper.GetParent(parent);
            }
            return parent as TabControl;
        }

        private void OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            AppendLog("尝试打开下载目录...");

            try
            {
                if (Directory.Exists(downloadPath))
                {
                    Process.Start("explorer.exe", downloadPath);
                    AppendLog($"已打开目录: {downloadPath}");
                }
                else
                {
                    UpdateStatus("下载目录不存在", Brushes.Orange);
                    AppendLog("错误: 下载目录不存在");
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"无法打开目录: {ex.Message}", Brushes.Red);
                AppendLog($"无法打开目录: {ex.Message}");
            }
        }

        private void ChangeDownloadPath_Click(object sender, RoutedEventArgs e)
        {
            AppendLog("更改下载目录...");

            try
            {
                var dialog = new OpenFolderDialog
                {
                    Title = "选择下载目录",
                    Multiselect = false,
                    InitialDirectory = Directory.Exists(downloadPath) ? downloadPath : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                };

                if (dialog.ShowDialog() == true)
                {
                    string newPath = dialog.FolderName;
                    if (!string.IsNullOrWhiteSpace(newPath))
                    {
                        downloadPath = newPath;
                        UpdateDownloadPathDisplay();
                        GenerateBatFile();
                        UpdateStatus($"下载路径已更新: {newPath}", Brushes.Green);
                        AppendLog($"下载目录已更改为: {newPath}");
                    }
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"更改目录失败: {ex.Message}", Brushes.Red);
                AppendLog($"更改目录失败: {ex.Message}");
            }
        }

        private async void CopyRawData_Click(object sender, RoutedEventArgs e)
        {
            AppendLog("复制原始数据到剪贴板...");

            if (string.IsNullOrEmpty(lastClipboardText))
            {
                UpdateStatus("没有可复制的原始数据", Brushes.Orange);
                AppendLog("错误: 没有可复制的原始数据");
                return;
            }

            try
            {
                bool success = await SafeSetClipboard(lastClipboardText, CancellationToken.None);
                if (success)
                {
                    UpdateStatus("原始数据已复制到剪贴板", Brushes.Green);
                    AppendLog("原始数据已成功复制到剪贴板");
                }
                else
                {
                    UpdateStatus("复制原始数据失败", Brushes.Red);
                    AppendLog("复制原始数据失败");
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"复制原始数据失败: {ex.Message}", Brushes.Red);
                AppendLog($"复制原始数据失败: {ex.Message}");
            }
        }

        private async void CopyUrl_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string url)
            {
                try
                {
                    AppendLog($"尝试复制URL: {Truncate(url, 50)}");
                    bool success = await SafeSetClipboard(url, CancellationToken.None);
                    if (success)
                    {
                        UpdateStatus("视频URL已复制到剪贴板", Brushes.Green);
                    }
                    else
                    {
                        UpdateStatus("复制URL失败，请重试", Brushes.Orange);
                    }
                }
                catch (Exception ex)
                {
                    UpdateStatus($"复制URL失败: {ex.Message}", Brushes.Red);
                }
            }
        }

        private void ClearLog_Click(object sender, RoutedEventArgs e)
        {
            txtLog.Clear();
            AppendLog("日志已清空");
        }

        // 添加日志方法
        private void AppendLog(string message)
        {
            Dispatcher.Invoke(() =>
            {
                // 添加新日志
                txtLog.AppendText($"{DateTime.Now:HH:mm:ss} - {message}\n");

                // 确保滚动到最新内容
                txtLog.CaretIndex = txtLog.Text.Length;
                txtLog.ScrollToEnd();

                // 强制UI更新
                txtLog.UpdateLayout();
                logScrollViewer.ScrollToEnd();
            });
        }

        // 添加下载输出方法
        private void AppendDownloadOutput(string message)
        {
            Dispatcher.Invoke(() =>
            {
                // 添加新日志
                txtDownloadOutput.AppendText($"{message}\n");

                // 确保滚动到最新内容
                txtDownloadOutput.CaretIndex = txtDownloadOutput.Text.Length;
                txtDownloadOutput.ScrollToEnd();

                // 强制UI更新
                txtDownloadOutput.UpdateLayout();
                downloadOutputScrollViewer.ScrollToEnd();
            });
        }

        // 截断长文本
        private string Truncate(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text)) return text;
            return text.Length <= maxLength ? text : text.Substring(0, maxLength) + "...";
        }

        // 清理文件名
        private string CleanName(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;

            // 清理文件名中的非法字符
            char[] invalidChars = Path.GetInvalidFileNameChars();
            foreach (char c in invalidChars)
            {
                name = name.Replace(c.ToString(), "");
            }

            // 移除特殊字符
            name = name.Replace(":", "")
                     .Replace("?", "")
                     .Replace("*", "")
                     .Replace("|", "")
                     .Replace("<", "")
                     .Replace(">", "")
                     .Replace("\"", "");

            return name;
        }
    }

    // 添加Process扩展方法用于异步等待
    public static class ProcessExtensions
    {
        public static Task WaitForExitAsync(this Process process,
                                            CancellationToken cancellationToken = default)
        {
            var tcs = new TaskCompletionSource<object?>();
            process.EnableRaisingEvents = true;
            process.Exited += (sender, args) => tcs.TrySetResult(null);

            if (cancellationToken != default)
            {
                cancellationToken.Register(() =>
                {
                    tcs.TrySetCanceled();
                });
            }

            return tcs.Task;
        }
    }

    internal static class NativeMethods
    {
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool OpenClipboard(IntPtr hWndNewOwner);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CloseClipboard();

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool EmptyClipboard();

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);
    }
}

namespace VideoDownloader.Converters
{
    public class DarkenColorConverter : IValueConverter
    {
        public double DarkenFactor { get; set; } = 0.3; // 默认变暗30%

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is SolidColorBrush brush)
            {
                Color color = brush.Color;

                // 计算变暗后的颜色
                byte r = (byte)(color.R * (1 - DarkenFactor));
                byte g = (byte)(color.G * (1 - DarkenFactor));
                byte b = (byte)(color.B * (1 - DarkenFactor));

                return new SolidColorBrush(Color.FromRgb(r, g, b));
            }

            // 如果无法处理，返回原始值
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}