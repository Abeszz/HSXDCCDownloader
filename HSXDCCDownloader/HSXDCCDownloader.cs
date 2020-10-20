﻿using HorribleSubsFetcher;
using JikanDotNet;
using SimpleIRCLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace HSXDCCDownloader
{
    public partial class HSXDCCDownloader : Form
    {

        private readonly SimpleIRC irc;
        private readonly IJikan jikan;

        public int port = 6697;
        public string ip = "irc.rizon.net";
        public string username = "HSXDCCbotOhYeah" + new Random().Next(1, 9999).ToString();
        public string channel = "#horriblesubs";

        public string downloadDirectory;

        private List<string> DownloadQueue;

        public HSXDCCDownloader()
        {
            irc = new SimpleIRC();
            irc.DccClient.OnDccEvent += OnDccEvent;

            jikan = new Jikan();

            downloadDirectory = Directory.GetCurrentDirectory();

            InitializeComponent();

            AnimeBox.MouseDoubleClick += new MouseEventHandler(AnimeBoxDoubleClick);


        }

        private void HSXDCCDownloader_Load(object sender, EventArgs e)
        {
            ResetHSXDCCDownloader();

            if (irc != null)
            {
                if (!irc.IsClientRunning())
                {
                    irc.SetupIrc(ip, username, channel, port);
                    irc.StartClient();
                    WriteDebugLabel("Connected to IRC");
                }
            }
        }

        private void ResetHSXDCCDownloader()
        {
            DebugLabel.Text = "";
            EnableAnimeBox();
        }

        private void EnableAnimeBox()
        {
            AnimeBox.Visible = true;
            AnimeBox.Enabled = true;
            AnimeBox.Items.Clear();
            DownloadBox.Visible = false;
            DownloadBox.Enabled = false;
        }

        private void EnableDownloadBox()
        {
            DownloadBox.Visible = true;
            DownloadBox.Enabled = true;
            DownloadBox.Rows.Clear();
            AnimeBox.Visible = false;
            AnimeBox.Enabled = false;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (irc.IsClientRunning())
            {
                irc.StopClient();
                irc.StopXDCCDownload();
            }
        }

        public void WriteDebugLabel(string s)
        {
            DebugLabel.Text = s;
        }

        private void FileButton_Click(object sender, EventArgs e)
        {
            DialogResult result = OpenFolderDialog.ShowDialog();
            if (result == DialogResult.OK)
            {
                string newDownloadDirectory = OpenFolderDialog.SelectedPath;
                downloadDirectory = newDownloadDirectory;
                irc.SetCustomDownloadDir(newDownloadDirectory);
                WriteDebugLabel("Set to " + downloadDirectory);
            }
        }

        private async void SearchButton_Click(object sender, EventArgs e)
        {
            WriteDebugLabel("Searching");

            EnableAnimeBox();
            AnimeSearchResult asr = await SearchAnime();

            if (asr != null)
            {
                foreach (AnimeSearchEntry r in asr.Results)
                {
                    AnimeBox.Items.Add(r.Title);
                }
            }
        }

        private async Task<AnimeSearchResult> SearchAnime()
        {
            AnimeSearchResult asr = await jikan.SearchAnime(SearchBox.Text);
            return asr;
        }

        private async void AnimeBoxDoubleClick(object sender, MouseEventArgs e)
        {
            WriteDebugLabel("Displaying");

            int index = this.AnimeBox.IndexFromPoint(e.Location);
            if (index != ListBox.NoMatches)
            {
                IEnumerable<HorribleSubsFetcher.Model.Pack> hp = await GetPacklist(AnimeBox.SelectedItem.ToString());

                if (hp != null)
                {
                    EnableDownloadBox();
                    foreach (var p in hp)
                    {
                        DownloadBox.Rows.Add(false, p.Filename, GetResolution(p.Filename, p.Bot), p.Size + "M", "", p.ToString());
                    }
                }
            }
        }

        private async Task<IEnumerable<HorribleSubsFetcher.Model.Pack>> GetPacklist(string a)
        {
            var httpClient = new HttpClient();

            var fetcher = new Fetcher(httpClient);
            var tokenSource = new CancellationTokenSource();

            IEnumerable<HorribleSubsFetcher.Model.Pack> packList = await fetcher.FindPacksAsync(a, tokenSource.Token);

            return packList;

        }

        private string GetResolution(string f, string b)
        {
            if (b == "ARUTHA-BATCH|480p")
            {
                return "480p";
            }
            else if (b == "ARUTHA-BATCH|720p")
            {
                return "720p";
            }
            else if (b == "ARUTHA-BATCH|1080p")
            {
                return "1080p";
            }
            else
            {
                if (f.Contains("480p") || f.Contains("480"))
                {
                    return "480p";
                }
                else if (f.Contains("720p") || f.Contains("720"))
                {
                    return "720p";
                }
                else if (f.Contains("1080p") || f.Contains("1080"))
                {
                    return "1080p";
                }
                else
                {
                    return "";
                }
            }
        }

        private void DownloadButton_Click(object sender, EventArgs e)
        {
            WriteDebugLabel("Downloading");

            DownloadQueue = new List<string>();

            foreach (DataGridViewRow r in DownloadBox.Rows)
            {
                if ((bool)r.Cells[0].Value == true)
                {
                    r.Cells[4].Value = "Queued";
                    r.Cells[0].Value = false;
                    DownloadQueue.Add(r.Cells[5].Value.ToString());
                }
            }

            if (!irc.CheckIfDownload())
            {
                if (DownloadQueue.Count > 0)
                {
                    CheckdownloadQueue();
                }
            }
        }

        private void CheckdownloadQueue()
        {
            foreach (string q in DownloadQueue)
            {
                if (q != "Completed")
                {
                    Console.WriteLine("check qu");
                    irc.SendMessageToChannel(q, channel);
                    break;
                }
            }
        }

        public void OnDccEvent(object sender, DCCEventArgs args)
        {
            if (args != null)
            {
                string xdcc = "/msg " + args.Bot + " xdcc send " + args.Pack;
                int progress = args.Progress;
                string status = args.Status;

                UpdateDownloadingBox(xdcc, progress, status);

                if (status == "COMPLETED")
                {
                    int i = DownloadQueue.IndexOf(xdcc);
                    DownloadQueue[i] = "Completed";
                    CheckdownloadQueue();
                }
            }

        }

        private void UpdateDownloadingBox(string xdcc, int pg, string st)
        {
            if (DownloadBox.InvokeRequired)
            {
                DownloadBox.Invoke(new MethodInvoker(() => UpdateDownloadingBox(xdcc, pg, st)));
            }
            else
            {
                foreach (DataGridViewRow r in DownloadBox.Rows)
                {
                    if (r.Cells[5].Value.ToString() == xdcc)
                    {
                        r.Cells[4].Value = pg + "%";
                    }
                }

                if (st == "COMPLETED")
                {
                    foreach (DataGridViewRow r in DownloadBox.Rows)
                    {
                        if (r.Cells[5].Value.ToString() == xdcc)
                        {
                            r.Cells[4].Value = "Completed";
                        }
                    }
                }
            }
        }
    }
}
