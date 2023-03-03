using Common.PSELibrary.CustomObjects;
using System.Collections;
using System.Windows;
using System.Windows.Controls;
using WPF.PSE.ViewModelLayer;

namespace WPF.PSE.Utility.UserControls
{
    public partial class LoginControl : UserControl, IPSUserControl
    {
        private IDictionary _mCookies;
        public LoginControl(IDictionary cookie)
        {
            InitializeComponent();
            _mCookies = cookie;
            // Connect to instance of the view model created by the XAML
            _viewModel = (LoginViewModel)this.Resources["viewModel"];
        }

        // Login view model class
        private LoginViewModel _viewModel = null;

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
            get;
            set;
        }
        public double TabHeight
        {
            get;
            set;
        }
        public string PSEnvironment { get; set; }
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.Close();
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {           
            _viewModel.UserEntity.DomainName = txtDomain.Text;
            _viewModel.UserEntity.UserName = txtUserName.Text;
            _viewModel.UserEntity.Password = txtPassword.Password;
            if(_viewModel.LoginValidate)
            {                
                _viewModel.LoginAsAdmin(_viewModel.UserEntity);   
            }
            else
            {
                _viewModel.DisplayStatusMessage("Username Or Password is incorrect. \nTry Again.");
            }
        }

    }
}
