using System.IO;
using System.Windows;
using System.Windows.Controls;
using WPF.PSE.ViewModelLayer;
using System;
using System.Collections;
using System.Linq;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Diagnostics;
using System.Security.Principal;
using WPF.PSE.AppLayer.DataObject;
using WPF.PSE.Common;
using WPF.PSE.AppLayer.Models;
using Common.PSELibrary.Tool;
using System.Collections.Specialized;
using System.Threading;
using System.Text;

namespace WPF.PSE.Utility.UserControls
{
    public partial class PSSetActiveProject : UserControl, IPSUserControl
    {
        [DllImport("kernel32.dll")]
        static extern bool CreateSymbolicLink(
       string lpSymlinkFileName, string lpTargetFileName, SymbolicLink dwFlags);

        enum SymbolicLink
        {
            File = 0,
            Directory = 1
        }
        private SQLTrackingViewModel _viewModel = null;
        private UserControl _tabUserControl = null;
        private IDictionary _mCookies;
        private CODISUtilGenericData _utilsData = null;
        private string _Localrepo = Environment.GetEnvironmentVariable("localrepo");
        private string _MV = Environment.GetEnvironmentVariable("MV");

        public PSSetActiveProject(IDictionary cookie)
        {
            InitializeComponent();
            // Connect to instance of the view model created by the XAML
            _viewModel = (SQLTrackingViewModel)this.Resources["viewModel"];
            _viewModel.DisplayStatusMessage("Wecome to CODIS");
            _mCookies = cookie;
            btnReload_Click(null, null);
            DBCommonScript.SelectedIndex = -1;
        }

        private void btnReload_Click(object sender, RoutedEventArgs e)
        {
            btnInstalDB.IsEnabled = false;
            btCreateDirLink.IsEnabled = false;
            int lastSelectedIndex = 0;
            if (sender != null)
            {
                lastSelectedIndex = DBCommonScript.SelectedIndex;
                TextVSVersion.Items.Clear();
                DBCommonScript.Items.Clear();
            }
            _utilsData = LoadVSPathData();
            LoadVSPathLastUsedVesion();
            LoadFavoriteScript();
            LoadBranchVersionSupported();
            LoadCODISDbType();
            if (DBCommonScript.Items.Count - 1 >= lastSelectedIndex)
                DBCommonScript.SelectedIndex = lastSelectedIndex;
        }

        private void LoadCODISDbType()
        {
            TextDBType.Items.Clear();
            foreach (UtilityGenericData data in _utilsData.Items.Where(x => x.Key == "CODISDBType").OrderBy(y => y.Name))
            {
                TextDBType.Items.Add(data);
            }
            if (TextDBType.Items.Count == 1)
                TextDBType.SelectedIndex = 0;
        }

        private void LoadBranchVersionSupported()
        {
            txtBranchName.Items.Clear();
            foreach (UtilityGenericData data in _utilsData.Items.Where(x => x.Key == "CODISVersion").OrderBy(y => y.Name))
            {
                txtBranchName.Items.Add(data);
            }
            if (txtBranchName.Items.Count == 1)
                txtBranchName.SelectedIndex = 0;
        }
        private void LoadFavoriteScript()
        {
            foreach (UtilityGenericData data in _utilsData.Items.Where(x => x.Key == "favSQl").OrderBy(y =>y.Name))
            {
                DBCommonScript.Items.Add(data);
            }
        }

        private CODISUtilGenericData LoadVSPathData()
        {
            Stream stream = new DataContext().GetStream("CODISUtilGenericData.xml", true, false);
            var _utilsData = CommonFunctions.ConvertXMLToClassObject<CODISUtilGenericData>(stream);
            stream.Close();
            foreach (UtilityGenericData data in _utilsData.Items.Where(x => x.Key == "VSLocation").OrderBy(y => y.Name))
            {
                TextVSVersion.Items.Add(data);
            }
            return _utilsData;
        }

        private void LoadVSPathLastUsedVesion()
        {
            string path = Path.Combine(_MV, "UtilityData", "ActiveVersion.txt");
            if (File.Exists(path))
                TxtLastAccessEnvironment.Text = File.ReadAllText(path);
            else
            {
                _viewModel.DisplayStatusMessage("Welcome first time exploring AT CODIS Utility");
                TxtLastAccessEnvironment.Text = "Have not used this tool before.";
            }
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

        private void CreateDirLink(object sender, RoutedEventArgs e)
        {           
            this.Cursor = System.Windows.Input.Cursors.Wait;
            try
            {
                string symbolicLinkD = Path.Combine(_Localrepo, "Trunk", "Source", "Product", "Debug");
                string symbolicLinkR = Path.Combine(_Localrepo, "Trunk", "Source", "Product", "Release");
                string symbolicLinkD_Restore = Path.Combine(_Localrepo, "Trunk", "Source", "Product", "Debug_Bak");
                string symbolicLinkR_Restore = Path.Combine(_Localrepo, "Trunk", "Source", "Product", "Release_Bak");
                CloseAWB();
                Thread.Sleep(1);
                Thread.Sleep(StopServices("NGISSWinService"));
                Thread.Sleep(StopServices("CASWinService"));

                if (Directory.Exists(symbolicLinkD))
                {
                    DirectoryInfo di = new DirectoryInfo(symbolicLinkD);
                    if (!di.Attributes.HasFlag(FileAttributes.ReparsePoint))
                    {
                        if (Directory.Exists(symbolicLinkD_Restore))
                            Directory.Delete(symbolicLinkD_Restore, true);
                        di.MoveTo(symbolicLinkD_Restore);
                    }
                    else
                    {
                        Directory.Delete(symbolicLinkD, true);
                    }
                }
                if (Directory.Exists(symbolicLinkR))
                {
                    DirectoryInfo di = new DirectoryInfo(symbolicLinkR);
                    if (!di.Attributes.HasFlag(FileAttributes.ReparsePoint))
                    {
                        if (Directory.Exists(symbolicLinkR_Restore))
                            Directory.Delete(symbolicLinkR_Restore, true);
                        di.MoveTo(symbolicLinkR_Restore);
                    }
                    else
                    {
                        Directory.Delete(symbolicLinkR, true);                       
                    }
                    Thread.Sleep(2);
                }
                string workingDebugFolder = Path.Combine(_Localrepo, txtBranchName.Text, "Source", "Product", "Debug");
                string workingReleaseFolder = Path.Combine(_Localrepo, txtBranchName.Text, "Source", "Product", "Release");

                if (txtBranchName.Text == "Trunk")
                {
                    if (Directory.Exists(symbolicLinkD_Restore))
                    {
                        DirectoryInfo di = new DirectoryInfo(symbolicLinkD_Restore);
                        di.MoveTo(symbolicLinkD);
                    }
                    if (Directory.Exists(symbolicLinkR_Restore))
                    {
                        DirectoryInfo di = new DirectoryInfo(symbolicLinkR_Restore);
                        di.MoveTo(symbolicLinkR);
                    }
                    SetActiveEnvironment(txtBranchName.Text);
                    this.Cursor = System.Windows.Input.Cursors.Arrow;
                    _viewModel.DisplayStatusMessage($"{txtBranchName.Text} Is now ready.");
                    return;
                }

                CreateSymbolicLink(symbolicLinkD, workingDebugFolder, SymbolicLink.Directory);
                CreateSymbolicLink(symbolicLinkR, workingReleaseFolder, SymbolicLink.Directory);
                SetActiveEnvironment(txtBranchName.Text);
                _viewModel.DisplayStatusMessage($"{txtBranchName.Text} Is now ready.");
                
                // Set up the registry NOT Working
                //try
                //{
                //    WindowsIdentity identity = WindowsIdentity.GetCurrent();
                //    WindowsPrincipal principal = new WindowsPrincipal(identity);
                //    bool isAdmin = principal.IsInRole(WindowsBuiltInRole.Administrator);
                //    if (isAdmin)
                //    {
                //        Process.Start("regedit.exe");
                //        ProcessStartInfo info = new ProcessStartInfo("regedit.exe",  $"{Path.Combine(_MV, "UtilityData", "CODISTrunk_V12.reg")}");
                //        info.UseShellExecute = true;
                //        info.WorkingDirectory = $"{Path.Combine(_MV, "UtilityData")}";
                //        info.CreateNoWindow = true;
                //        info.Verb = "runas";
                //        Process.Start(info);
                //    }
                //    else
                //    {
                //        MessageBox.Show("You are nor running as Administrator", "Notice", MessageBoxButton.OK, MessageBoxImage.Warning);
                //        Process.Start($"{Path.Combine(_MV, "UtilityData", "CODISTrunk_V12.reg")}");
                //    }
                //}
                //catch
                //{
                //    _viewModel.DisplayStatusMessage($"Not able to open the reg");
                //    return;
                //}
            }
            catch(Exception ex)
            {
                MessageBox.Show(ex.Message + "\r\nPlease close File Explorer that may be opening CODIS debug folders.", "Setup Env Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            finally
            {
                this.Cursor = System.Windows.Input.Cursors.Arrow;
            }
        }

        private void CloseAWB()
        {
            try
            {
                var processes = Process.GetProcessesByName("CODIS.AnalystWorkbench");

                foreach (var process in processes)
                {
                    process.Kill();
                }
                Thread.Sleep(2);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + " .Please manually close AWB.", "Close AWB Error", MessageBoxButton.OK, MessageBoxImage.Error);                
            }
            finally
            {
               
            }
        }

        private void SetActiveEnvironment(string text)
        {
            using (var writer = File.CreateText(Path.Combine(_MV, "UtilityData", "ActiveVersion.txt")))
            {
                if (TextDBType.Text != "")
                    text = text + " - Database: " + TextDBType.Text;
                writer.WriteLine(text);
            }
            TxtLastAccessEnvironment.Text = text;
        }
        private bool StartServices(string serviceName)
        {
            try
            {
                var service = new ServiceController(serviceName);
                if (service == null)
                {
                    MessageBox.Show($"Unable to start '{serviceName}' service.\r\nNo Binary found.\r\nYou should compile the project now.");
                    return false;
                }
                switch (service.Status)
                {
                    case ServiceControllerStatus.Running:
                        break;
                    case ServiceControllerStatus.StartPending:
                    case ServiceControllerStatus.Paused:
                        try
                        {
                            service.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
                        }
                        catch
                        {
                            MessageBox.Show($"Unable to start '{serviceName}' service.");
                            return false;
                        }
                        break;
                    case ServiceControllerStatus.StopPending:
                        service.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(20));
                        service.Start();
                        break;
                    default:
                        service.Start();
                        service.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(20));
                        break;
                }
            }
            catch (Exception ex)    
            {
                MessageBox.Show(ex.Message, "Service start/stop Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            finally
            {
            }
            return true;
        }

        private int StopServices(string serviceName)
        {
            try
            {
                var service = new ServiceController(serviceName);
                if (service == null)
                {
                    return 0;
                }
                switch (service.Status)
                {
                    case ServiceControllerStatus.Stopped:
                        break;
                    case ServiceControllerStatus.StopPending:
                    case ServiceControllerStatus.Paused:
                        try
                        {
                            service.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(20));
                        }
                        catch
                        {
                            MessageBox.Show($"Unable to Stop '{serviceName}' service.");
                            return 0;
                        }
                        break;
                    case ServiceControllerStatus.Running:
                    case ServiceControllerStatus.StartPending:
                        service.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(20));
                        service.Stop();
                        service.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(20));
                        break;
                    default:
                        service.Stop();
                        service.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(20));
                        break;
                }
                return 2;
            }
            catch(Exception ex)
            {
                MessageBox.Show(ex.Message, "Service Stop Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return -1;
            }
            finally
            {
            }
        }

        private void EnablePathCheck(object sender, SelectionChangedEventArgs e)
        {
            if (TextProjectPathSelected == null)
                return;
            btnUpdateReg.IsEnabled = true;
            TextProjectPathSelected.IsEnabled = true;
            btCreateDirLink.IsEnabled = true;
            TextProjectPathSelected.Items.Clear();
            string thePath = Path.Combine(_Localrepo, ((UtilityGenericData)txtBranchName.SelectedItem).Name, "Source", "Product");
            TextProjectPathSelected.Items.Add(thePath);
            TextProjectPathSelected.SelectedIndex = 0;
            btnEnablePathCheck.IsEnabled = true;
            TextProjectPathSelected.ToolTip = "%Localrepo%/Trunk/Source/Product/CODIS.sln";
            if(TextVSVersion!=null && TextVSVersion.Items.Count>0)
            TextVSVersion.SelectedIndex = TextVSVersion.Items.Count-1;
        }

        private void EnableScriptChange(object sender, SelectionChangedEventArgs e)
        {
            btnInstalDB.IsEnabled = txtBranchName.SelectedIndex > -1 && TextCodisVersion.SelectedIndex > -1 && TextDBType.SelectedIndex > -1;
            if (TextVSVersion.SelectedItem != null)
            {
                CompileVS.IsEnabled = true;
                CompileCleanVS.IsEnabled = true;
                TextVSVersion.ToolTip = ((UtilityGenericData)TextVSVersion.SelectedItem).Value;
            }
        }

        private void PathUpdate(object sender, SelectionChangedEventArgs e)
        {
            if (txtBranchName.SelectedItem == null)
                return;
            var versions = ((UtilityGenericData)txtBranchName.SelectedItem).Value.Split(",".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
            if (versions.Length > 0)
                TextCodisVersion.Items.Clear();
            foreach (var version in versions)
                TextCodisVersion.Items.Add(version);
            if (TextCodisVersion.Items.Count == 1)
                TextCodisVersion.SelectedIndex = 0;
            if (TextProjectPathSelected != null && TextProjectPathSelected.IsEnabled)
            {
                string thePath = Path.Combine(_Localrepo, ((UtilityGenericData)txtBranchName.SelectedItem).Name, "Source", "Product");
                TextProjectPathSelected.Items.Clear();
                TextProjectPathSelected.Items.Add(thePath);
                TextProjectPathSelected.SelectedIndex = 0; btnEnablePathCheck.IsEnabled = true;
            }
        }

        private void btnOpenFolder_Click(object sender, RoutedEventArgs e)
        {
            string path = TextProjectPathSelected.SelectedItem.ToString();
            if (!Directory.Exists(path))
            {
                MessageBox.Show($"{ TextProjectPathSelected.SelectedItem} is not existing.\r\nPlease get that vesion from TFS.", "Notice", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            else
                Process.Start(path);
        }

        private void btnLaunchVS_Click(object sender, RoutedEventArgs e)
        {
            this.Cursor = System.Windows.Input.Cursors.Wait;
            if (TextVSVersion.SelectedIndex < 0 || TextProjectPathSelected.SelectedIndex < 0)
            {
                MessageBox.Show("Please select the right Version of VS and Project!", "Notice", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            string path = TextProjectPathSelected.SelectedItem.ToString();
            if (!Directory.Exists(path))
            {
                _viewModel.DisplayStatusMessage("Please get this version from TFS");
                return;
            }
            if (TextVSVersion.Items.Count == 0)
            {
                UtilityGenericData newItem = new UtilityGenericData();
                newItem.Key = "VSPath";
                newItem.Name = "Curent version";
                newItem.Value = "C:\\Program Files\\Microsoft Visual Studio\\2022\\Enterprise\\Common7\\IDE\\devenv.exe";
                TextVSVersion.Items.Add(newItem);
                TextVSVersion.SelectedIndex = 0;
            }

            string vsExePath = ((UtilityGenericData)TextVSVersion.SelectedValue).Value;
            if(!File.Exists(vsExePath))
            {
                MessageBox.Show($"{vsExePath}\r\n\r\nVisual Studio Path above is NOT valid. \r\nPlease edit Configuration file now.", "Notice", MessageBoxButton.OK, MessageBoxImage.Warning);
                btnModifyVSPath_Click(null, null);
                return;
            }

            try
            {
                WindowsIdentity identity = WindowsIdentity.GetCurrent();
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                bool isAdmin = principal.IsInRole(WindowsBuiltInRole.Administrator);
                if (isAdmin)
                {
                    ProcessStartInfo info = new ProcessStartInfo(vsExePath, Path.Combine(path, "Codis.sln"));
                    info.UseShellExecute = true;
                    info.Verb = "runas";
                    Process.Start(info);
                }
                else
                {
                    MessageBox.Show("You are nor running as Administrator", "Notice", MessageBoxButton.OK, MessageBoxImage.Warning);
                    Process.Start(path);
                }
            }
            catch
            {
                _viewModel.DisplayStatusMessage($"Not able to open Codis.sln in {vsExePath}");
                return;
            }
            finally
            {
                this.Cursor = System.Windows.Input.Cursors.Arrow;
            }
        }
        private void DisplaySQLText_Click(object sender, SelectionChangedEventArgs e)
        {
            if (DBCommonScript.SelectedIndex < 0)
            {
                if (txtSQLScript != null)
                    txtSQLScript.Text = "";
                return;
            }
            txtSQLScript.Text = ((UtilityGenericData)DBCommonScript.SelectedItem)?.Value;
            btnCopySQL.IsEnabled = true;
        }
        private void btnCopySQL_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetDataObject(txtSQLScript.Text);
        }

        private void btnModifyVSPath_Click(object sender, RoutedEventArgs e)
        {
            var fileName = Path.Combine(_MV, "UtilityData", "CODISUtilGenericData.xml");
            if (File.Exists(fileName))
            {
                Process.Start("Notepad++", fileName);
            }
        }
        private void btnLaunchSQL_Click(object sender, RoutedEventArgs e)
        {
            this.Cursor = System.Windows.Input.Cursors.Wait;

            try
            {
                var errorIfAny = Utils.RunSQL(txtSQLScript.Text, true);
                if (errorIfAny.Length > 0)
                {
                    MessageBox.Show(errorIfAny, "Exec SQL Status", MessageBoxButton.OK, MessageBoxImage.Information);
                    _viewModel.DisplayStatusMessage($"Exec SQL Status: ERROR.");
                }
                else
                {
                    //MessageBox.Show("Completed", "Exec SQL Status", MessageBoxButton.OK, MessageBoxImage.Information);
                    _viewModel.DisplayStatusMessage($"Exec SQL Status: SUCCESS.");
                }
            }
            catch (Exception ex)
            {
                _viewModel.DisplayStatusMessage($" Err {ex.Message}");
                _viewModel.PublishException(ex);
            }

            finally
            {
                this.Cursor = System.Windows.Input.Cursors.Arrow;
            }
        }

        private void btnbtnInstalDB_Click(object sender, RoutedEventArgs e)
        {
            if(TextDBType.SelectedIndex<0 || TextCodisVersion.SelectedIndex < 0)
            {
                MessageBox.Show("Please select a database type/version", "OOPs", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            this.Cursor = System.Windows.Input.Cursors.Wait;
            if (TextCodisVersion.SelectedIndex<0 || TextDBType.SelectedIndex<0)
            {
                MessageBox.Show("Please select the Supporting Database Version", "Install DB Status", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            CloseAWB();            
            StopServices("NGISSWinService");
            StopServices("CASWinService");            
            try
            {
                NameValueCollection param = new NameValueCollection();
                param.Add("BranchName", GetBranchName());
                param.Add("DatabaseName", "CODIS");
                param.Add("Lab", ((UtilityGenericData)TextDBType.SelectedItem)?.Value);
                param.Add("ServerName", "localhost");
                param.Add("CODISVersion_Major", TextCodisVersion.Text);

                var errorIfAny = Utils.RunPSFile(Path.Combine(_Localrepo,
                                                              "trunk",
                                                              "Scripts\\Builds\\TeamCityBuilds",
                                                              "RestoreLatestDBToLocalSQLInstance_TeamCity.ps1"), param);
                if (errorIfAny.Length > 0)
                {
                    MessageBox.Show(errorIfAny, "Install DB Status", MessageBoxButton.OK, MessageBoxImage.Warning);
                    _viewModel.DisplayStatusMessage($"Install DB Status: Failed.");
                }
                else
                {
                    _viewModel.DisplayStatusMessage($"Install DB Status: SUCCESS.");
                }
            }
            catch (Exception ex)
            {
                _viewModel.DisplayStatusMessage($" Err {ex.Message}");
                _viewModel.PublishException(ex);
                this.Cursor = System.Windows.Input.Cursors.Arrow;
                return;
            }
            // DefaultATDBSetting
            StringBuilder sb = new StringBuilder();
            foreach (UtilityGenericData data in _utilsData.Items.Where(x => x.Key == "DefaultATDBSetting"))
            {
                sb.Append(data.Value).Replace("???", TextCodisVersion.Text);
            }
            if(sb.Length > 0)
            {
                Utils.RunSQL(sb.ToString(), false);
            }
            this.Cursor = System.Windows.Input.Cursors.Arrow;
        }

        private string GetBranchName()
        {
            if (txtBranchName.Text != "Maple" &&
                txtBranchName.Text != "Larch" &&
                txtBranchName.Text != "Aspen")
                return "AT";
            return txtBranchName.Text;
        }

        private void btnUpdateReg_Click(object sender, RoutedEventArgs e)
        {
           UtilityGenericData data = _utilsData.Items.FirstOrDefault(x => x.Key == "PSScriptUpdateCODISVersion");
            var temp = data.Value.Replace("???", TextCodisVersion.Text);
            //MessageBox.Show("Please paste the script below in PowerShell / Runas Admin. \r\nSet-ItemProperty -Path 'HKLM:\\SOFTWARE\\CODIS' -Name 'Version' -Value $NewVersion", "Script Run In Powershell", MessageBoxButton.OK, MessageBoxImage.Warning);
            Clipboard.SetDataObject(temp);

            string fileToRunManually = Path.Combine(_MV, "UtilityData", "MakeCODISVersion" + TextCodisVersion.Text + ".reg");
            MessageBox.Show("Please execute the reg file in the location:"+ fileToRunManually, "Script Run manually", MessageBoxButton.OK, MessageBoxImage.Information);
            
            if (!File.Exists(fileToRunManually))
            {
                using (var writer = File.CreateText(fileToRunManually))
                {
                    data = _utilsData.Items.FirstOrDefault(x => x.Key == "UPDVersionInRegistry");
                    var text = data.Value.Replace("???", TextCodisVersion.Text);
                    writer.WriteLine(text);
                }
            }
                FileInfo fileInfo = new FileInfo(fileToRunManually);
            if (!Directory.Exists(fileInfo.Directory.FullName))
            {
                _viewModel.DisplayStatusMessage($"{fileToRunManually} is not existing.");
            }
            else
                Process.Start(fileInfo.Directory.FullName); 
            
        }

        private void btnbtnRestoreDefault_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("This will delete you current configuration and revert all setting back to default", "Confirm Restore All Setting",
                MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.No)
                return;
            // copy default over and reload
            new DataContext().GetStream("CODISUtilGenericData.xml", true, true);
            TextVSVersion.Items.Clear();
            btnReload_Click(null, null);
            DBCommonScript.SelectedIndex = -1;
        }

        private void btnLaunchTc_Click(object sender, RoutedEventArgs e)
        {
            this.Cursor = System.Windows.Input.Cursors.Arrow;
            try
            {
                WindowsIdentity identity = WindowsIdentity.GetCurrent();
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                bool isAdmin = principal.IsInRole(WindowsBuiltInRole.Administrator);
                if (isAdmin)
                {
                    ProcessStartInfo info = new ProcessStartInfo("TestComplete.exe");// vsExePath, Path.Combine(path, "Codis.sln"));
                    info.UseShellExecute = true;
                    info.Verb = "runas";
                    Process.Start(info);
                }
                else
                {
                    MessageBox.Show("You are nor running as Administrator", "Notice", MessageBoxButton.OK, MessageBoxImage.Warning);
                    Process.Start("TestComplete.exe");
                }
                if (StartServices("CASWinService"))
                {
                    StartServices("NGISSWinService");
                }
            }
            catch
            {
                _viewModel.DisplayStatusMessage($"Not able to open TestComplete.exe");
                MessageBox.Show("TestComplete is not installed", "Notice", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                this.Cursor = System.Windows.Input.Cursors.Arrow;
            }           
        }

        private void btnLaunchCd_Click(object sender, RoutedEventArgs e)
        {
            this.Cursor = System.Windows.Input.Cursors.AppStarting;
            if (StartServices("CASWinService"))
            {
                StartServices("NGISSWinService");
                try
                {
                    string symbolicLinkD = Path.Combine(_Localrepo, "Trunk", "Source", "Product", "Debug", "CODIS.AnalystWorkbench.exe");
                    if (!File.Exists(symbolicLinkD))
                    {
                        throw new FileNotFoundException($"File {symbolicLinkD} not found for execution");
                    }
                    Process.Start(symbolicLinkD);
                }
                catch
                {
                    _viewModel.DisplayStatusMessage($"Not able to open TestComplete.exe");
                    MessageBox.Show("CODIS is not compiled", "Notice", MessageBoxButton.OK, MessageBoxImage.Error);
                    this.Cursor = System.Windows.Input.Cursors.Arrow;
                    return;
                }
                finally
                {
                    this.Cursor = System.Windows.Input.Cursors.Arrow;
                }
            }           
        }

        private void btnLaunchSQLServer_Click(object sender, RoutedEventArgs e)
        {
            this.Cursor = System.Windows.Input.Cursors.AppStarting;
            try
            {
                UtilityGenericData data = _utilsData.Items.FirstOrDefault(x => x.Key == "SQLServerLocation");
                var fileToExecute = data.Value;
                ProcessStartInfo startInfo = new ProcessStartInfo(data.Value);
                Process processToExecute = new Process();

                startInfo.UseShellExecute = true;
                processToExecute.StartInfo = startInfo;
                if (!File.Exists(fileToExecute))
                {
                    throw new FileNotFoundException($"File {fileToExecute} not found for execution");
                }

                processToExecute.Start();

            }
            catch
            {
                _viewModel.DisplayStatusMessage($"Not able to open sql server (Ssms.exe)");
                MessageBox.Show("Not able to open sql server (Ssms.exe).Please update your Config file", "Notice", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                this.Cursor = System.Windows.Input.Cursors.Arrow;
            }
        }

        private void btnCompileVS_Click(object sender, RoutedEventArgs e)
        {           
            BuildProjectWithTheLatestVersion(false);
        }
        private void btnCompileCleanVS_Click(object sender, RoutedEventArgs e)
        {
            BuildProjectWithTheLatestVersion(true);
        }
        private void BuildProjectWithTheLatestVersion(bool isCleanToo)
        {
            if(TextProjectPathSelected.ToolTip.ToString()=="" || txtBranchName.SelectedIndex<0)
            {
                CompileCleanVS.IsEnabled = false;
                CompileVS.IsEnabled = false;
                MessageBox.Show("Please select a project", "OOPs", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            UtilityGenericData data = _utilsData.Items.FirstOrDefault(x => x.Key == "PSScriptGetLatestCompile");
            string buildOption = isCleanToo ? "Clean,Build" : "Build";
            var temp = data.Value.Replace("OptBuild???", buildOption).Replace("OptSolNamePath???", 
                TextProjectPathSelected.ToolTip.ToString().Replace("%Localrepo%", _Localrepo));
            
            string fileToRunManually = Path.Combine(_MV, "UtilityData", "buildTheLatestCode.ps1");
            using (var writer = File.CreateText(fileToRunManually))
            {
                writer.WriteLine(temp);
            }
            var resultMes =MessageBox.Show("Please Make sure only Support CODIS 12/13. Otherwise, Cancel NOW!","Warning", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
            if(resultMes != MessageBoxResult.OK) 
            { return; }

            //get latest from tfs
            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);
            bool isAdmin = principal.IsInRole(WindowsBuiltInRole.Administrator);
            try {
                if (isAdmin)
                {
                    Process process = new Process();
                    ProcessStartInfo startInfo = new ProcessStartInfo();
                    startInfo.WindowStyle = ProcessWindowStyle.Maximized;
                    startInfo.FileName = "C:\\WINDOWS\\system32\\WindowsPowerShell\\v1.0\\powershell_ise.exe";

                    startInfo.Arguments = fileToRunManually;

                    //+ "\r\n" + getlatest + "\r\n";
                    var view = startInfo.Arguments;
                    startInfo.UseShellExecute = true;
                    startInfo.Verb = "runas";
                    process.StartInfo = startInfo;
                    Process.Start(startInfo);
                }
                else
                {
                    MessageBox.Show("You are not running as Administrator", "Notice", MessageBoxButton.OK, MessageBoxImage.Warning);

                }
            }
            catch(Exception ex)
            {
                var msg = ex.Message;
            }
            
            
            //string buildCmd = "\"C:\\Program Files\\Microsoft Visual Studio\\2022\\Enterprise\\MSBuild\\Current\\Bin\\MSBuild.exe\"";
            //string cmd1 = "\\Source\\Product\\CODIS.sln\" /t:Clean,Build /p:Configuration=Debug /p:Platform=x64";
            
        }


    }
}