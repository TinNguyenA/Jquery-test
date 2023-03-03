using System.IO;
using System.Windows;
using System.Windows.Controls;
using WPF.PSE.ViewModelLayer;
using System;
using System.Collections;
using System.Management.Automation;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using DevExpress.Xpf.Editors;
using System.Collections.Generic;
using System.Windows.Input;
using System.Windows.Media;
using System.Diagnostics;
using DevExpress.Xpf.Grid;
using Microsoft.Win32;
using System.Windows.Forms;
using Common.PSELibrary;
using WPF.PSE.AppLayer.DataObject;
using WPF.PSE.Common;
using WPF.PSE.Utility.Properties;
using System.Security;
using Common.PSELibrary.CustomObjects;

namespace WPF.PSE.Utility.UserControls
{
    public partial class FileManager : System.Windows.Controls.UserControl, IPSUserControl
    {
        private FileExploreViewModel _viewModel = null;
        private System.Windows.Controls.UserControl _tabUserControl = null;
        private IDictionary _mCookies;
        private static PowerShell _ps;
        private string strMsg = "";
        private DataTable ResultData = new DataTable("ResultLog");
        private int backupCount = 0;
        public string[] extList = { "*.cs", "*.xml", "*.csproj", "*.vbproj", "*.sql", "*.txt", "*.ps1", "*.feature", "*.dat", "*.log", "*.*" };

        public FileManager(IDictionary cookie)
        {
            InitializeComponent();
            // Connect to instance of the view model created by the XAML
            _viewModel = (FileExploreViewModel)this.Resources["viewModel"];
            _viewModel.DisplayStatusMessage("");
            _mCookies = cookie;
            ResetFileManagerControls();
            LoadFileManagerControls(_viewModel.LoadFileHistFromXML());
          //  MessageBroker.Instance.MessageReceived += Instance_MessageFileCopyReceived;

        }

        private void ResetFileManagerControls()
        {
            cbxSourceFolder.Items.Clear();
            cbxDestFolder.Items.Clear();
            cbxIncludeExt.Items.Clear();
            cbxExtExcluded.Items.Clear();
            cbxDescription.Items.Clear();
        }

        private void LoadFileManagerControls(ArrayOfFileManagerDataFileManagerData[] fileManagerDatas)
        {//Selected="LoadOtherControls"
            cbxDescription.ItemsSource =
            cbxIncludeExt.ItemsSource =
            cbxExtExcluded.ItemsSource =
            cbxDestFolder.ItemsSource =
            cbxSourceFolder.ItemsSource = fileManagerDatas;

            cbxDestFolder.SelectionChanged += LoadOtherControlsButSelected;
            cbxIncludeExt.SelectionChanged += LoadOtherControlsButSelected;
            cbxExtExcluded.SelectionChanged += LoadOtherControlsButSelected;
            cbxDescription.SelectionChanged += LoadOtherControlsButSelected;
        }

        private void LoadOtherControlsButSelected(object sender, SelectionChangedEventArgs e)
        {
            int index = ((System.Windows.Controls.ComboBox)sender).SelectedIndex;
            if (index == -1)
                return;           
            cbxSourceFolder.SelectedIndex = index;
            cbxDestFolder.SelectedIndex = index;
            cbxIncludeExt.SelectedIndex = index;
            cbxExtExcluded.SelectedIndex = index;
            cbxDescription.SelectedIndex = index;

            btnExecute.IsEnabled = index > 0;
        }

        private void SetGridInitialLayout(object sender, RoutedEventArgs e)
        {
            if (ResultData.Columns.Contains("colDetails"))
                return;
            var keys = new DataColumn[2];
            ResultData.Columns.Add("colLineNumber", typeof(int));
            ResultData.Columns.Add("colDetails", typeof(string));
            ResultData.Columns.Add("colFileName", typeof(string));
            ResultData.Columns.Add("colLocation", typeof(string));
            keys[0] = ResultData.Columns[0];
            keys[1] = ResultData.Columns[2];
            ResultData.PrimaryKey = keys;
        }
        public double MWidth
        {
            get { return this.Width; }
            set { this.Width = value; }
        }
        public double MHeight
        {
            get { return this.Height; }
            set { this.Height = value + 120; }
        }
        public double TabWidth
        {
            get { return this.Width; }
            set
            {
                //this.Width = value;
            }
        }
        public double TabHeight
        {
            get { return ((TabPageControl)_tabUserControl).Height; }
            set
            {
                this.Height = value + 120;
            }
        }
        public string PSEnvironment { get; set; }
        public string TxtEditor { get; private set; }

        private void DataCopyStatusAdded(object sender, DataAddedEventArgs e)
        {
            string msg = ((PSDataCollection<VerboseRecord>)sender)[e.Index].ToString();
        }

        private void GetUpdateStatus(Collection<PSObject> collection)
        {
            backupCount = 1;
            foreach (PSObject result in collection)
            {
                ResultData.Rows.Add(
                    result.Members["LineNumber"].Value.ToString(),
                    result.Members["Line"].Value.ToString().TrimStart(),
                    result.Members["Filename"].Value.ToString(),
                    result.Members["Path"].Value.ToString());
                backupCount++;
            }

            //ResultGrid.ItemsSource = ResultData;
        }

        private string CleanUpFilter(string filter, Hashtable columnValues)
        {
            IEnumerator enumerator = columnValues.Keys.GetEnumerator();
            if (enumerator.MoveNext())
            {
                object first = enumerator.Current;
                return filter + first + " in(" + columnValues[first] + ")";
            }
            return "";
        }
              
        private void copyText_OnClick(object sender, HyperlinkEditRequestNavigationEventArgs e)
        {
            string theText = ((HyperlinkEdit)(sender)).ActualNavigationUrl;
            System.Windows.Clipboard.SetDataObject(theText);
        }
        private void copyText_OnClickWithOpenEditor(object sender, HyperlinkEditRequestNavigationEventArgs e)
        {
            string theText = ((HyperlinkEdit)(sender)).ActualNavigationUrl;
            System.Windows.Clipboard.SetDataObject(theText);

            //Process process = new Process();
            //ProcessStartInfo startInfo = new ProcessStartInfo("devenv.exe", $@"/edit {theText}");
            //process.StartInfo = startInfo;
            //process.Start();

        }
        private void btnExecuteCopy_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                this.Cursor = System.Windows.Input.Cursors.Pen;
                //string strTempPath = Path.GetDirectoryName(System.Windows.Forms.Application.StartupPath) + "\\Template\\UnderConst";

                // this.label_Status.Text = "Please wait, system is processing .......";
                // label_Status.Update();
                // button_openLog.Tag = Environment.MachineName + DateTime.Now.Minute.ToString();

                strMsg = ""; 
                if (ChkReplaceBySourceFileName.IsChecked == true)
                {
                    int copyN = CommonFunctions.DoFileReplaceOnly(cbxSourceFolder.Text, cbxDestFolder.Text, cbxIncludeExt.Text.Replace("*.", "."), ref strMsg);
                    this.Cursor = System.Windows.Input.Cursors.Arrow;
                    File.WriteAllText(Resource.FileManagerLog + "//MyCopy" + cbxDescription.Text.Replace(" ", "") + ".log", strMsg + "Copied: " + copyN.ToString() + " Files.");
                    _viewModel.DisplayStatusMessage("Job Completed.");
                    return;
                }
                
                if (ChkDelTargetBfCopy.IsChecked == true)
                {
                    //strMsg += "***** Delete All Files in Destination\r\n";
                    DirectoryInfo DinFo = new DirectoryInfo(cbxDestFolder.Text);
                    if (DinFo.Exists)
                    {
                        DinFo.Delete(true);
                    }
                }
                strMsg += "Copy from " + cbxSourceFolder.Text + " to " + cbxDestFolder.Text + "\r\n";
                int copyNo = CommonFunctions.GetCopyFileList(ChkIncludeSubDir.IsChecked,
                                                            chkMergeTo1Dir.IsChecked == true, ref strMsg,
                                                            cbxIncludeExt.Text.Replace("*.", "."),
                                                            cbxExtExcluded.Text.Replace("*.", "."),
                                                            ChkOverrideReadOnly.IsChecked == true,
                                                            new DirectoryInfo(cbxSourceFolder.Text),
                                                            0,
                                                             cbxSourceFolder.Text,
                                                             cbxDestFolder.Text);
                if (ChkSynFiles.IsChecked == true)
                {
                    strMsg += "\n\rCopied: " + copyNo.ToString() + " Files.";
                    strMsg += "\n\r Copy from " + cbxDestFolder.Text + " to " + cbxSourceFolder.Text + "\r\n";
                    copyNo += CommonFunctions.GetCopyFileList(ChkIncludeSubDir.IsChecked,
                                                            chkMergeTo1Dir.IsChecked == true, ref strMsg,
                                                            cbxIncludeExt.Text.Replace("*.", "."),
                                                            cbxExtExcluded.Text.Replace("*.", "."),
                                                            ChkOverrideReadOnly.IsChecked == true,
                                                            new DirectoryInfo(cbxDestFolder.Text),
                                                            0,
                                                            cbxDestFolder.Text,
                                                            cbxSourceFolder.Text);
                }

                btnViewLog.IsEnabled = true;
                if (!Directory.Exists(Resource.FileManagerLog))
                    Directory.CreateDirectory(Resource.FileManagerLog);
                File.WriteAllText(Resource.FileManagerLog + "//MyCopy" + cbxDescription.Text.Replace(" ", "") + ".log", strMsg + "Copied: " + copyNo.ToString() + " Files.");
                _viewModel.DisplayStatusMessage("Job Completed.");
            }
            catch (Exception ex)
            {
                //_viewModel.DisplayPopUpMessage(new MessageView()
                //{
                //    InfoMessage = $"{ex}.",
                //    InfoMessageTitle = $"File copy/move error",
                //    IsInfoMessageVisible = true
                //});
                MessageBroker.Instance.SendMessage(MessageBrokerMessages.FILE_COPY_ISSUE);
                _viewModel.DisplayStatusMessage("Error File copy");
            }
            this.Cursor = System.Windows.Input.Cursors.Arrow;
            _viewModel.DisplayStatusMessage("File copy completed");
        }

        private void btnReRefresh_Click(object sender, RoutedEventArgs e)
        {
            LoadFileManagerControls(_viewModel.LoadFileHistFromXML());
        }

        private void btnViewLog_Click(object sender, RoutedEventArgs e)
        {
            string strPathAndFileName = Resource.FileManagerLog + "\\MyCopy" + cbxDescription.Text.Replace(" ", "") + ".log";
            if (File.Exists(strPathAndFileName))
            {
                try
                {
                    Process.Start(@"C:\Program Files\Notepad++\notepad++.exe", strPathAndFileName);
                }
                catch
                {
                    Process.Start("notepad.exe", strPathAndFileName);
                }
            }
        }

        private void btnOpenConfig_Click(object sender, RoutedEventArgs e)
        {
            string strPathAndFileName = System.Windows.Forms.Application.StartupPath + "\\Data\\" + Environment.MachineName + ".xml";
            if (File.Exists(strPathAndFileName))
            {
                try
                {
                    Process.Start(@"C:\Program Files\Notepad++\notepad++.exe", strPathAndFileName);
                }
                catch
                {
                    Process.Start("notepad.exe", strPathAndFileName);
                }
            }
        }

        private void btnRemoveFile_Click(object sender, RoutedEventArgs e)
        {
            DialogResult dialogResult = System.Windows.Forms.MessageBox.Show("Are you sure?", "Delete Confirmation", MessageBoxButtons.YesNo);

            if (dialogResult == DialogResult.No)
            {
                return;
            }

            var strSource = ((System.Windows.Controls.Button)sender).Name;
            string paramInfo = "";
            if (strSource == "btnRemoveFileSource")
            {
                paramInfo = cbxSourceFolder.Text;
                if (paramInfo == "")
                {
                    _viewModel.DisplayStatusMessage("Must select a Source Folder first");
                    return;
                }
            }
            else
            {
                paramInfo = cbxDestFolder.Text;
                if (paramInfo == "")
                {
                    _viewModel.DisplayStatusMessage("Must select a Destination Folder first");
                    return;
                }
            }

            strMsg = "";
            int copyNo = 0;
            string ext = TxtCleanSource.Text.Trim();
            if (string.IsNullOrEmpty(ext))
                return;
            else
                ext = ext.ToLower();
            DirectoryInfo DinFo = new DirectoryInfo(paramInfo);
            if (DinFo.Exists)
            {
                string[] deleteMe = ext.Split('.');
                foreach (string s in deleteMe)
                {
                    if (s.Trim().Length > 1)
                        copyNo += ReCursiveDelete("." + s.Trim(), DinFo, 0);
                }
                btnOpenConfigFile.IsEnabled = true;
                File.WriteAllText(System.Windows.Forms.Application.StartupPath +
                    "\\MyDelete.log", strMsg + "Deleted: " + copyNo.ToString() + " Files.");
            }
        }
        private int ReCursiveDelete(string ext, DirectoryInfo DinFo, int count)
        {
            foreach (FileInfo ifo in DinFo.GetFiles())
            {
                if (ifo.Extension.Equals(ext))
                {
                    FileAttributes at = ifo.Attributes;
                    if (at.ToString().Contains(FileAttributes.ReadOnly.ToString()))
                        ifo.Attributes = FileAttributes.Normal;
                    ifo.Delete();
                    count++;
                    strMsg += ifo.FullName + "\r\n";
                }
            }
            foreach (DirectoryInfo dfo in DinFo.GetDirectories())
            {
                count += ReCursiveDelete(ext, dfo, 0);
            }
            return count;
        }

        private void btnBrowseDestFolder_Click(object sender, RoutedEventArgs e)
        {
            FolderBrowserDialog openFileDialog = new FolderBrowserDialog();

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                cbxDestFolder.Text = openFileDialog.SelectedPath;
            }

        }

        private void btnBrowseSourceFolder_Click(object sender, RoutedEventArgs e)
        {
            FolderBrowserDialog openFileDialog = new FolderBrowserDialog();

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                cbxSourceFolder.Text = openFileDialog.SelectedPath;
            }
        }

        private void ViewFolder(object sender, MouseButtonEventArgs e)
        {
            var strSource = ((Image)sender).Name;
            string strProgram;
            string argument;
            strProgram = "explorer.exe";
            argument = "";
            if (strSource == "Source")
            {
                if (this.cbxSourceFolder.Text.Trim().Length > 0)
                {
                    argument = IsLocalPath(cbxSourceFolder.Text) ?
                               "/e," + this.cbxSourceFolder.Text :
                               "/e,/root," + this.cbxSourceFolder.Text;
                }
            }
            else
            {
                if (this.cbxDestFolder.Text.Trim().Length > 0)
                {
                    argument = IsLocalPath(cbxDestFolder.Text) ?
                               "/e," + this.cbxDestFolder.Text :
                               "/e,/root," + this.cbxDestFolder.Text ;
                }
            }

            if (argument == "")
                return;

            if (IsLocalPath(argument))
            {
                Process proc = Process.Start(strProgram, argument);
            }
            else
            {
                SecureString securePass = new SecureString();
                foreach (char c in Resource.ImpersonatedKey)
                {
                    securePass.AppendChar(c);
                }
                Process proc = Process.Start("C:\\Windows\\" +strProgram, argument, Resource.ImpersonatedName,
                   securePass, Resource.DomainName);
                proc = Process.Start(strProgram, argument);
            }
        }
        private void btnSaveSettting_Click(object sender, RoutedEventArgs e)
        {
            ArrayOfFileManagerDataFileManagerData[] data = _viewModel.LoadFileHistFromXML();
            ArrayOfFileManagerDataFileManagerData newRecord = new ArrayOfFileManagerDataFileManagerData();
            newRecord.FileType = cbxIncludeExt.Text;
            newRecord.FileTypeExcept = cbxExtExcluded.Text;
            newRecord.HistDest = cbxDestFolder.Text;
            newRecord.HistSource = cbxSourceFolder.Text;
            newRecord.Description = cbxDescription.Text;
            newRecord.insertedDT = DateTime.Now.ToString();
            var id =  data.Select(x => float.Parse(((ArrayOfFileManagerDataFileManagerData)x).HistSynID)).ToList();
            id.Sort();
            int flNewId = int.Parse(id[id.Count - 1].ToString()) +1 ;
            var newUpdate = data.FirstOrDefault(x => x.Description.Trim().ToLower() == cbxDescription.Text.Trim().ToLower());
            if (newUpdate!=null)
            {
                _viewModel.SaveToFileManagerConfig(newRecord, float.Parse(newUpdate.HistSynID));
            }
            else
            {
                newRecord.HistSynID = flNewId.ToString();
                _viewModel.SaveToFileManagerConfig(newRecord, -1);
            }

            // _viewModel.SaveHistToXMLFile(data, System.Windows.Forms.Application.StartupPath + "\\Data\\");
        }
        private bool IsLocalPath(string argument)
        {
            return true;
            //return argument.ToLower().Contains(":\\");
        }

        private void RemoveOptionsForFileReplaceSpecial(object sender, RoutedEventArgs e)
        {
            if(ChkReplaceBySourceFileName.IsChecked == true)
            {
                ChkOverrideReadOnly.IsChecked = true;
                ChkOverrideReadOnly.IsEnabled = false;

                ChkIncludeSubDir.IsChecked = true;
                ChkIncludeSubDir.IsEnabled = false;

                ChkSynFiles.IsChecked = false;
                ChkSynFiles.IsEnabled = false;

                ChkDelTargetBfCopy.IsChecked = false;
                ChkDelTargetBfCopy.IsEnabled = false;

                ChkSkipNewerFiles.IsChecked = false;
                ChkSkipNewerFiles.IsEnabled = false;

                chkMergeTo1Dir.IsChecked = false;
                chkMergeTo1Dir.IsEnabled = false;
            }
            else
            {
                ResetAllCheckBox();                
            }
        }

        private void ResetAllCheckBox()
        {
            ChkOverrideReadOnly.IsChecked = true;
            ChkOverrideReadOnly.IsEnabled = true;

            ChkIncludeSubDir.IsChecked = true;
            ChkIncludeSubDir.IsEnabled = true;

            ChkSynFiles.IsChecked = false;
            ChkSynFiles.IsEnabled = true;

            ChkDelTargetBfCopy.IsChecked = false;
            ChkDelTargetBfCopy.IsEnabled = true;

            ChkSkipNewerFiles.IsChecked = true;
            ChkSkipNewerFiles.IsEnabled = true;

            chkMergeTo1Dir.IsChecked = false;
            chkMergeTo1Dir.IsEnabled = true;
        }
    }
}
