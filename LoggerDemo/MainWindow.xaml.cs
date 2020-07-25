using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows;

namespace LoggerDemo
{
  /// <summary>
  /// Interaction logic for MainWindow.xaml
  /// </summary>
  public partial class MainWindow : Window
  {
    public MainWindow()
    {
      InitializeComponent();
    }
    private void Button_Click(object sender, RoutedEventArgs e)
    {
      for (int i = 0; i < 1000; i++)
      {
        Logger.Write($"Test line {i}");
        Thread.Sleep(500);
      }
    }
  }

  public static class Logger
  {
    public static void Write(string message) => Log.Write(message);
    private static LogIntern Log = new LogIntern();
    public static int CheckIntervalHours { get; set; } = 1;
    public static long MaxLogLength { get; set; } = 50 * 1024 * 1024;
    public static int DeleteLogAfterDays { get; set; } = 14;
    public static string LogDirPath = @"c:\temp\log";

    private class LogIntern
    {
      public LogIntern()
      {
        this._nameOfApp = Assembly.GetExecutingAssembly().FullName.Split(',')[0];
        Thread th1 = new Thread(() =>
        {
          do
          {
            signalMessage.WaitOne();
            while (messageQueue.Count > 0)
            {
              writer.Write($"{messageQueue.Dequeue()}{Environment.NewLine}");
              writer.Flush();
            }
          } while (!stopping);
          CloseWriter();
          signalStopped.Set();
        });
        th1.Start();
        Thread th2 = new Thread(() =>
        {
          do
          {
            if (signalStopCheck.WaitOne(Logger.CheckIntervalHours * 3600000)) break;
            if (new FileInfo(this._fileName).Length > Logger.MaxLogLength) CloseWriter();
            if (this._lastDeleteDate < DateTime.Now.Date)
            {
              this._lastDeleteDate = DateTime.Now.Date;
              foreach (FileSystemInfo fsi in (new DirectoryInfo(Logger.LogDirPath)).GetFileSystemInfos())
                if (fsi.Name.StartsWith(this._nameOfApp) && this._lastDeleteDate.Subtract(fsi.LastWriteTime).TotalDays > Logger.DeleteLogAfterDays) File.Delete(fsi.FullName);
            }
          } while (true);
        });
        th2.Start();
      }

      private Queue<string> messageQueue = new Queue<string>();
      private AutoResetEvent signalMessage = new AutoResetEvent(false);
      private AutoResetEvent signalStopped = new AutoResetEvent(false);
      private AutoResetEvent signalStopCheck = new AutoResetEvent(false);
      private bool stopping = false;
      private StreamWriter _writer;
      private string _fileName;
      private DateTime _lastDeleteDate = DateTime.Now.AddDays(-1).Date;
      private string _nameOfApp;

      private StreamWriter writer
      {
        get
        {
          lock (this) if (_writer == null) this._writer = new StreamWriter(GetFileName());
          return this._writer;
        }
      }

      private string GetFileName()
      {
        string fileName0 = System.IO.Path.Combine(Logger.LogDirPath, $"{this._nameOfApp}_{DateTime.Now:yyyy-MM-dd}");
        this._fileName = $"{fileName0}.log";
        int nr = 1;
        while (File.Exists(this._fileName)) this._fileName = $"{fileName0}-{nr++}.log";
        return this._fileName;
      }

      private void CloseWriter()
      {
        lock (this) if (writer != null)
          {
            writer.Flush();
            writer.Close();
            writer.Dispose();
            this._writer = null;
          }
      }

      internal void Write(string message)
      {
        messageQueue.Enqueue($"{DateTime.Now:dd.MM.yy HH:mm:ss.fff} {message}");
        signalMessage.Set();
      }

      ~LogIntern()
      {
        stopping = true;
        signalStopCheck.Set();
        signalMessage.Set();
        signalStopped.WaitOne();
      }
    }
  }
}

