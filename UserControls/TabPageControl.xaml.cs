using System;
using System.Windows;
using System.Windows.Controls;
using WPF.PSE.AppLayer.DataObject;
using WPF.PSE.ViewModelLayer;

namespace WPF.PSE.Utility.UserControls
{
    public partial class TabPageControl : UserControl
    {
        public TabControl GetPSTabControl
        {
            get { return TabPlaceHolder; }

        }
        public TabPageControl()
        {
            InitializeComponent();

            // Connect to instance of the view model created by the XAML
            _viewModel = (TabControlViewModel)this.Resources["viewModel"];
            //addeventOnload here
            AddNewPage(null);
        }

        // Login view model class
        private TabControlViewModel _viewModel = null;

        public void AddNewPage(IComputerProperty selectedItem, string optName = null)
        {
            TabPagesTemplate tabTemplate;
            if (selectedItem == null && optName == null)
            {
                tabTemplate = new TabPagesTemplate();
            }
            else
            {
                tabTemplate = new TabPagesTemplate(optName ?? selectedItem.Name, selectedItem.IpAddress);
            }
            tabTemplate.LoadData();
            TabPlaceHolder.Items.Add(tabTemplate.GetNewTab);
            tabTemplate.Focus();
            TabPlaceHolder.SelectedIndex = TabPlaceHolder.Items.Count - 1;
            //PSEnvironment = selectedItem.Environment + " is connected.";
        }

        //public void AddNewPageInTestEnv(TestEnvInfo selectedItem)
        //{
        //    TabPagesTemplate tabTemplate;
        //    if (selectedItem == null)
        //    {
        //        tabTemplate = new TabPagesTemplate();
        //    }
        //    else
        //    {
        //        tabTemplate = new TabPagesTemplate(selectedItem.serverName, selectedItem.tags);
        //        _viewModel.SetTitle(selectedItem.tags + " is connected.");
        //    }
        //    tabTemplate.LoadData();
        //    TabPlaceHolder.Items.Add(tabTemplate.GetNewTab);
        //    tabTemplate.Focus();
        //    TabPlaceHolder.SelectedIndex = TabPlaceHolder.Items.Count - 1;
        //}

        public bool DoesTabExist(string value)
        {
            foreach (TabItem item in TabPlaceHolder.Items)
            {
                if (item.Header.ToString().Trim().Equals(value.Trim(), StringComparison.InvariantCultureIgnoreCase))
                    return true;
            }
            return false;
        }

        public void SetSelectedTab(string name)
        {
            foreach (TabItem item in TabPlaceHolder.Items)
            {
                if (item.Header.ToString().Trim() == name.Trim())
                {
                    item.IsSelected = true;
                    break;
                }
            }
        }

        private void Image_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            int currentIndex = 0;
            foreach (TabItem item in TabPlaceHolder.Items)
            {
                if (item.Header.ToString().Trim() == sender.ToString())
                {
                    break;
                }
                currentIndex++;
            }
            TabPlaceHolder.Items.RemoveAt(currentIndex);
        }

        private void ResizeChildrenControl(object sender, SizeChangedEventArgs e)
        {
            foreach (TabItem item in TabPlaceHolder.Items)
            {
                ResizeMainGridControl(item);
                ResizeMainResultTextControl(item);
            }

        }

        private void ResizeMainResultTextControl(TabItem item)
        {
            if (item.Content is Panel)
            {
                foreach (var element in ((Panel)item.Content).Children)
                {
                    if (element is Panel)
                    {
                        foreach (var child in ((Panel)element).Children)
                        {
                            if (child is Grid)
                                foreach (var Grdchild in ((Grid)child).Children)
                                {
                                    if (Grdchild is TextBox)
                                        if (((TextBox)Grdchild).Name == "txtOutput")
                                            ((TextBox)Grdchild).Height = ((System.Windows.FrameworkElement)item.Parent).ActualHeight - 140;
                                }
                        }
                    }
                }
            }
            
        }

        private void ResizeMainGridControl(TabItem item)
        {
            // new System.Linq.SystemCore_EnumerableDebugView(((System.Windows.Controls.Panel)item.Content).Children).Items[1]
            if (item.Content is Panel)
            {
                foreach (var element in ((Panel)item.Content).Children)
                {
                    if (element is DevExpress.Xpf.Grid.GridControl)
                    {
                        ((DevExpress.Xpf.Grid.GridControl)element).Height = ((Panel)item.Content).ActualHeight - 50;
                    }
                }
            }
        }
    }
}
