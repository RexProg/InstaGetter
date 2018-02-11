#region using

using System.Diagnostics;
using System.Windows.Input;

#endregion

namespace InstaGetter
{
    /// <summary>
    ///     Interaction logic for About.xaml
    /// </summary>
    public partial class About
    {
        public About()
        {
            InitializeComponent();
        }

        private void image_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Process.Start("https://t.me/CyberSoldiersST");
        }
    }
}