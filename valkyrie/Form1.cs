using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using VideoLibrary;

namespace valkyrie
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private async Task DownloadVideoById(string youTubeVideoID)
        {
            Invoke((MethodInvoker)delegate { listBox1.Items.Add(youTubeVideoID); });
            await Task.Run(() =>
            {
                try
                {
                    var videoID = "https://youtube.com/watch?v=" + youTubeVideoID;

                    using (var service = Client.For(YouTube.Default))
                    {
                        var video = service.GetVideo(videoID);

                        var targetName =
                             Path.Combine(
                                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                                    "Downloads"), video.FullName).ToLowerInvariant();

                        var destName = Path.ChangeExtension(targetName.Replace(" - youtube", string.Empty), "mp3");

                        if (File.Exists(destName)) return;

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
                    }
                }
                catch
                {
                    /**/
                }
            });
            Invoke((MethodInvoker)delegate { listBox1.Items.RemoveAt(0); });
        }

        private void QueueUpIdFromHtml(string htmlFilename)
        {
            var fileContent = File.ReadAllText(htmlFilename);

            var idSet = new HashSet<string>();
            var regex = new Regex(@"(?<=watch\?v=)(.*?)(?=&amp)");

            var matches = regex.Matches(fileContent);
            foreach (Match match in matches)
            {
                if (match.Value.Length == 11) idSet.Add(match.Value);
            }

            Parallel.ForEach(idSet, new ParallelOptions { MaxDegreeOfParallelism = 4 }, id =>
            {
                DownloadVideoById(id);
            });
        }

        private async void QueueUpIdFromUrl(string item)
        {
            const string patt = @"^((?:https?:)?\/\/)?((?:www|m)\.)?((?:youtube\.com|youtu.be))(\/(?:[\w\-]+\?v=|embed\/|v\/)?)([\w\-]+)(\S+)?$";
            var match = Regex.Match(item, patt);
            if (match.Groups.Count <= 4) return;

            var id = match.Groups[5].ToString();
            await DownloadVideoById(id);
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
                QueueUpIdFromUrl(item);
            }

            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var filenames = (string[])e.Data.GetData(DataFormats.FileDrop);
                foreach (var filename in filenames)
                {
                    QueueUpIdFromHtml(filename);
                }
            }
        }

        private void textBoxURL_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter) return;

            var item = textBoxURL.Text.Trim();

            textBoxURL.Text = string.Empty;

            QueueUpIdFromUrl(item);

            e.Handled = true;
        }
    }
}
