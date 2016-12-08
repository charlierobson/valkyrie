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

        private async Task<string> DownloadVideoById(string youTubeVideoID)
        {
            var destName = string.Empty;

            try
            {
                var videoID = $"https://youtube.com/watch?v={youTubeVideoID}";
                var video = await GetVideoAsync(videoID);

                var tempName =
                    Path.Combine(
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                            "Downloads"), video.FullName).ToLowerInvariant();

                destName = Path.ChangeExtension(tempName.Replace(" - youtube", string.Empty), "mp3");

                if (File.Exists(destName)) return destName;

                await WriteBytesAsync(tempName, video);

                var result = await RunProcessAsync("ffmpeg.exe", $"-i \"{tempName}\" -y -q:a 0 -map a \"{destName}\"");
                if (result)
                {
                    File.Delete(tempName);
                    return destName;
                }
            }
            catch
            {
                /**/
            }
            return $"Failed '{destName}'";
        }

        private static Task WriteBytesAsync(string destName, YouTubeVideo video)
        {
            return Task.Run(() =>
            {
                File.WriteAllBytes(destName, video.GetBytes());
            });
        }

        private static Task<YouTubeVideo> GetVideoAsync(string videoID)
        {
            return Task.Run(() =>
            {
                using (var service = Client.For(YouTube.Default))
                {
                    return service.GetVideo(videoID);
                }
            });
        }

        private Task<bool> RunProcessAsync(string fileName, string arguments)
        {
            var tcs = new TaskCompletionSource<bool>();

            var process = new Process
            {
                StartInfo =
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                },
                EnableRaisingEvents = true
            };

            process.Exited += (sender, args) =>
            {
                tcs.SetResult(process.ExitCode == 0);
                process.Dispose();
            };

            process.Start();

            return tcs.Task;
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
            listBox1.Items.Add($"Downloading {id}");
            var result = await DownloadVideoById(id);
            listBox1.Items.Add(result);
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

        private void listBox1_DoubleClick(object sender, EventArgs e)
        {
            if (listBox1.SelectedItem != null)
            {
                var item = listBox1.SelectedItem.ToString();
                Process.Start("explorer.exe", $"/select,\"{item}\"");
            }
        }
    }
}
