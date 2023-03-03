using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using WPF.PSE.ViewModelLayer;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using WPF.PSE.AppLayer.DataObject;
using System.Collections;

namespace WPF.PSE.Utility.UserControls
{
    public partial class ServerListObject : UserControl, IPSUserControl
    {
        public string TxtEditor { get; private set; }
        private IDictionary _mCookies;

        public bool EnableConnect =>  MainWindow.UserCredential!=null;
        public ServerListObject(IDictionary cookie)
        {
            InitializeComponent();
            _mCookies = cookie;

            _viewModel = (ServerListObjectViewModel)this.Resources["viewModel"];
            AddTabControl_OnIni();
            comboBoxServer.OnApplyTemplate();
            LoadServerData();// Connect to instance of the view model created by the XAML

        }
        public double MWidth
        {
            get { return this.Width; }
            set { this.Width = value; }
        }
        public double MHeight
        {
            get { return this.Height; }
            set { this.Height = value; }
        }
        public double TabWidth
        {
            get { return ((TabPageControl)_tabUserControl).Width; }
            set
            {
                if (value > 10)
                    ((TabPageControl)_tabUserControl).Width = value;
            }
        }
        public double TabHeight
        {
            get { return ((TabPageControl)_tabUserControl).Height; }
            set
            {
                if (value > 10)
                {
                    ((TabPageControl)_tabUserControl).Height = value;
                    //int i= ((TabPageControl)_tabUserControl).GetPSTabControl.SelectedIndex;
                    var i = ((TabPageControl)_tabUserControl).GetPSTabControl;
                }
            }
        }

        public string PSEnvironment
        {
            get { return lbServerSelect.Content.ToString(); }
            set
            {
                lbServerSelect.Content = value;
            }
        }

        private void LoadServerData()
        {
            comboBoxServer.ItemsSource = _viewModel.LoadServerFromXML();
            comboBoxServer.SelectedIndex = 0;
        }

        private ServerListObjectViewModel _viewModel = null;
        private UserControl _tabUserControl = null;
        private void CloseButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            _viewModel.Close();
        }
        private void AddTabControl_OnIni()
        {
            var controlName = "WPF.PSE.Utility.UserControls.TabPageControl";
            Type ucType = null;

            ucType = Type.GetType(controlName);
            if (ucType == null)
            {
                MessageBox.Show("The Control: " + controlName
                                                + " does not exist.");
            }
            else
            {
                _tabUserControl = (UserControl)Activator.CreateInstance(ucType);
                if (_tabUserControl != null)
                {
                    // Display control in content area
                    contentArea.Children.Add(_tabUserControl);
                }
            }
        }
        private void AddTabControl_OnClick(object sender, RoutedEventArgs e)
        {
            if (!EnableConnect)
            {
                _viewModel.DisplayStatusMessage("Please Use your Pa account to connect.");
                _viewModel.CreateInfoMessageTimer("When Using Powershell module, please use your Pa Account to connect to external compupters", "Login Request", 4000);
                return;
            }
            if (((TabPageControl)_tabUserControl).DoesTabExist(comboBoxServer.Text))
            {
                ((TabPageControl)_tabUserControl).SetSelectedTab(comboBoxServer.Text);
                return;
            }
            IComputerProperty selectedServer = comboBoxServer.SelectedItem as IComputerProperty;
            if (selectedServer == null)
            {
                string origVal = null;
                if (selectedServer == null)
                {
                    origVal = comboBoxServer.Text;
                    comboBoxServer.SelectedIndex = 0;
                    selectedServer = comboBoxServer.SelectedItem as IComputerProperty;
                }
                else
                {
                    origVal = comboBoxServer.Text;
                }
                ((TabPageControl)_tabUserControl).AddNewPage(selectedServer, origVal);
            }
            else
            {
                if (comboBoxServer.Text.Trim().Equals(selectedServer.Name.Trim(),
                    StringComparison.InvariantCultureIgnoreCase))
                {
                    ((TabPageControl)_tabUserControl).AddNewPage(selectedServer);
                }
                else
                {
                    ((TabPageControl)_tabUserControl).AddNewPage(selectedServer, comboBoxServer.Text);
                }
            }
        }

        private void Image_Button_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            if (openFileDialog.ShowDialog() == true)
            {
                TxtEditor = File.ReadAllText(openFileDialog.FileName);
                lbServerSelect.Content = openFileDialog.SafeFileName;
            }
            comboBoxServer.ItemsSource = _viewModel.LoadServerFromXML(TxtEditor);
            comboBoxServer.SelectedIndex = 0;           
        }

        //public void SetCredential(string secret)
        //{
        //    _viewModel.UserEntity = new User() { UserName = Environment.UserName, Password = secret, IsLoggedIn = true, DomainName = Environment.UserDomainName };
        //}
    }

}
