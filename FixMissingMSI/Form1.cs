﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;

using System.Windows.Forms;
using System.IO;
using Microsoft.Deployment.WindowsInstaller;
using Microsoft.Deployment.WindowsInstaller.Linq;
using Microsoft.Deployment.WindowsInstaller.Package;

using System.Diagnostics;
using System.Reflection;
using System.Threading;

namespace FixMissingMSI
{
    public partial class Form1 : Form
    {
        static Control lbl;
        static Control myform;
        public Form1()
        {
            InitializeComponent();
           
            lbl = this.lbInfo;
            lbl.Text = "";

            lableSwitch(false);

            Logger.SetupLog();

            myData.Init(dataGridView1, UpdateStatus, DoneCallBack, DoneCallBack_Last);
 

            myform = this;

            string ver = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            this.Text = this.Text + "  Version " + ver.Replace(".0.0", "");

            splitContainer1.SplitterDistance= splitContainer1.Panel1MinSize;

            myFind.dataGridView1 = dataGridView1;

        }
        public void lableSwitch(bool isVisible)
        {
            rbAll.Visible 
                = rbMissingOrMismatched.Visible 
                = lbTotal.Visible 
                = lbOK.Visible 
                = lbMissing.Visible 
                = lbMismatched.Visible
                = isVisible;

            lbInfo.Visible = !isVisible;
        }
        public void UpdateStatistics()
        {
            Logger.LogMsg("UpdateStatistics started.");
            this.lbTotal.Text = "Total: " + this.dataGridView1.Rows.Count.ToString();
            int mismatched = 0, missing = 0, ok = 0;
            foreach (DataGridViewRow r in this.dataGridView1.Rows)
            {
                if ((CacheFileStatus)r.Cells["Status"].Value == CacheFileStatus.Mismatched)
                    mismatched++;
                else if ((CacheFileStatus)r.Cells["Status"].Value == CacheFileStatus.Missing)
                    missing++;
                else if ((CacheFileStatus)r.Cells["Status"].Value == CacheFileStatus.OK)
                    ok++;
            }

            this.lbOK.Text = "OK: " + ok;
            this.lbMismatched.Text = "Mismatched: " + mismatched;
            this.lbMissing.Text = "Missing: " + missing;
            Logger.LogMsg("UpdateStatistics done.");
        }

        public void ShowInfo(string msg)
        {
            lbInfo.Text = msg;
            //lbInfo.Refresh();
        }
        private void SetColumnSort()
        {

            foreach (DataGridViewColumn column in this.dataGridView1.Columns)
            {
                //设置自动排序
                column.SortMode = DataGridViewColumnSortMode.Automatic;
            }
        }
        public void DoneCallBack()
        {
            myform.BeginInvoke((MethodInvoker)delegate
            {
                lbInfo.Text = "Scan done. Formatting rows and populating grid view, may take minutes...";
                lbInfo.Refresh();
                myData.SetRow();//scan done, update the datasource of the gridview

          
                UpdateStatistics();
             
                UpdateColorForDataGridView();
                lbInfo.Text = "";
                lbInfo.Refresh();
                SetColumnSort();
                lableSwitch(true);

                rbAll.Visible = rbMissingOrMismatched.Visible = false;
                lbInfo.Visible = true; 
               
                //Now call afterdone to update FixCommand
                Thread th = new Thread(new ThreadStart(myData.AfterDone));
                th.Start();

            });
        }


        public void DoneCallBack_Last()
        {
            myform.BeginInvoke((MethodInvoker)delegate
            {

                lbInfo.Visible = false;
              

                rbAll.Visible = rbMissingOrMismatched.Visible = true;
                this.menuStrip1.Enabled = true;
                rbAll.Enabled = rbMissingOrMismatched.Enabled = true;
                // lableSwitch(true);
            

             this.dataGridView1.CellClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.dataGridView1_CellClick);

            });
        }

        public void UpdateStatus(string msg)// string data, int colorAARRGGBB = 0)
        {



            if (!this.InvokeRequired)
            {
                ShowInfo(msg);

            }

            else
            {
                Form1.lbl.BeginInvoke((MethodInvoker)delegate
                {
                    ShowInfo(msg);
                });
            }
        }

        


        private void rbMissingOrMismatched_CheckedChanged(object sender, EventArgs e)
        {
            this.Enabled = false;
            //Missing/Mismtached only
            if (((RadioButton)sender).Checked == true)
            {
                lbInfo.Visible = true;
                lbInfo.Text = "Refreshing...may take minutes...";
                lbInfo.Refresh();
                myData.SetFilter(dataGridView1);
                UpdateColorForDataGridView();
                UpdateStatistics();
            }
            else
            {
                lbInfo.Text = "Refreshing...may take minutes...";
                lbInfo.Refresh();
                myData.RemoveFilter(dataGridView1);
                UpdateColorForDataGridView();
                UpdateStatistics();
            }
            lbInfo.Text = "";
            lbInfo.Refresh();
            lbInfo.Visible = false;
            this.Enabled = true;
        }


 

        private void Form1_Load(object sender, EventArgs e)
        {
            typeof(DataGridView).InvokeMember(
            "DoubleBuffered",
            BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.SetProperty,
            null,
            dataGridView1,
            new object[] { true });
        }


 
        private class MyCell
        {
            public Int32 rowIdx;
            public Int32 colIdx;
            public String Value;
            public MyCell(int ri, int ci, string v)
            {
                rowIdx = ri; colIdx = ci; Value = v;
            }

        }
        private void CopySelectedCell()
        {
            var cnt = dataGridView1.SelectedCells.Count;
            if (cnt <= 0) return;
            string s = "";
            var rowIdx = 0;
            List<MyCell> cells = new List<MyCell>();
            foreach (var cell in dataGridView1.SelectedCells)
            {
                if (cell.GetType() == typeof(DataGridViewTextBoxCell))
                {
                    DataGridViewTextBoxCell c = (DataGridViewTextBoxCell)cell;
                    cells.Add(new MyCell(c.RowIndex, c.ColumnIndex, c.Value == null ? "" : c.Value.ToString()));
                }
            }
            cells = cells.OrderBy(p => p.rowIdx).ThenBy(p => p.colIdx).ToList();
            bool isFirst = true;
            foreach (MyCell c in cells)
            {

                if (rowIdx != c.rowIdx)
                {
                    rowIdx = c.rowIdx;
                    s = s + "\n\r";
                    isFirst = true;
                }
                //first cell
                if (isFirst) { s = s + "\"" + c.Value + "\""; isFirst = false; }
                else s = s + "," + "\"" + c.Value + "\"";
            }


            if (!String.IsNullOrEmpty(s))
            {
                Clipboard.SetText(s.Trim());
               // lbInfo.Text = cnt + " selected cell(s) copied to clipboard.";
            }
        }

        private void btnScanFix_Click(object sender, EventArgs e)
        {

         



        }


        private void copySelectedCellsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                CopySelectedCell();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.Message);

            }
        }
       
        private void Form1_Resize(object sender, EventArgs e)
        {
           // ResizeIt();
        }

        public void UpdateColorForDataGridView()
        {
            Logger.LogMsg("UpdateColorForDataGridView started.");
            foreach (DataGridViewRow row in this.dataGridView1.Rows)
            {
              
                if ((CacheFileStatus)row.Cells["Status"].Value == CacheFileStatus.Missing)
                {
                    row.DefaultCellStyle.BackColor = Color.OrangeRed;
                    ((DataGridViewButtonCell)row.Cells["FixIt"]).Value = "Fix It";
                }
                else if ((CacheFileStatus)row.Cells["Status"].Value == CacheFileStatus.Mismatched)
                {
                    row.DefaultCellStyle.BackColor = Color.Yellow;
                    ((DataGridViewButtonCell)row.Cells["FixIt"]).Value = "Fix It";
                }

                else if ((CacheFileStatus)row.Cells["Status"].Value == CacheFileStatus.Fixed)
                {
                    row.DefaultCellStyle.BackColor = Color.YellowGreen;

                    row.Cells["FixIt"].Value = null;
                    row.Cells["FixIt"] = new DataGridViewTextBoxCell();
                }

                else
                {
                    if ((row.Cells["PackageName"].Value!=null))
                    {
                        if (row.Cells["PackageName"].Value.ToString().ToUpper().Contains(".MSP"))
                            row.DefaultCellStyle.BackColor = Color.FromArgb(255, 255, 255, 240);
                        else row.DefaultCellStyle.BackColor = Color.White;
                    }
                    //Hide the Fix it buton
                    row.Cells["FixIt"].Value = null;
                    row.Cells["FixIt"] = new DataGridViewTextBoxCell();

                }

            }
            Logger.LogMsg("UpdateColorForDataGridView done.");
        }


        private bool CopyFile(string source, string destination)
        {
            String warning = "Do you want to copy [" + source + "] to [" + destination + "] ?";
            DialogResult result = MessageBox.Show(warning, "Confirmation",
                MessageBoxButtons.OKCancel, MessageBoxIcon.Question);
            if (result == DialogResult.OK)
            {

                File.Copy(source, destination, true);
                if (File.Exists(destination))
                {
                    return true;
                }
                else
                {
                    MessageBox.Show("Copy [" + source + "] to [" + destination + "] failed.", "Copy failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }
            }
            return false;
        }
        private void dataGridView1_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex != -1)
            {
                if (this.dataGridView1.Columns[e.ColumnIndex].Name == "FixIt")//
                {
                    //   MessageBox.Show(this.dataGridView1.Rows[e.RowIndex].Cells["PackageName"].Value.ToString());

                    if ((CacheFileStatus)(this.dataGridView1.Rows[e.RowIndex].Cells["Status"].Value) == CacheFileStatus.Missing
                         || (CacheFileStatus)(this.dataGridView1.Rows[e.RowIndex].Cells["Status"].Value) == CacheFileStatus.Mismatched)
                    {
                        int idx = e.RowIndex;
                        var rowIndexCellValue = (int)this.dataGridView1.Rows[e.RowIndex].Cells["Index"].Value;
                        myRow r = null;
                        foreach (myRow rr in myData.rows)
                        {
                            if (rr.Index == rowIndexCellValue) { r = rr; break; }
                        }


                        if (r == null)
                        {
                            MessageBox.Show("Internal data error! Clicked row not found in rows!");
                            return;
                        }
                        if (r.isPatch)
                        {
                            var matchedFile = myData.FindMsp(r.ProductName, r.PackageName, r.PatchCode);
                            if (!String.IsNullOrEmpty(matchedFile))
                            {
                                string destination = Path.Combine(@"c:\WINDOWS\INSTALLER\", r.CachedMsiMsp);
                                bool copied = CopyFile(matchedFile, destination);
                                if (copied)
                                {
                                    r.Status = CacheFileStatus.Fixed;

                                    var row = this.dataGridView1.Rows[e.RowIndex];
                                    row.DefaultCellStyle.BackColor = Color.YellowGreen;

                                    row.Cells["FixIt"].Value = null;
                                    row.Cells["FixIt"] = new DataGridViewTextBoxCell();

                                    UpdateStatistics();
                                    Logger.LogMsg("[Copy Done]" + destination + "==>" + destination);

                                }
                                else
                                    Logger.LogMsg("[Copy Failed]" + destination + "==>" + destination);
                            }
                            else
                                MessageBox.Show("Missing MSP not found!\n" + r.PackageName, "File Not Found",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Exclamation);

                        }
                        else

                        {
                            var matchedFile = myData.FindMsi(r.ProductName, r.PackageName, r.ProductCode, r.ProductVersion, r.PackageCode);
                            if (!String.IsNullOrEmpty(matchedFile))
                            {

                                string destination = Path.Combine(@"c:\WINDOWS\INSTALLER\", r.CachedMsiMsp);
                                bool copied = CopyFile(matchedFile, destination);
                                if (copied)
                                {
                                    r.Status = CacheFileStatus.Fixed;

                                    var row = this.dataGridView1.Rows[e.RowIndex];
                                    row.DefaultCellStyle.BackColor = Color.YellowGreen;

                                    row.Cells["FixIt"].Value = null;
                                    row.Cells["FixIt"] = new DataGridViewTextBoxCell();

                                    UpdateStatistics();
                                    Logger.LogMsg("[Copy Done]" + destination + "==>" + destination);
                                }
                                else
                                    Logger.LogMsg("[Copy Failed]" + destination + "==>" + destination);


                            }

                            else
                                MessageBox.Show("Missing MSI not found!\n" + r.PackageName, "File Not Found",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Exclamation);
                        }

                    } //IF CLICK "fix it"


                } //if click onte first colum
            }

        }



        private int FixAll()
        {
            bool modifed = false;
            int fixedCount = 0;
            foreach (myRow r in myData.rows)
            {
                if (r.Status == CacheFileStatus.Mismatched || r.Status == CacheFileStatus.Missing)
                {
                    string destination = Path.Combine(@"c:\WINDOWS\INSTALLER\", r.CachedMsiMsp);

                    if (r.isPatch)
                    {
                        var matchedFile = myData.FindMsp(r.ProductName, r.PackageName, r.PatchCode);
                        if (!String.IsNullOrEmpty(matchedFile))
                        {
                            Logger.LogMsg("[Found missing MSP]" + matchedFile);
                            File.Copy(matchedFile, destination, true);
                            if (File.Exists(destination))
                            {
                                r.Status = CacheFileStatus.Fixed;
                                modifed = true;
                                fixedCount++;
                                Logger.LogMsg("[Copy Done]" + matchedFile + "==>" + destination);
                            }
                            else
                                Logger.LogMsg("[Copy Failed]" + matchedFile + "==>" + destination);
                        }
                        else
                            Logger.LogMsg("[Missing MSP not found]" + matchedFile);
                    }
                    else
                    {
                        var matchedFile = myData.FindMsi(r.ProductName, r.PackageName, r.ProductCode, r.ProductVersion, r.PackageCode);
                        if (!String.IsNullOrEmpty(matchedFile))
                        {
                            Logger.LogMsg("[Found missing MSI]" + matchedFile);
                            File.Copy(matchedFile, destination, true);
                            if (File.Exists(destination))
                            {
                                r.Status = CacheFileStatus.Fixed;
                                modifed = true;
                                fixedCount++;
                                Logger.LogMsg("[Copy Done]" + matchedFile + "==>" + destination);
                            }
                            else
                                Logger.LogMsg("[Copy Failed]" + matchedFile + "==>" + destination);
                        }
                        else
                            Logger.LogMsg("[Missing MSI not found]" + matchedFile);

                    }




                }




            }//foreach

            if (modifed)
            {
                UpdateColorForDataGridView();
                UpdateStatistics();
            }

            return fixedCount;
        }

      

        private void dataGridView1_ColumnHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {

            this.Enabled = false;
          //  lbInfo.Text = "Sorting and coloring...";
          //  lbInfo.Refresh();


            this.UpdateColorForDataGridView();


            this.Enabled = true;
            //lbInfo.Text = "Column sorted.";
           // lbInfo.Refresh();

        }

        public void StartScan()
        {
            Form scan = new ScanForm();
            DialogResult r = scan.ShowDialog();

            if (r == DialogResult.OK)
            {
                this.dataGridView1.CellClick -= this.dataGridView1_CellClick;
                lableSwitch(false);
                rbAll.Enabled = rbMissingOrMismatched.Enabled = false;
                menuStrip1.Enabled = false;
 
                
                myData.RemoveDataSource();
                Thread th = new Thread(new ThreadStart(myData.ScanProducts));
                th.Start();
               
                 
            }
        }

        private void Form1_Shown(object sender, EventArgs e)
        {
            StartScan();
        }

        private void scanToolStripMenuItem_Click(object sender, EventArgs e)
        {
            StartScan();
        }

        private void exportToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (myData.rows.Count == 0)
            {
                MessageBox.Show("Empty result. Nothing to export.", "Empty Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }




            System.Windows.Forms.SaveFileDialog dialog = new SaveFileDialog();

            string fileNameTXT = "";
            string fileNameCSV = "";

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                string name = Path.GetFileName(dialog.FileName);
                string shortname = Path.GetFileNameWithoutExtension(dialog.FileName);
                string path = dialog.FileName.Replace(name, "");
                fileNameTXT = Path.Combine(path, shortname + ".txt");
                fileNameCSV = Path.Combine(path, shortname + ".csv");

            }
            else return;

            lbInfo.Visible = true;
            this.Enabled = false;
            lbInfo.Text = "Export data to " + fileNameTXT + " as text file, may take minutes...";
            lbInfo.Refresh();
            Logger.LogMsg("Export data to " + fileNameTXT + ", may take minutes...");


            string result = Output.FormatListTXT<myRow>(myData.rows);
            File.WriteAllText(fileNameTXT, result);

            lbInfo.Text = "Export data to " + fileNameCSV + " as csv file, may take minutes...";
            lbInfo.Refresh();
            Logger.LogMsg("Export data to " + fileNameCSV + ", may take minutes...");


            string resultCSV = Output.FormatListCSV<myRow>(myData.rows);
            File.WriteAllText(fileNameCSV, resultCSV);



            lbInfo.Visible = false;
            this.Enabled = true;
            lbInfo.Text = "Done.";
            lbInfo.Refresh();
            Logger.LogMsg("Export done.");

            MessageBox.Show("Report saved to:\n" + fileNameCSV + "\n" + fileNameTXT, "File Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);

        }

        private void logToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!File.Exists(Logger.logFileName)) return;
            var process = new Process();
            process.StartInfo.FileName = "notepad.exe";
            process.StartInfo.Arguments = Logger.logFileName;

            process.Start();
        }

        private void fixAllToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            String warning = "Do you want to fix those missing/mismatched msi/msp automatically ?";
            DialogResult result = MessageBox.Show(warning, "Confirmation",
                MessageBoxButtons.OKCancel, MessageBoxIcon.Question);
            if (result == DialogResult.OK)
            {

                this.menuStrip1.Enabled = false;
                rbAll.Enabled = rbMissingOrMismatched.Enabled = false;
                this.dataGridView1.Enabled = false;

                int count = FixAll();

                MessageBox.Show("Done. Fixed: " + count + " items.", "Fix All");
               // lbInfo.Text = "Fixed: " + count + " items.";
                Logger.LogMsg("Fixed: " + count + " items.");

                this.menuStrip1.Enabled = true;
                rbAll.Enabled = rbMissingOrMismatched.Enabled = true;
                this.dataGridView1.Enabled = true;

            }

        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (MessageBox.Show("Are you sure you want to exit?", "Exit Confirmation", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.No)
            {

                e.Cancel = true;

            }
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string ver = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            ver = ver.Replace(".0.0", "");

            MessageBox.Show("FixMissingMSI, Version " + ver + "\nA tool to fix missing/mismatched installer cached MSI/MSP files\nBy Simon Su @Microsoft, 2018.1.22", "About", MessageBoxButtons.OK, MessageBoxIcon.Information);

        }

        private void Find()
        {
            Form search = new FindWhat();
            DialogResult r = search.ShowDialog();
            if (r == DialogResult.Yes)
            {
               


            }
            else if (r == DialogResult.No)
            {
                MessageBox.Show("Key not found!", "Not Found", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }

           
        }
        private void findToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            Find();
        }

        private void findNextToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(myFind.lastFindText))
            {

                bool isFound = myFind.Find(myFind.lastFindText);
                if (isFound)
                { 

                }
                else
                    MessageBox.Show("Key not found!", "Not Found", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);


            }
        }

        private void dataGridView1_SelectionChanged(object sender, EventArgs e)
        {

            var cnt = dataGridView1.SelectedCells.Count;
            if (cnt <= 0) return;

            int minRowIdx = Int32.MaxValue;
            int minCellIdx = Int32.MaxValue;
            foreach (var  cell in dataGridView1.SelectedCells)
            {
                if (cell.GetType() == typeof(DataGridViewTextBoxCell))
                {
                    DataGridViewTextBoxCell c = (DataGridViewTextBoxCell)cell;
                    minRowIdx = Math.Min(minRowIdx, c.RowIndex);
                    minCellIdx = Math.Max(Math.Min(minCellIdx, c.ColumnIndex), 0);
                }
            }

            myFind.cellIdx = minCellIdx;
            myFind.rowIdx = minRowIdx;
            myFind.IncreaseStep();

        }

        private void manualToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string path = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            //string path2 = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().GetName().CodeBase);

            path = Path.Combine(path, "Manual");
            path = Path.Combine(path, "FixMissingMSI Readme.pdf");

            Process.Start("explorer.exe", path);
        }
    }

}
