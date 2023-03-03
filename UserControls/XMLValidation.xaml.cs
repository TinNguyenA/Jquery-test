using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using WPF.PSE.ViewModelLayer;
using UserControl = System.Windows.Controls.UserControl;
using System;
using System.Windows.Media;
using System.Xml.Schema;
using System.Collections.Generic;
using System.Xml.Linq;
using System.Xml;
using WPF.PSE.Common;
using System.Collections;
using System.Xml.XPath;
using System.Text;

namespace WPF.PSE.Utility.UserControls
{
    public partial class XMLValidation : UserControl, IPSUserControl
    {
        private ServerListObjectViewModel _viewModel = null;
        private UserControl _tabUserControl = null;
        private IDictionary _mCookies;
        public XMLValidation(IDictionary cookie)
        {
            InitializeComponent();
            // Connect to instance of the view model created by the XAML
            _viewModel = (ServerListObjectViewModel)this.Resources["viewModel"];
            _viewModel.DisplayStatusMessage("");
            _mCookies = cookie;
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
        private string XSDText { get; set; }
        private string XMLText { get; set; }
        private string XSDFile { get; set; }
        private string XMLFile { get; set; }
        public List<Error> Validated
        {
            get
            {
                return ValidateSchema(XSDText,
                                      GetTargetNsp(XMLText),
                                      XMLText);
            }
        }

        private string GetTargetNsp(string xMLText)
        {
            var xmlDoc = new XmlDocument();

            if (xMLText != string.Empty)
            {
                try
                {
                    xmlDoc.LoadXml(xMLText);
                    XPathNavigator foo = xmlDoc.CreateNavigator();
                    foo.MoveToFollowing(XPathNodeType.Element);
                    IDictionary<string, string> test = foo.GetNamespacesInScope(XmlNamespaceScope.All);
                    if (test.ContainsKey(""))
                    {
                        return test[""].ToString();
                    }
                }
                catch (XmlException e)
                {
                    TxtValidateResult.Text = e.Message;
                }
            }
            return "";
        }

        private void btnOpenFile_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "xml files (*.xml)|*.xml";
            openFileDialog.RestoreDirectory = true;
            if (openFileDialog.ShowDialog() == true)
            {
                txtEditorXML.Navigate(openFileDialog.FileName);
                XMLText = File.ReadAllText(openFileDialog.FileName);
                XMLFile = openFileDialog.FileName;
                BrowsersDisplay.Visibility = Visibility.Visible;
                btnXMLCodeGen.IsEnabled = true;
                btnReset.IsEnabled = true;
                btnValidation.IsEnabled = true;
                ValidateTxt.Height = new GridLength(0);
            }
        }

        private void btnOpenFile_ClickXsd(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "xml files (*.xsd)|*.xsd";
            //openFileDialog.InitialDirectory = "%LocalRepo%\\Trunk\\Source\\Product\\Production\\";
            if (openFileDialog.ShowDialog() == true)
            {
                XSDText = File.ReadAllText(openFileDialog.FileName);
                XSDFile = openFileDialog.FileName;
                txtEditorXSD.Navigate(openFileDialog.FileName);
                BrowsersDisplay.Visibility = Visibility.Visible;
                btnXMLCodeGen.IsEnabled = true;
                btnReset.IsEnabled = true;
                btnValidation.IsEnabled = !string.IsNullOrEmpty(XMLText);
                ValidateTxt.Height = new GridLength(0);
            }
        }

        private void btnXMLCodeGen_Click(object sender, RoutedEventArgs e)
        {
            ValidateTxt.Height = new GridLength(0);
            lbValidateResult.Text = "Code Generated";
            string XSDTextnew = CommonFunctions.CreateCodeFromXML(XMLFile, XSDFile);
            if (XSDTextnew != "" && XSDTextnew.ToLower().EndsWith("xsd"))
            {
                txtEditorXSD.Navigate(XSDTextnew);
                _viewModel.DisplayStatusMessage("Default Shema of the XML was used to generate code.");
            }
            else
            {
                if (string.IsNullOrEmpty(XMLText))
                {
                    string strFile = Path.GetFileNameWithoutExtension(XSDTextnew) + ".xml";
                    strFile = Path.Combine(Path.GetDirectoryName(XSDTextnew), strFile);
                    if (File.Exists(strFile))
                        txtEditorXML.Navigate(strFile);
                }
                _viewModel.DisplayStatusMessage("Code generated base on the (XSD) schema");
            }
        }
        private void btnReset_Click(object sender, RoutedEventArgs e)
        {
            XMLFile = XSDFile = "";
            XMLText = XSDText = "";
            btnValidation.IsEnabled = false;
            btnXMLCodeGen.IsEnabled = false;
            btnReset.IsEnabled = false;
            BrowsersDisplay.Visibility = Visibility.Hidden;
            txtEditorXML.Navigate("about:Blank");
            txtEditorXSD.Navigate("about:Blank");
            ValidateTxt.Height = new GridLength(0);
        }
        private void btnValidation_Click(object sender, RoutedEventArgs e)
        {
            ValidateLb.Height = new GridLength(30);
            ValidateTxt.Height = new GridLength(70);
            lbValidateResult.Text = " Validation result";
            TxtValidateResult.Text = "";
            TxtValidateResult.Visibility = Visibility.Visible;
            lbValidateResult.Visibility = Visibility.Visible;

            foreach (Error str in Validated)
            {
                TxtValidateResult.Text += str.Message + "\r\n";
                ValidateTxt.Height = new GridLength(70);
                TxtValidateResult.Foreground = new SolidColorBrush(Colors.Red);
            }

            if (TxtValidateResult.Text.Length == 0)
            {
                ValidateTxt.Height = new GridLength(50);
                TxtValidateResult.Text = "The XML is VALID";
                TxtValidateResult.Foreground = new SolidColorBrush(Colors.Green);
            }
        }

        private void IsValidate_Changed(object sender, TextChangedEventArgs e)
        {
            if (Validated.Count == 0)
            {
                btnXMLCodeGen.IsEnabled = true;
            }
            else
            {
                btnXMLCodeGen.IsEnabled = false;
            }
        }
        private List<Error> _errorMessages;
        private List<Error> ValidateSchema(string xsdSchema, string targetNameSpace, string xmlToValidate)
        {
            var xsdSchemaSet = new XmlSchemaSet();

            _errorMessages = new List<Error>();

            if (string.IsNullOrEmpty(xsdSchema))
            {
                return _errorMessages;
            }
            try
            {
                xsdSchemaSet.Add(targetNameSpace, XmlReader.Create(new StringReader(xsdSchema)));
            }
            catch (Exception e)
            {
                _errorMessages.Add(new Error { Type = ErrorTypes.XsdSchema, Message = e.Message });
            }

            if (_errorMessages.Count != 0)
                return _errorMessages;

            try
            {
                var xDocument = XDocument.Parse(xmlToValidate);
                xDocument.Validate(xsdSchemaSet,
                    (o, e) => { _errorMessages.Add(new Error { Type = ErrorTypes.Xml, Message = e.Message }); });
            }
            catch (Exception e)
            {
                _errorMessages.Add(new Error { Type = ErrorTypes.Xml, Message = e.Message });
            }
            return _errorMessages;
        }

        private void OpenToTextEditor(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (((TextBlock)(e.Source)).Text == " XSD")
            {
                if (txtEditorXSD.Source == null)
                {
                    return;
                }
                else
                    CommonFunctions.OpenFileEditor(txtEditorXSD.Source.LocalPath);
            }
            else
            {
                if (txtEditorXML.Source == null)
                {
                    return;
                }
                else
                    CommonFunctions.OpenFileEditor(txtEditorXML.Source.LocalPath);
            }
        }

        private void CreateFile(string fileName, string xSDText)
        {
            if (File.Exists(fileName))
                return;
            using (FileStream fs = File.Create(fileName))
            {
                // Add some text to file    
                Byte[] title = new UTF8Encoding(true).GetBytes(xSDText);
                fs.Write(title, 0, title.Length);
            }
        }
    }
    public enum ErrorTypes
    {
        Xml,
        XsdSchema
    }
    public class Error
    {
        public ErrorTypes Type { get; set; }
        public string Message { get; set; }
    }
}
