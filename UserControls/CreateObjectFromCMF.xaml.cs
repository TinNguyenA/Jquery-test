using System.Windows;
using System.Windows.Controls;
using WPF.PSE.ViewModelLayer;

namespace WPF.PSE.Utility.UserControls
{
    public partial class CreateObjectFromCMF :  UserControl, IPSUserControl
    {
        private ServerListObjectViewModel _viewModel = null;
        private UserControl _tabUserControl = null;
        public CreateObjectFromCMF()
        {
            InitializeComponent();
            // Connect to instance of the view model created by the XAML
            _viewModel = (ServerListObjectViewModel)this.Resources["viewModel"];
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
            get { return this.Width; }
            set
            {
                this.Width = value;
            }
        }
        public double TabHeight
        {
            get { return ((TabPageControl)_tabUserControl).Height; }
            set
            {
                this.Height = value;
            }
        }
        public string PSEnvironment { get; set; }
        public string TxtEditor { get; private set; }




        private void btnOpenFile_Click1(object sender, RoutedEventArgs e)
        {

        }
        private void btnOpenFile_Click2(object sender, RoutedEventArgs e)
        {

        }

        private void btnGenerate_Click(object sender, RoutedEventArgs e)
        {

        }

        private void btnClear_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
