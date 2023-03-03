namespace WPF.PSE.Utility.UserControls
{
    public interface IPSUserControl
    {
        double MWidth
        {
            get;
            set;
        }
        double MHeight
        {
            get;
            set;
        }
        double TabWidth
        {
            get;
            set;
        }
        double TabHeight
        {
            get;
            set;
        }
        string PSEnvironment
        {
            get;
            set;
        }
    }
}