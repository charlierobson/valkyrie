using System;
using System.Collections.Generic;
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
        private readonly Thread _downloaderThread;
        private readonly object _listLock;
        private readonly Queue<string> _downloadQueue;
        private bool _downloaderFinished;

        public Form1()
        {
            InitializeComponent();

            _listLock = new object();
            _downloadQueue = new Queue<string>();

            _downloaderThread = new Thread(Downloader);
            _downloaderThread.Start();

            ShowDownloadCount();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            _downloaderFinished = true;
            _downloaderThread.Join();
        }

        private void ShowDownloadCount()
        {
            toolStripStatusLabel0.Text = _downloadQueue.Count.ToString();
        }

        private void SetStatus(string status)
        {
            toolStripStatusLabel1.Text = status;
        }

        private void Downloader()
        {
            while (!_downloaderFinished)
            {
                if (_downloadQueue.Count == 0)
                {
                    Thread.Sleep(200);
                    continue;
                }

                string downloadItem;
                lock (_listLock)
                {
                    downloadItem = _downloadQueue.Dequeue();
                }

                Invoke((MethodInvoker) (() => SetStatus("Downloading...")));

                const string patt =
                    @"^((?:https?:)?\/\/)?((?:www|m)\.)?((?:youtube\.com|youtu.be))(\/(?:[\w\-]+\?v=|embed\/|v\/)?)([\w\-]+)(\S+)?$";
                var match = Regex.Match(downloadItem, patt);
                if (match.Groups.Count <= 4) return;

                var videoID = "https://youtube.com/watch?v=" + match.Groups[5];
                var savedName = string.Empty;

                try
                {
                    using (var service = Client.For(YouTube.Default))
                    {
                        var video = service.GetVideo(videoID);

                        savedName =
                            Path.Combine(
                                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                                    "Downloads"), video.FullName);
                        File.WriteAllBytes(savedName, video.GetBytes());

                        Invoke((MethodInvoker) (() => listBox1.Items.Add(savedName)));
                    }
                }
                catch (Exception ex)
                {
                    Invoke((MethodInvoker) (() => listBox1.Items.Add("FAILED - " + savedName)));
                    Console.WriteLine(ex);
                }

                Invoke((MethodInvoker) (() => SetStatus(string.Empty)));
                Invoke((MethodInvoker) (ShowDownloadCount));
            }
        }

        private void OnDragEnter(object sender, DragEventArgs e)
        {
            e.Effect = e.Data.GetDataPresent(DataFormats.Text) ? DragDropEffects.Copy : DragDropEffects.None;
        }

        private void OnDragDrop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(typeof(string))) return;
            if (e.Effect != DragDropEffects.Copy) return;

            var item = (string) e.Data.GetData(typeof(string));
            Console.WriteLine(item);

            lock (_listLock)
            {
                _downloadQueue.Enqueue(item);
            }
            ShowDownloadCount();
        }

        private void textBoxURL_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter) return;

            var item = textBoxURL.Text.Trim();

            textBoxURL.Text = string.Empty;

            lock (_listLock)
            {
                _downloadQueue.Enqueue(item);
            }
            ShowDownloadCount();

            e.Handled = true;
        }

        private void listBox1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            var index = listBox1.IndexFromPoint(e.Location);
            if (index == ListBox.NoMatches) return;

            try
            {
                Process.Start((string)listBox1.Items[index]);
            }
            catch
            {
                //
            }
        }
    }
}
