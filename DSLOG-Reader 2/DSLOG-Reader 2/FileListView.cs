﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.Globalization;
using DSLOG_Reader_Library;
using DSLOG_Reader_2.Properties;

namespace DSLOG_Reader_2
{
    public partial class FileListView : UserControl
    {
        private MainForm MForm;
        private string Path;
        private List<DSLOGFileEntry> DSLOGFiles;
        private string lastFilter = "";
        public MainGraphView MainChart { get; set; }
        private int lastIndexSelectedFiles = 0;
        private bool filterUseless = false;
        public FileListView()
        {
            InitializeComponent();
            DSLOGFiles = new List<DSLOGFileEntry>();
            toolTip1.SetToolTip(buttonFilter, "Filter Useless Logs");
        }

        public void SetMainForm(MainForm form) { MForm = form; }
        public void SetPath(string path) { Path = path; textBoxPath.Text = path; }

        public void LoadFiles()
        {
            listView.Items.Clear();
            DSLOGFiles.Clear();
            if (Path == null)
            {
                return; //Throw something
            }
            if (Directory.Exists(Path))
            {
               
                DSLOGFileEntryCache cache = new DSLOGFileEntryCache(@"C:\Users\Public\Documents\FRC\.dslog.cache");
                CacheLoadingDialog dialog = new CacheLoadingDialog(cache, DSLOGFiles, Path);
                dialog.ShowDialog();
                FillInMissingFMSEventInfo();
                cache.SaveCache();
                listView.BeginUpdate();
                foreach (var entry in DSLOGFiles)
                {
                    listView.Items.Add(entry.ToListViewItem());
                }
                listView.EndUpdate();
                //sroll down to bottom (need to use timer cuz it's weird
                timerScrollToBottom.Start();
                ResizeColumns();
                CreateFileWatcher();
            }

            InitFilterCombo();
        }

        private void FillInMissingFMSEventInfo()
        {
            var fmsMatches = DSLOGFiles.Where(e => e.IsFMSMatch);
            foreach (var match in fmsMatches)
            {
                if (string.IsNullOrWhiteSpace(match.EventName))
                {
                    var nearestEvent = fmsMatches.Where(e => !string.IsNullOrWhiteSpace(e.EventName)).OrderBy(e => Math.Abs((e.StartTime - match.StartTime).Ticks)).First();
                    TimeSpan span = nearestEvent.StartTime - match.StartTime;
                    if (Math.Abs((span).TotalDays) < 5)
                    {
                        //if (match.MatchType == nearestEvent.MatchType && ((span.TotalSeconds < 0 && match.FMSMatchNum < nearestEvent.FMSMatchNum) || (span.TotalSeconds > 0 && match.FMSMatchNum > nearestEvent.FMSMatchNum)))
                        //{
                        //    continue;
                        //}

                        match.EventName = nearestEvent.EventName;
                        match.FMSFilledIn = true;
                        continue;
                    }
                    match.FMSFilledIn = true;
                    match.EventName = "???";
                }
            }
        }


        private void CreateFileWatcher()
        {
            FileSystemWatcher watcher = new FileSystemWatcher();
            watcher.Path = Path;
            watcher.Filter = "*.dslog*";
            watcher.Created += new FileSystemEventHandler(OnFileChanged);
            watcher.EnableRaisingEvents = true;
        }

        private void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            var entry = new DSLOGFileEntry(e.Name.Replace(".dslog", ""), Path);
            
            listView.Invoke(new MethodInvoker(delegate 
            {
                DSLOGFiles.Add(entry);
                string selectedEvent = filterSelectorCombo.Items[filterSelectorCombo.SelectedIndex].ToString();

                if (selectedEvent == "" || entry.EventName == selectedEvent.Substring(0, selectedEvent.Length - 5) && entry.StartTime.ToString("yyyy") == selectedEvent.GetLast(4))
                {
                    listView.Items.Add(entry.ToListViewItem());
                }
                
            }));
        }

        private void timerScrollToBottom_Tick(object sender, EventArgs e)
        {
            if(listView.Items.Count != 0)
            {
                listView.EnsureVisible(listView.Items.Count - 1);
            }
           
            timerScrollToBottom.Stop();
        }

        private void InitFilterCombo()
        {
            filterSelectorCombo.Items.Clear();
            filterSelectorCombo.Items.Add("All Logs");
            var eventNames = DSLOGFiles.Where(e => e.IsFMSMatch).Select(e => e.EventName + " "+e.StartTime.ToString("yyyy")).Distinct();
            foreach(var name in eventNames)
            {
                filterSelectorCombo.Items.Add(name);
            }
            filterSelectorCombo.SelectedIndex = 0;
        }

        private void filterSelectorCombo_SelectedIndexChanged(object sender, EventArgs e)
        {
            FilterLogs();
        }

        private void listView_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (listView.SelectedItems.Count != 0)
            {
                if (listView.SelectedItems[0].Index != lastIndexSelectedFiles)
                {
                    lastIndexSelectedFiles = listView.SelectedItems[0].Index;
                    GraphLog(listView.SelectedItems[0].Text);
                }
            }
        }

        private void GraphLog(string file)
        {
            if (MainChart !=null)
            {
                MainChart.LoadLog(file, Path);
            }
        }

        private void buttonFilter_Click(object sender, EventArgs e)
        {
            if (!filterUseless)
            {
                filterUseless = true;
                buttonFilter.BackgroundImage = Resources.StopFilter_16x;
            }
            else
            {
                filterUseless = false;
                buttonFilter.BackgroundImage = Resources.RunFilter_16x;
            }
            FilterLogs(true);
        }

        private void FilterLogs(bool force = false)
        {
            string selectedEvent = filterSelectorCombo.Items[filterSelectorCombo.SelectedIndex].ToString();
            if (lastFilter == selectedEvent && !force) return;

            listView.BeginUpdate();
            listView.Items.Clear();
            
            
            if (selectedEvent == "All Logs")
            {
                foreach (var entry in DSLOGFiles.Where(e=>!(e.Useless && filterUseless)))
                {
                    listView.Items.Add(entry.ToListViewItem());
                }
            }
            else
            {
                foreach (var entry in DSLOGFiles.Where(e => !(e.Useless && filterUseless)).Where(en => en.EventName == selectedEvent.Substring(0, selectedEvent.Length-5) && en.StartTime.ToString("yyyy") == selectedEvent.GetLast(4)))
                {
                    listView.Items.Add(entry.ToListViewItem());
                }
            }
            listView.EndUpdate();
            //sroll down to bottom (need to use timer cuz it's weird)
            lastIndexSelectedFiles = -1;
            timerScrollToBottom.Start();
            lastFilter = selectedEvent;
        }

        private void buttonRefreash_Click(object sender, EventArgs e)
        {
            LoadFiles();
        }

        public bool HasFMSMatch()
        {
            if (DSLOGFiles != null && DSLOGFiles.Count > 0)
            {
                return DSLOGFiles.Any(e => e.IsFMSMatch);
            }
            return false;
        }

        private void ResizeColumns()
        {
            for(int i = 0; i < listView.Columns.Count; i++)
            {
                if (i == 2 || i == 3 || i == 5)
                {
                    listView.AutoResizeColumn(i, ColumnHeaderAutoResizeStyle.HeaderSize);
                }
                else
                {
                    listView.AutoResizeColumn(i, ColumnHeaderAutoResizeStyle.ColumnContent);
                }
                
            }
        }
    }        
}
