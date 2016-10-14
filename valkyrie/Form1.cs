using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
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

            DoDownload(item);
        }

        private void textBoxURL_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter) return;

            var item = textBoxURL.Text.Trim();

            textBoxURL.Text = string.Empty;

            DoDownload(item);
            e.Handled = true;
        }

        //

        void DoDownload(string item)
        {
            const string patt =
                @"^((?:https?:)?\/\/)?((?:www|m)\.)?((?:youtube\.com|youtu.be))(\/(?:[\w\-]+\?v=|embed\/|v\/)?)([\w\-]+)(\S+)?$";
            var match = Regex.Match(item, patt);
            if (match.Groups.Count <= 4) return;

            toolStripStatusLabel1.Text = @"Saving...";

            var savedName = string.Empty;
            if (Download(match.Groups[5].ToString(), ref savedName))
            {
                listBox1.Items.Add(savedName);
            }

            toolStripStatusLabel1.Text = string.Empty;
        }

        private bool Download(string id, ref string savedName)
        {
            var saved = false;

            try
            {
                using (var service = Client.For(YouTube.Default))
                {
                    var video = service.GetVideo("https://youtube.com/watch?v=" + id);

                    savedName = Path.Combine(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"), video.FullName);

                    File.WriteAllBytes(savedName, video.GetBytes());
                }

                saved = true;
            }
            catch (Exception)
            {
                //
            }

            return saved;
        }

        private void listBox1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            var index = listBox1.IndexFromPoint(e.Location);
            if (index != ListBox.NoMatches)
            {
                Process.Start((string)listBox1.Items[index]);
            }
        }
    }
}