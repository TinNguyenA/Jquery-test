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

namespace WPF.PSE.Utility.UserControls
{
    public partial class TextSearch : UserControl, IPSUserControl
    {
        private SQLTrackingViewModel _viewModel = null;
        private UserControl _tabUserControl = null;
        private IDictionary _mCookies;
        private static PowerShell _ps;
        private DataTable ResultData = new DataTable("ResultLog");
        private int backupCount = 0;
        public string[] extList = { "*.cs", "*.xml", "*.csproj", "*.vbproj", "*.sql", "*.txt", "*.ps1", "*.feature", "*.dat", "*.log", "*.*" };

        public TextSearch(IDictionary cookie)
        {
            InitializeComponent();
            // Connect to instance of the view model created by the XAML
            _viewModel = (SQLTrackingViewModel)this.Resources["viewModel"];
            _viewModel.DisplayStatusMessage("");
            _mCookies = cookie;
            CbxExt.ItemsSource = extList.ToArray();
            CbxExt.SelectedIndex = 7;
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

        private void CreateParametersSearch()
        {
            _ps.AddParameter("path", txtLocation.Text);
            _ps.AddParameter("ext", CbxExt.SelectedValue.ToString());
            _ps.AddParameter("txt", TextBlockSearchText.Text);
            _ps.AddParameter("case_Ss", (bool)ChkCase.IsChecked? "Yes":""); 
        }

        private void CreateParametersSearchReplace()
        {
            _ps.AddParameter("path", txtLocation.Text);
            _ps.AddParameter("ext", CbxExt.SelectedValue.ToString());
            _ps.AddParameter("txtOld", TextBlockSearchText.Text);
            _ps.AddParameter("txtNew", TextBlockReplaceText.Text);
        }


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

            ResultGrid.ItemsSource = ResultData;
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

        private void ToggleServiceForDBRestore(PowerShell ps, bool isStop)
        {
            try
            {
                //if (runspace != null)
                //    _ps.Runspace = runspace;
                string path = Path.Combine(Environment.CurrentDirectory, "PsScriptLib", isStop ? @"StopServices.PS1" : "StartServices.PS1");
                if (!File.Exists(path))
                {
                    throw new Exception($"{ path } does not exists.");
                }
                //PS Script file has to be runing no error. No error handle in this code.
                string scriptFile = File.ReadAllText(path);
                ps.Commands.Clear();
                ps.AddScript(scriptFile);
                var test = ps.Invoke();
            }
            catch (Exception ex)
            {
                _viewModel.DisplayStatusMessage($" Err { ex.Message}");
                _viewModel.PublishException(ex);
                ps.Stop();
                ps.Dispose();
                throw;
            }
        }

        public void OnSelectedGeoDocument(HyperlinkEditRequestNavigationEventArgs args)
        {
            var test = args.Source;

            // use args.Source to get access to the HyperlinkEdit object
        }
        private void btnFind_Click(object sender, RoutedEventArgs e)
        {
            this.Cursor = System.Windows.Input.Cursors.Wait;
            ResultData.Clear();
            ResultGrid.ItemsSource = ResultData;
            _ps = PowerShell.Create();
            try
            {
                //if (runspace != null)
                //    _ps.Runspace = runspace;
                string path = Path.Combine(Environment.CurrentDirectory, "PsScriptLib", @"TextSearch.ps1");
                if (!File.Exists(path))
                {
                    throw new Exception($"{ path } does not exists.");
                }
                //PS Script file has to be runing no error. No error handle in this code.
                string scriptFile = File.ReadAllText(path);
                _ps.Commands.Clear();
                //_ps.Streams.Verbose.DataAdded += new EventHandler<DataAddedEventArgs>(DataCopyStatusAdded);
                _ps.AddScript(scriptFile);
                CreateParametersSearch();
                Collection<PSObject> collection = _ps.Invoke();
                if (collection.Count == 1)
                {
                    if (collection.First().BaseObject.ToString().StartsWith("Ran into an issue"))
                        throw new Exception(collection.First().BaseObject.ToString());
                }
                GetUpdateStatus(collection);
            }
            catch (Exception ex)
            {
                _viewModel.DisplayStatusMessage($" Err { ex.Message}");
                _viewModel.PublishException(ex);
            }
            finally
            {
                _ps.Stop();
                _ps.Dispose();
                this.Cursor = System.Windows.Input.Cursors.Arrow;
            }
            _viewModel.DisplayStatusMessage($"Search is Completed. Found ({backupCount - 1}) matched.");
        }

        private void btnReplace_Click(object sender, RoutedEventArgs e)
        {
            this.Cursor = System.Windows.Input.Cursors.Wait;
            MessageBoxResult messageBoxResult = MessageBox.Show("Are you sure?", "Replace Text Confirmation", System.Windows.MessageBoxButton.YesNo);
            if (messageBoxResult == MessageBoxResult.No)
                return;
            try
            {
                _ps = PowerShell.Create();
                //if (runspace != null)
                //    _ps.Runspace = runspace;
                string path = Path.Combine(Environment.CurrentDirectory, "PsScriptLib", @"TextReplace.ps1");
                if (!File.Exists(path))
                {
                    throw new Exception($"{ path } does not exists.");
                }
                //PS Script file has to be runing no error. No error handle in this code.
                string scriptFile = File.ReadAllText(path);
                _ps.Commands.Clear();
                //_ps.Streams.Verbose.DataAdded += new EventHandler<DataAddedEventArgs>(DataCopyStatusAdded);
                _ps.AddScript(scriptFile);
                CreateParametersSearchReplace();
                Collection<PSObject> collection = _ps.Invoke();
            }
            catch (Exception ex)
            {
                _viewModel.DisplayStatusMessage($" Err { ex.Message}");
                _viewModel.PublishException(ex);
            }
            finally
            {
                _viewModel.DisplayStatusMessage("Replace Text is Completed.");
                _ps.Stop();
                _ps.Dispose();
                this.Cursor = System.Windows.Input.Cursors.Arrow;
            }          
        }

        private void copyText_OnClick(object sender, HyperlinkEditRequestNavigationEventArgs e)
        {
            string theText = ((HyperlinkEdit)(sender)).ActualNavigationUrl;
            Clipboard.SetDataObject(theText);
        }
        private void copyText_OnClickWithOpenEditor(object sender, HyperlinkEditRequestNavigationEventArgs e)
        {
            string theText = ((HyperlinkEdit)(sender)).ActualNavigationUrl;
            Clipboard.SetDataObject(theText);

            //Process process = new Process();
            //ProcessStartInfo startInfo = new ProcessStartInfo("devenv.exe", $@"/edit {theText}");
            //process.StartInfo = startInfo;
            //process.Start();

        }
        private void ChkFixString_Click1(object sender, RoutedEventArgs e)
        {
            if (this.TextBlockSearchText.Text.Trim() != "")
            {
                    this.TextBlockSearchText.Text = string.Concat(this.TextBlockSearchText.Text.Select(x => x == ' ' ? "" : x.ToString())).TrimStart(' ');
            }
        }
        private void ChkFixString_Click(object sender, RoutedEventArgs e)
        {
            if (this.TextBlockSearchText.Text.Trim() != "")
            {
                if (ChkFixString.IsChecked.HasValue && ChkFixString.IsChecked == true)
                    this.TextBlockSearchText.Text = string.Concat(this.TextBlockSearchText.Text.Select(x => Char.IsUpper(x) ? " " + x : x.ToString())).TrimStart(' ');
            }
        }
    }
}
