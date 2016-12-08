using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using VideoLibrary;

namespace valkyrie
{
    public partial class Form1 : Form
    {
        private readonly Thread[] _workers;
        private readonly ManualResetEvent _workerQuit;
        private readonly object _queueLock;
        private readonly List<string> _videoQueue;
        private int _busyThreads;

        public Form1()
        {
            InitializeComponent();

            _videoQueue = new List<string>();
            _queueLock = new object();
            _workerQuit = new ManualResetEvent(false);

            _workers = new Thread[4];

            for (var i = 0; i < _workers.Length; ++i)
            {
                _workers[i] = new Thread(Converter);
                _workers[i].Start();
            }

            ShowQueueCount();
            ShowBusyThreadCount();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            _workerQuit.Set();
            foreach (var worker in _workers)
            {
                worker.Join();
            }
        }

        private void Converter()
        {
            while(!_workerQuit.WaitOne(500))
            {
                string id;

                lock (_queueLock)
                {
                    if (_videoQueue.Count == 0) continue;

                    id = _videoQueue[0];
                    _videoQueue.RemoveAt(0);

                    ShowQueueCount();
                }

                ++_busyThreads;
                ShowBusyThreadCount();

                var fileName = DoConversion(id);
                Invoke((MethodInvoker)delegate { listBox1.Items.Add(fileName); });

                --_busyThreads;
                ShowBusyThreadCount();
            }
        }

        private void ShowBusyThreadCount()
        {
            if (InvokeRequired) Invoke((MethodInvoker)ShowBusyThreadCount);
            else toolStripStatusLabel2.Text = $@"Active conversions: {_busyThreads}";
        }

        private void ShowQueueCount()
        {
            if (InvokeRequired) Invoke((MethodInvoker) ShowQueueCount);
            else toolStripStatusLabel1.Text = $@"Files in queue: {_videoQueue.Count}";
        }

        private string DoConversion(string youTubeVideoID)
        {
            var videoID = $"https://youtube.com/watch?v={youTubeVideoID}";
            try
            {
                using (var service = Client.For(YouTube.Default))
                {
                    var video = service.GetVideo(videoID);

                    var targetName =
                        Path.Combine(
                            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                                "Downloads"), video.FullName).ToLowerInvariant();

                    var destName = Path.ChangeExtension(targetName.Replace(" - youtube", string.Empty), "mp3");

                    if (File.Exists(destName)) return destName;

                    File.WriteAllBytes(targetName, video.GetBytes());

                    var tool = new Process
                    {
                        StartInfo =
                        {
                            FileName = "ffmpeg.exe",
                            Arguments = $"-i \"{targetName}\" -y -q:a 0 -map a \"{destName}\"",
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            CreateNoWindow = true
                        }
                    };

                    tool.Start();
                    tool.WaitForExit();
                    if (tool.ExitCode == 0)
                    {
                        File.Delete(targetName);
                    }

                    return destName;
                }
            }
            catch{ /**/ }

            return $"Error: {videoID}";
        }

        private void QueueUpIdsFromHtmlPlaylist(string htmlFilename)
        {
            var fileContent = File.ReadAllText(htmlFilename);

            var idSet = new HashSet<string>();
            var regex = new Regex(@"(?<=watch\?v=)(.*?)(?=&amp)");

            var matches = regex.Matches(fileContent);
            foreach (Match match in matches)
            {
                if (match.Value.Length == 11) idSet.Add(match.Value);
            }

            lock (_queueLock)
            {
                foreach (var id in idSet)
                {
                    _videoQueue.Add(id);
                }
            }
            ShowQueueCount();
        }

        private void QueueUpIdFromWatchUrl(string item)
        {
            const string patt = @"^((?:https?:)?\/\/)?((?:www|m)\.)?((?:youtube\.com|youtu.be))(\/(?:[\w\-]+\?v=|embed\/|v\/)?)([\w\-]+)(\S+)?$";
            var match = Regex.Match(item, patt);
            if (match.Groups.Count <= 4) return;

            var id = match.Groups[5].ToString();
            lock (_queueLock)
            {
                _videoQueue.Add(id);
            }
            ShowQueueCount();
        }

        private void OnDragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.Text) || e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Copy;
            }
        }

        private void OnDragDrop(object sender, DragEventArgs e)
        {
            if (e.Effect != DragDropEffects.Copy) return;

            if (e.Data.GetDataPresent(DataFormats.Text))
            {
                var item = (string)e.Data.GetData(typeof(string));
                QueueUpIdFromWatchUrl(item);
            }

            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var filenames = (string[])e.Data.GetData(DataFormats.FileDrop);
                foreach (var filename in filenames)
                {
                    QueueUpIdsFromHtmlPlaylist(filename);
                }
            }
        }

        private void listBox1_DoubleClick(object sender, EventArgs e)
        {
            Process.Start("explorer.exe", $"/select,\"{listBox1.SelectedItem.ToString()}\"");
        }
    }
}
