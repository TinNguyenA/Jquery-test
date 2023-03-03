using System;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Management.Automation;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WPF.PSE.AppLayer;
using WPF.PSE.AppLayer.DataObject;
using WPF.PSE.ViewModelLayer;
using WPF.PSE.AppLayer.Models;
using System.Management.Automation.Runspaces;
using System.Security;
using System.Diagnostics;
using System.Windows.Threading;
using WPF.PSE.Utility.Properties;
using System.Collections.Generic;
using System.Threading;
using System.Text;
using System.Windows.Media.Imaging;
using Common.PSELibrary.CustomObjects;
using WPF.PSE.Common;

namespace WPF.PSE.Utility.UserControls
{
    /// <summary>
    /// Interaction logic for TabPagesTemplate.xaml
    /// </summary>
    public partial class TabPagesTemplate : UserControl
    {
        #region Memembers and properties
        private string _Id;
        private string SelectIP;
        private const string _DefaultSelectedTab = "127.0.0.0";
        private static PowerShell _ps;
        private DataTable LogEntry = new DataTable("ResultLog");
        private ServerListObjectViewModel _viewModel = null;
        private Runspace _runspace;
        private CancellationTokenSource isCancel;
        delegate void SetOutput(string value);

        public double MWidth
        {
            get { return this.Width; }
            set { this.Width = value; }
        }
        public double MHeight
        {
            get { return this.Height; }
            set
            {
                this.Height = value;
            }
        }
        public void CreateTab(string header = "Localhost")
        {
            GetNewTab.Header = header;
            GetNewTab.Content = TabpagePlaceHolder;
            if (header == "Localhost")
                btnCloseTab.Visibility = Visibility.Hidden;
        }
        public TabItem GetNewTab { get; } = new TabItem();
        public bool IsParamUsed
        {
            get
            {
                return (comboLogName.IsEnabled);
            }
        }

        public string ImpersonatedKey { get { return null;  } }// private set; }

        private List<string> SupportedAttributes = new List<string>() {
                                                    "Name", "Value", "DisplayName","Status","ServiceName"};
        private List<string> UnSupportedAttributes = new List<string>() {
                                                    "ToString", "Equals", "GetHashCode","GetType","PSProvider",
                                                    "PSPath","PSParentPath","PSChildName","PSDrive", "get_Key",
                                                    "get_Value", "set_Key","set_Value","PSIsContainer"};

        #endregion

        #region Constructor / ini method
        public TabPagesTemplate(string name = null, string ip = null)
        {
            _viewModel = (ServerListObjectViewModel)this.Resources["viewModel"];
            InitializeComponent();
            if (ip == null)
            {
                _Id = _DefaultSelectedTab;
                CreateTab();
            }
            else
            {
                _Id = name.Trim();
                SelectIP = ip.Trim();
                CreateTab(name);
            }
            
            LogEntry.Columns.Add("Date", typeof(DateTime));
            LogEntry.Columns.Add("LevelDisplayName", typeof(ImageSource));
            LogEntry.Columns.Add("Event", typeof(string));           
            LogEntry.Columns.Add("Source", typeof(string));
        }

        /// <summary>
        /// Load All dropdown List for Filtering
        /// </summary>
        internal void LoadData()
        {
            _viewModel = (ServerListObjectViewModel)this.Resources["viewModel"];

            //Load filter cbxs
            comboLogName.ItemsSource = _viewModel.LoadEventLogInfo();

            Stream stream = new DataContext().GetStream("EventLogFilter.xml");
            Events filterEvent = CommonFunctions.ConvertXMLToClassObject<Events>(stream);
            stream.Close();
            foreach (EventsEventLogFilter logName in filterEvent.Items)
            {
                comboEventType.Items.Add(logName);
            }
            stream = new DataContext().GetStream("PsCommand.xml");
            //comboCommandName
            PSCommands commnands = CommonFunctions.ConvertXMLToClassObject<PSCommands>(stream);
            stream.Close();
            foreach (PSCommandsPSCommand cmdName in commnands.Items)
            {
                if (this._Id == _DefaultSelectedTab && cmdName.type != Resource.TypeDevToolCommand)
                    comboCommandName.Items.Add(cmdName);
                else
                {
                    if (cmdName.type == Resource.TypeEventLogCommand ||
                       cmdName.type == Resource.TypeCheckCODISRegCommand)
                        comboCommandName.Items.Add(cmdName);
                }
            }
            ResultGrid.ItemsSource = LogEntry;
        }
        #endregion

        #region Run job ps1 methods
        private void GetDataFromPsCommand(Runspace runspace)
        {
            //_viewModel.DisplayStatusMessage();
           
            if (((PSCommandsPSCommand)comboCommandName.SelectedValue).type != Resource.TypeEventLogCommand)
            {

                LoadPSScriptWindow(runspace, IsParamUsed);
                return;
            }
            ResultGrid.Visibility = Visibility.Visible;
            txtOutput.Visibility = Visibility.Collapsed;

            LoadPSEventLogWindow(runspace);
        }

        /// <summary>
        /// run Assynchonously
        /// </summary>
        /// <param name="runspace"></param>
        private void LoadPSEventLogWindow(Runspace runspace)
        {
            _ps = PowerShell.Create();
            try
            {
                if (runspace != null)
                    _ps.Runspace = runspace;

                string blockFilter = @"Get-WinEvent -FilterHashtable @{
                                           logname=" + GetSelectedLogNameCbx(comboLogName) + @";" +
                                           "Level = " + GetSelectedLogTypeNameCbx(comboEventType) + @"; " +
                                           "StartTime= '" + calStartDate.SelectedDate.ToString() + @"';" +
                                           "EndTime ='" + calEndDate.SelectedDate.Value.AddHours(2).ToString() + @"'" +
                                           "} -ErrorAction 'SilentlyContinue'";
                LogEntry.Clear();
                ResultGrid.Columns[2].Header = "Message";
                _ps.AddScript(blockFilter);
                PSDataCollection<PSObject> results = new PSDataCollection<PSObject>();
                results.DataAdded += new EventHandler<DataAddedEventArgs>(Process_GridDataAdded);
                IAsyncResult rasr = _ps.BeginInvoke<PSObject, PSObject>(null, results, null, AsyncInvoke, null);
            }
            catch (Exception ex)
            {
                _viewModel.DisplayStatusMessage($" {this._Id} machine is not Accessible at this time. { ex.Message}");
                _viewModel.PublishException(ex);
                return;
            }
        }

        private string GetSelectedLogNameCbx(ComboBox comboLogName)
        {
            if (((EventLogInfoLogAvailable)comboLogName.SelectedValue).LogName != "All")
            {
                return "'" + ((EventLogInfoLogAvailable)comboLogName.SelectedValue).LogName + "'";
            }
            List<string> valuesSelected = new List<string>();
            foreach (EventLogInfoLogAvailable item in comboLogName.Items)
            {
                if (item.LogName == "All")
                    continue;
                valuesSelected.Add("'" + item.LogName + "'");
            }
            return string.Join(",", valuesSelected);
        }
        private string GetSelectedLogTypeNameCbx(ComboBox comboLogName)
        {
            if (((EventsEventLogFilter)comboLogName.SelectedValue).EventsType != "All")
            {
                return ((EventsEventLogFilter)comboLogName.SelectedValue).FilterId;
            }
            List<string> valuesSelected = new List<string>();
            foreach (EventsEventLogFilter item in comboLogName.Items)
            {
                if (item.EventsType == "All")
                    continue;
                valuesSelected.Add(item.FilterId);
            }
            return string.Join(",", valuesSelected);
        }

        /// <summary>
        /// Close connection here
        /// </summary>
        /// <param name="ar"></param>
        private void AsyncInvoke(IAsyncResult ar)
        {            
            StringBuilder sb = new StringBuilder("\r\n");
            try
            {
                foreach (var error in _ps.Streams.Debug)
                {
                    if (string.IsNullOrEmpty(error.Message))
                        continue;
                    sb.Append($"Debug sent from {this._Id}: {error.Message}\r\n");
                    Debug.WriteLine("=>" + error.Message);
                }
                foreach (var error in _ps.Streams.Information)
                {
                    if (string.IsNullOrEmpty(error.MessageData.ToString()))
                        continue;
                    sb.Append($"Information sent from {this._Id}: {error.MessageData.ToString()}\r\n");
                    Debug.WriteLine("=>" + error.MessageData.ToString());
                }
                foreach (var error in _ps.Streams.Verbose)
                {
                    if (string.IsNullOrEmpty(error.Message))
                        continue;
                    sb.Append($"Error sent from {this._Id}: {error.Message}\r\n");
                    Debug.WriteLine("=>" + error.Message);
                }
                foreach (var error in _ps.Streams.Progress)
                {
                    //Debug.WriteLine("=>" + error.StatusDescription);
                }
                foreach (var error in _ps.Streams.Warning)
                {
                    if (string.IsNullOrEmpty(error.Message))
                        continue;
                    sb.Append($"Warning sent from {this._Id}: {error.Message} \r\n");
                    Debug.WriteLine("=>" + error.Message);
                }
                _ps.EndInvoke(ar);
                if (LogEntry.Rows.Count == 0 && txtOutput.Visibility == Visibility.Collapsed)
                {
                    _viewModel.DisplayErrorMessage($"Not Found any record from {this._Id}.");
                }
                _viewModel.DisplayStatusMessage($"Last proccesing a job on {this._Id} machine.");

            }
            catch (Exception ex)
            {
                _viewModel.PublishException(ex);
            }
            finally
            {
                _ps?.Dispose();
                _runspace?.Close();
                Dispatcher.BeginInvoke(DispatcherPriority.Normal,
                    new Action(() =>
                    {
                        btnProcessFilter.Content = "Execute";
                        ClearPopup();
                    }));
            }
            if (sb.Length > 0)
            {
                txtOutput.Dispatcher.BeginInvoke(
                    new SetOutput(Execute), new object[] { sb.ToString() });
            }
        }

        private void Execute(string msg)
        {
            txtOutput.Text += msg;
            txtOutput.ForceCursor = true;
        }

        /// <summary>
        /// run Assynchonously
        /// </summary>
        /// <param name="runspace"></param>
        private void LoadPSScriptWindow(Runspace runspace, bool withParams = false)
        {
            string tabName = ((PSCommandsPSCommand)comboCommandName.SelectedItem).Description;
            txtOutput.Text = $"Starting Time: {DateTime.Now.ToLongTimeString()} \r\n------------------\r\n";

            ResultGrid.Visibility = Visibility.Collapsed;
            txtOutput.Visibility = Visibility.Visible;

            _ps = PowerShell.Create();
            if (runspace != null)
            {
                _ps.Runspace = runspace;
            }
            string scriptFile = Path.Combine(Environment.CurrentDirectory, "PsScriptLib", ((PSCommandsPSCommand)comboCommandName.SelectedValue).CommandName.Trim());
            _ps.Commands.Clear();
            _ps.Streams.Verbose.DataAdded += new EventHandler<DataAddedEventArgs>(Verbose_DataAdded);
            //_ps.Streams.Progress.DataAdded += new EventHandler<DataAddedEventArgs>(Process_DataAdded);
            _ps.Streams.Error.DataAdded += new EventHandler<DataAddedEventArgs>(Error_DataAdded);
            _ps.Streams.Debug.DataAdded += new EventHandler<DataAddedEventArgs>(Debug_DataAdded);
            var sr = new StreamReader(scriptFile);

            _ps.AddScript(sr.ReadToEnd());
            if (withParams)
            {
                CreateParameters();
            }

            PSDataCollection<PSObject> result = new PSDataCollection<PSObject>();
            result.DataAdded += new EventHandler<DataAddedEventArgs>(Process_TextDataAdded);
            _ps.InvocationStateChanged += new EventHandler<PSInvocationStateChangedEventArgs>(Powershell_InvocationStateChanged);
            // _ps.AddStatement();
            IAsyncResult invokeResult = _ps.BeginInvoke<PSObject, PSObject>(null, result, null, AsyncInvoke, null);
        }

        private void CreateParameters()
        {
            if (comboLogName.IsEnabled)
            {
                _ps.AddParameter("LogName", comboLogName.SelectedItem.ToString());
            }
            // can be more later.
        }

        private Visibility SetUpFilters(string filterType)
        {
            switch (filterType)
            {
                case "EventLog":
                    comboLogName.IsEnabled = true;
                    comboEventType.IsEnabled = true;
                    txtFilter.IsEnabled = true;
                    calStartDate.IsEnabled = true;
                    calEndDate.IsEnabled = true;

                    btnProcessFilter.Visibility = Visibility.Visible;

                    comboLogName.SelectedIndex = 2;
                    comboEventType.SelectedIndex = 0;
                    calEndDate.SelectedDate = DateTime.Now;
                    calStartDate.SelectedDate = DateTime.Today.AddDays(-1);

                    comboLogName.Foreground = new SolidColorBrush(Colors.Black);
                    comboEventType.Foreground = new SolidColorBrush(Colors.Black);
                    calEndDate.Background = new SolidColorBrush(Colors.White);
                    calStartDate.Background = new SolidColorBrush(Colors.White);
                    break;

                case "Clear Log":
                    comboLogName.IsEnabled = true;
                    comboEventType.IsEnabled = false;
                    comboLogName.SelectedIndex = 1;
                    txtFilter.IsEnabled = false;
                    calStartDate.IsEnabled = false;
                    calEndDate.IsEnabled = false;

                    btnProcessFilter.Visibility = Visibility.Collapsed;

                    comboLogName.Foreground = new SolidColorBrush(Colors.Black);
                    comboEventType.Foreground = new SolidColorBrush(Colors.White);
                    calEndDate.Background = new SolidColorBrush(Colors.Gray);
                    calStartDate.Background = new SolidColorBrush(Colors.Gray);
                    break;
                default:
                    comboLogName.IsEnabled = false;
                    comboEventType.IsEnabled = false;
                    txtFilter.IsEnabled = false;
                    calStartDate.IsEnabled = false;
                    calEndDate.IsEnabled = false;

                    btnProcessFilter.Visibility = Visibility.Collapsed;

                    comboLogName.Foreground = new SolidColorBrush(Colors.White);
                    comboEventType.Foreground = new SolidColorBrush(Colors.White);
                    calEndDate.Background = new SolidColorBrush(Colors.Gray);
                    calStartDate.Background = new SolidColorBrush(Colors.Gray);
                    break;
            }
            return Visibility.Visible;
        }

        private bool FilteredAttributes(PSMemberInfoCollection<PSMemberInfo> members)
        {
            return (members.Any(x => x.Name == "CanStop"));
        }
        public void UpdateResultGrid(IEnumerable<PSObject> resultFilter)
        {
            foreach (PSObject result in resultFilter)
            {
                try
                {
                    DateTime time = DateTime.Now;
                    string message = "";
                    string level = "";
                    if (result.Members["TimeCreated"] != null)
                    {
                        time = DateTime.Parse(result.Members["TimeCreated"].Value?.ToString());
                    }
                    if (result.Members["Message"] != null)
                    {
                        if (string.IsNullOrEmpty(txtFilter.Text.Trim()))
                        {
                            message = result.Members["Message"].Value?.ToString();
                        }
                        else if ((result.Members["Message"].Value?.ToString()).ToLower().Contains(txtFilter.Text.Trim().ToLower()))
                        {
                            message = result.Members["Message"].Value?.ToString();
                        }
                        else
                            continue;
                    }
                    if (result.Members["LevelDisplayName"] != null)
                    {
                        level = result.Members["LevelDisplayName"].Value?.ToString();
                    }
                    
                    LogEntry.Rows.Add(time,
                                      GetImage($"pack://application:,,,/WPF.PSE.Common;Component/Images/{level}.png"),
                                      message,                         
                                      ((PSProperty)result.Members["LogName"]).Value);
                }
                catch //(Exception ex)
                {
                    continue;
                }

            }
            ResultGrid.Columns[2].Header = $"Message ({LogEntry.Rows.Count})";
            _viewModel.DisplayStatusMessage($"Process Completed on {this.Name}");
            ClearPopup();
        }

        private ImageSource GetImage(string path)
        {
            return new BitmapImage(new Uri(path, UriKind.Absolute));
        }

        #endregion

        #region Events
        private void Powershell_InvocationStateChanged(object sender, PSInvocationStateChangedEventArgs e)
        {
            Console.WriteLine("PowerShell object state changed: state: {0}\n", e.InvocationStateInfo.State);
            if (e.InvocationStateInfo.State == PSInvocationState.Completed)
            {
                txtOutput.Dispatcher.BeginInvoke(new SetOutput(Execute), new object[] { $"\r\nProcessing completed at. {DateTime.Now.ToLongTimeString()}" });
            }
        }

        private void LoadFilterSet(object sender, SelectionChangedEventArgs e)
        {
            btnProcessFilter.Visibility = SetUpFilters(((PSCommandsPSCommand)comboCommandName.SelectedValue).type);
        }
        private void ButtonProcessCommand_Click(object sender, RoutedEventArgs e)
        {
            this.Cursor = System.Windows.Input.Cursors.Pen;
            _viewModel.DisplayPopUpMessage(new MessageView()
            {
                InfoMessage = $"Processing job on {this._Id} machine ... Please wait.",
                InfoMessageTitle = comboCommandName.Text,
                IsInfoMessageVisible = true
            });
            if (btnProcessFilter.Content.ToString() == "Execute")
            {
                btnProcessFilter.Content = "Cancel";
                if (isCancel != null)
                {
                    isCancel.Cancel();
                    isCancel = null;
                }
            }
            else
            {
                isCancel = new CancellationTokenSource();

                btnProcessFilter.Content = "Execute";
                return;
            }

            try
            {
                if (this._Id.Equals(_DefaultSelectedTab))
                {
                    GetDataFromPsCommand(null);
                    return;
                }
                
                GetDataFromPsCommand(_viewModel.CreatePsRunSpace(ref _runspace, 
                    MainWindow.UserCredential.Password,
                    MainWindow.UserCredential.UserName,
                    MainWindow.UserCredential.DomainName,
                    this._Id));
            }
            catch (Exception ex)
            {
                _viewModel.DisplayStatusMessage($"Fail to connect to server [{this._Id.Trim()}]");
                _viewModel.DisplayErrorMessage(ex.Message);
                _viewModel.PublishException(ex);
                _runspace?.Close();
                _ps?.Dispose();
                ClearPopup();               
                return;
            }
            finally
            {
                this.Cursor = System.Windows.Input.Cursors.Arrow;
            }
        }
        
        private void ClearPopup()
        {
            //_viewModel.Clear();
            _viewModel.DisplayPopUpMessage(new MessageView()
            {
                InfoMessage = $"Complete.",
                InfoMessageTitle = comboCommandName.Text,
                IsInfoMessageVisible = false
            });
        }

        private void Error_DataAdded(object sender, DataAddedEventArgs e)
        {
            string msg = ((PSDataCollection<ErrorRecord>)sender)[e.Index].ToString();
            txtOutput.Dispatcher.BeginInvoke(new SetOutput(Execute), new object[] { msg });
        }
        private void Debug_DataAdded(object sender, DataAddedEventArgs e)
        {
            string msg = ((PSDataCollection<DebugRecord>)sender)[e.Index].ToString();
            txtOutput.Dispatcher.BeginInvoke(new SetOutput(Execute), new object[] { msg });
        }
        private void Process_TextDataAdded(object sender, DataAddedEventArgs e)
        {
            PSDataCollection<PSObject> myp = (PSDataCollection<PSObject>)sender;
            Collection<PSObject> results = myp.ReadAll();
            foreach (var message in results.Where(x => x != null))
            {
                List<string> msg = new List<string>();
                foreach (var rest in message.Members)
                {
                    if (FilteredAttributes(message.Members))
                    {
                        if (SupportedAttributes.Contains(rest.Name))
                        {
                            msg.Add(string.Concat(rest.Name, ": ", rest.Value));
                        }
                    }
                    else
                    {
                        if (!UnSupportedAttributes.Contains(rest.Name))
                        {
                            msg.Add(string.Concat(rest.Name, ": ", rest.Value));
                        }
                    }
                }
                txtOutput.Dispatcher.BeginInvoke(new SetOutput(Execute),
                        new object[] { String.Join("\r\n", msg) + "\r\n------------------\r\n" });
            }
        }
        private void Process_GridDataAdded(object sender, DataAddedEventArgs e)
        {
            if (isCancel != null)
            {
                _ps.Stop();
                _ps.Dispose();
                _runspace?.Close();
                return;
            }
            PSDataCollection<PSObject> myp = (PSDataCollection<PSObject>)sender;
            Collection<PSObject> results = myp.ReadAll();

            Dispatcher.Invoke(() =>
            {
                UpdateResultGrid(results);
            });
        }

        private void Verbose_DataAdded(object sender, DataAddedEventArgs e)
        {
            string msg = ((PSDataCollection<VerboseRecord>)sender)[e.Index].ToString();
            txtOutput.Dispatcher.BeginInvoke(new SetOutput(Execute), new object[] { msg });
        }
        #endregion

        private void RessizeResultTextBox(object sender, TextChangedEventArgs e)
        {
            double height = GetTabHeight((TextBox)sender);
            txtOutput.Height = height - 140;
        }

        private double GetTabHeight(FrameworkElement sender)
        {
            if (sender is TabControl)
                return ((FrameworkElement)sender).ActualHeight;
            else
                return GetTabHeight((FrameworkElement)sender.Parent);
        }

        private void SetGridInitialLayout(object sender, RoutedEventArgs e)
        {
            double height = GetTabHeight((FrameworkElement)sender);
            if(height > 130)
                ResultGrid.Height = height - 130;
        }

        private void ButtonCloseTab_Click(object sender, RoutedEventArgs e)
        {
            var tabItem = ((FrameworkElement)((FrameworkElement)((FrameworkElement)((FrameworkElement)sender).Parent).Parent).Parent).Parent;
            var name = ((TabItem)tabItem).Header.ToString();
            _viewModel.CloseTabByName(name);
        }
        
    }
}
