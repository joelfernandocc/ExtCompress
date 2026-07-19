using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ExtCompress;

public class ProgressForm : Form
{
    private Label lblPercentage;
    private Label lblOperation;
    private PictureBox progressBarBox;
    private Label lblSpeed;
    private Label lblTimeRemaining;
    private Label lblItemsRemaining;
    private PictureBox graphBox;
    private Button btnCancel;
    private CancellationTokenSource _cts;

    private List<double> _speedHistory = new List<double>();
    private double _maxSpeed = 1.0;
    private double _percentage = 0.0;
    private bool _isCompleted = false;

    private long _currentBytesProcessed = 0;
    private long _totalSize = 0;
    private Stopwatch _sw;
    private System.Windows.Forms.Timer _uiTimer;
    
    private Color bgColor = Color.FromArgb(32, 32, 32);
    private Color fgColor = Color.White;
    private Color secondaryColor = Color.FromArgb(180, 180, 180);
    private Color accentColor = Color.FromArgb(76, 194, 255); // Win11 light blue

    public ProgressForm()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        this.Text = LocalizationManager.Get("UI_Title");
        this.Size = new Size(500, 360);
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = true;
        this.StartPosition = FormStartPosition.CenterScreen;
        this.BackColor = bgColor;
        this.Font = new Font("Segoe UI Variable Display", 9.75F, FontStyle.Regular, GraphicsUnit.Point, 0);
        this.AllowDrop = false;
        this.DoubleBuffered = true;

        _uiTimer = new System.Windows.Forms.Timer();
        _uiTimer.Interval = 16; // ~60 FPS
        _uiTimer.Tick += UiTimer_Tick;

        lblPercentage = new Label
        {
            AutoSize = true,
            Location = new Point(20, 15),
            Font = new Font("Segoe UI Variable Display", 22F, FontStyle.Regular, GraphicsUnit.Point, 0),
            ForeColor = fgColor,
            Text = LocalizationManager.Get("UI_Completed", "0")
        };

        lblOperation = new Label
        {
            AutoSize = true,
            Location = new Point(25, 60),
            Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point, 0),
            ForeColor = secondaryColor,
            Text = LocalizationManager.Get("UI_Calculating")
        };

        progressBarBox = new PictureBox
        {
            Location = new Point(25, 85),
            Size = new Size(435, 6),
            BackColor = bgColor
        };
        progressBarBox.Paint += ProgressBarBox_Paint;

        graphBox = new PictureBox
        {
            Location = new Point(25, 110),
            Size = new Size(435, 100),
            BackColor = bgColor,
            BorderStyle = BorderStyle.None
        };
        graphBox.Paint += GraphBox_Paint;

        lblSpeed = new Label
        {
            AutoSize = true,
            Location = new Point(25, 225),
            Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point, 0),
            ForeColor = secondaryColor,
            Text = LocalizationManager.Get("UI_Speed", "0 MB/s")
        };

        lblTimeRemaining = new Label
        {
            AutoSize = true,
            Location = new Point(25, 245),
            Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point, 0),
            ForeColor = secondaryColor,
            Text = LocalizationManager.Get("UI_TimeRemaining", LocalizationManager.Get("UI_Calculating"))
        };

        lblItemsRemaining = new Label
        {
            AutoSize = true,
            Location = new Point(25, 265),
            Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point, 0),
            ForeColor = secondaryColor,
            Text = LocalizationManager.Get("UI_ItemsRemaining", LocalizationManager.Get("UI_Calculating"))
        };

        btnCancel = new Button
        {
            Location = new Point(385, 280),
            Size = new Size(75, 28),
            Text = LocalizationManager.Get("UI_Cancel"),
            ForeColor = fgColor,
            BackColor = Color.FromArgb(50, 50, 50),
            FlatStyle = FlatStyle.Flat,
            UseVisualStyleBackColor = false
        };
        btnCancel.FlatAppearance.BorderSize = 1;
        btnCancel.FlatAppearance.BorderColor = Color.FromArgb(80, 80, 80);
        btnCancel.Click += BtnCancel_Click;

        this.Controls.Add(lblPercentage);
        this.Controls.Add(lblOperation);
        this.Controls.Add(progressBarBox);
        this.Controls.Add(graphBox);
        this.Controls.Add(lblSpeed);
        this.Controls.Add(lblTimeRemaining);
        this.Controls.Add(lblItemsRemaining);
        this.Controls.Add(btnCancel);
    }

    private void ProgressBarBox_Paint(object sender, PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        Rectangle rect = new Rectangle(0, 0, progressBarBox.Width, progressBarBox.Height);
        
        using (SolidBrush trackBrush = new SolidBrush(Color.FromArgb(60, 60, 60)))
        {
            g.FillRectangle(trackBrush, rect);
        }
        
        int fillWidth = (int)(progressBarBox.Width * (_percentage / 100.0));
        if (fillWidth > 0)
        {
            Rectangle fillRect = new Rectangle(0, 0, fillWidth, progressBarBox.Height);
            using (SolidBrush fillBrush = new SolidBrush(accentColor))
            {
                g.FillRectangle(fillBrush, fillRect);
            }
        }
    }

    private void GraphBox_Paint(object sender, PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        float width = graphBox.Width;
        float height = graphBox.Height;

        g.FillRectangle(new SolidBrush(Color.FromArgb(40, 40, 40)), 0, 0, width, height);
        g.DrawRectangle(new Pen(Color.FromArgb(60, 60, 60)), 0, 0, width - 1, height - 1);

        Pen gridPen = new Pen(Color.FromArgb(60, 60, 60));
        for (int i = 1; i < 4; i++)
        {
            float y = height * i / 4f;
            g.DrawLine(gridPen, 0, y, width, y);
        }
        for (int i = 1; i < 6; i++)
        {
            float x = width * i / 6f;
            g.DrawLine(gridPen, x, 0, x, height);
        }

        if (_speedHistory.Count < 2) return;

        PointF[] points = new PointF[_speedHistory.Count];
        float stepX = width / Math.Max(1, _speedHistory.Count - 1);
        
        double maxSpeed = Math.Max(1.0, _maxSpeed);

        for (int i = 0; i < _speedHistory.Count; i++)
        {
            float x = i * stepX;
            float y = height - (float)((_speedHistory[i] / maxSpeed) * height);
            y = Math.Max(0, Math.Min(height, y));
            points[i] = new PointF(x, y);
        }

        PointF[] fillPoints = new PointF[_speedHistory.Count + 2];
        Array.Copy(points, fillPoints, _speedHistory.Count);
        fillPoints[_speedHistory.Count] = new PointF(points[_speedHistory.Count - 1].X, height);
        fillPoints[_speedHistory.Count + 1] = new PointF(points[0].X, height);

        using (LinearGradientBrush brush = new LinearGradientBrush(new RectangleF(0, 0, width, height), Color.FromArgb(120, accentColor), Color.FromArgb(10, accentColor), LinearGradientMode.Vertical))
        {
            g.FillPolygon(brush, fillPoints);
        }

        using (Pen linePen = new Pen(accentColor, 2f))
        {
            g.DrawLines(linePen, points);
        }
    }

    private void BtnCancel_Click(object sender, EventArgs e)
    {
        if (_isCompleted)
        {
            this.Close();
            return;
        }

        if (_cts != null && !_cts.IsCancellationRequested)
        {
            _cts.Cancel();
            btnCancel.Enabled = false;
            lblPercentage.Text = LocalizationManager.Get("UI_Canceling");
        }
        else
        {
            this.Close();
        }
    }

    public async void RunCompression(string[] files, string outputPath, ExtCompressOptions options)
    {
        _cts = new CancellationTokenSource();
        _totalSize = CalculateTotalSize(files);
        _sw = Stopwatch.StartNew();
        _currentBytesProcessed = 0;
        
        lblOperation.Text = LocalizationManager.Get("UI_CompressingTo", Path.GetFileName(outputPath));
        _uiTimer.Start();
        
        var progress = new Progress<long>(bytesProcessed => 
        {
            Interlocked.Exchange(ref _currentBytesProcessed, bytesProcessed);
        });

        try
        {
            await ExtCompressEngine.CompressAsync(files, outputPath, options, progress, _cts.Token);
            _sw.Stop();
            FinishSuccess(LocalizationManager.Get("UI_Compression"), outputPath, _totalSize, _sw);
        }
        catch (OperationCanceledException)
        {
            try { if (File.Exists(outputPath)) File.Delete(outputPath); } catch { }
            HandleCancel();
        }
        catch (Exception ex)
        {
            try { if (File.Exists(outputPath)) File.Delete(outputPath); } catch { }
            HandleError(ex);
        }
    }

    public async void RunDecompression(string inputPath, string outputDir, string oldRootName, string newRootName, ExtCompressOptions options = null)
    {
        if (options == null) options = new ExtCompressOptions();
        _cts = new CancellationTokenSource();
        _totalSize = new FileInfo(inputPath).Length;
        _sw = Stopwatch.StartNew();
        _currentBytesProcessed = 0;

        lblOperation.Text = LocalizationManager.Get("UI_ExtractingTo", Path.GetFileName(outputDir));
        _uiTimer.Start();

        var progress = new Progress<long>(bytesProcessed => 
        {
            Interlocked.Exchange(ref _currentBytesProcessed, bytesProcessed);
        });

        try
        {
            await ExtCompressEngine.DecompressAsync(inputPath, outputDir, oldRootName, newRootName, options, progress, _cts.Token);
            _sw.Stop();
            string finalHighlightPath = outputDir;
            if (!string.IsNullOrEmpty(oldRootName))
            {
                finalHighlightPath = Path.Combine(outputDir, newRootName ?? oldRootName);
            }
            FinishSuccess(LocalizationManager.Get("UI_Extraction"), finalHighlightPath, _totalSize, _sw);
        }
        catch (OperationCanceledException)
        {
            HandleCancel();
        }
        catch (Exception ex)
        {
            HandleError(ex);
        }
    }

    private void UiTimer_Tick(object sender, EventArgs e)
    {
        if (_isCompleted) { _uiTimer.Stop(); return; }
        long bytesProcessed = Interlocked.Read(ref _currentBytesProcessed);
        UpdateMetrics(bytesProcessed, _totalSize, _sw);
    }

    private void UpdateMetrics(long bytesProcessed, long totalSize, Stopwatch sw)
    {
        if (totalSize <= 0) return;
        
        _percentage = (bytesProcessed * 100.0) / totalSize;
        double elapsedSeconds = sw.Elapsed.TotalSeconds;
        double mbPerSecond = (bytesProcessed / 1024.0 / 1024.0) / (elapsedSeconds > 0 ? elapsedSeconds : 1);
        
        double remainingBytes = totalSize - bytesProcessed;
        double secondsRemaining = mbPerSecond > 0 ? (remainingBytes / 1024.0 / 1024.0) / mbPerSecond : 0;
        TimeSpan eta = TimeSpan.FromSeconds(Math.Max(0, secondsRemaining));

        string FormatSize(double bytes)
        {
            if (bytes >= 1024 * 1024 * 1024) return $"{(bytes / 1024 / 1024 / 1024):F2} GB";
            if (bytes >= 1024 * 1024) return $"{(bytes / 1024 / 1024):F2} MB";
            return $"{(bytes / 1024):F2} KB";
        }

        string FormatSpeed(double speedMb)
        {
            if (speedMb >= 1024) return $"{(speedMb / 1024):F2} GB/s";
            return $"{speedMb:F1} MB/s";
        }

        lblPercentage.Text = LocalizationManager.Get("UI_Completed", $"{Math.Min(100.0, Math.Max(0.0, _percentage)):F2}");
        progressBarBox.Invalidate();
        
        string timeString = eta.TotalHours >= 1 ? LocalizationManager.Get("UI_TimeFormat_HM", (int)eta.TotalHours, eta.Minutes)
                          : eta.TotalMinutes >= 1 ? LocalizationManager.Get("UI_TimeFormat_MS", eta.Minutes, eta.Seconds)
                          : LocalizationManager.Get("UI_TimeFormat_S", eta.Seconds);
        
        lblSpeed.Text = LocalizationManager.Get("UI_Speed", FormatSpeed(mbPerSecond));
        lblTimeRemaining.Text = LocalizationManager.Get("UI_TimeRemaining", timeString);
        lblItemsRemaining.Text = LocalizationManager.Get("UI_ItemsRemaining", FormatSize(remainingBytes));
        
        _speedHistory.Add(mbPerSecond);
        if (_speedHistory.Count > 100) _speedHistory.RemoveAt(0);
        if (mbPerSecond > _maxSpeed) _maxSpeed = mbPerSecond * 1.2;
        
        graphBox.Invalidate();
    }

    private void FinishSuccess(string mode, string path, long size, Stopwatch sw)
    {
        _isCompleted = true;
        MethodInvoker action = delegate
        {
            try 
            { 
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{path}\"",
                    UseShellExecute = true
                });
            } 
            catch { }
            this.Close();
        };

        if (this.InvokeRequired)
            this.BeginInvoke(action);
        else
            action();
    }

    private void HandleCancel()
    {
        _isCompleted = true;
        MethodInvoker action = delegate
        {
            this.Close();
        };

        if (this.InvokeRequired)
            this.BeginInvoke(action);
        else
            action();
    }

    private void HandleError(Exception ex)
    {
        _isCompleted = true;
        MethodInvoker action = delegate
        {
            lblPercentage.Text = LocalizationManager.Get("UI_Error");
            lblPercentage.ForeColor = Color.Red;
            lblOperation.Text = ex.Message;
            btnCancel.Text = LocalizationManager.Get("UI_Close");
            btnCancel.Enabled = true;
        };

        if (this.InvokeRequired)
            this.BeginInvoke(action);
        else
            action();
    }

    private long CalculateTotalSize(string[] paths)
    {
        long size = 0;
        foreach (var path in paths)
        {
            if (File.GetAttributes(path).HasFlag(FileAttributes.Directory))
            {
                try {
                    size += Directory.EnumerateFiles(path, "*.*", SearchOption.AllDirectories).Sum(f => new FileInfo(f).Length);
                } catch { } 
            }
            else
            {
                size += new FileInfo(path).Length;
            }
        }
        return size;
    }
}
