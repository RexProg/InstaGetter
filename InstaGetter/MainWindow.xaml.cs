#region using

using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Newtonsoft.Json.Linq;

#endregion

namespace InstaGetter
{
    /// <summary>
    ///     Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        private string _acc;
        private CookieContainer _cookie;
        private string _edge = "edge_followed_by";
        private bool _hasNextPage = true;
        private string _parameter = "{\"id\":\"XID\",\"first\":2500,\"after\":\"XTOK\"}";
        private string _savePath = Environment.CurrentDirectory + @"\InstaGetter_Usernames.txt";
        private bool _started;
        private int _swc = 1;
        private object _syncLock = new object();
        private string _token;
        private string _url = "https://www.instagram.com/graphql/query/?query_id=17851374694183129&variables=";
        private int _userCount;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            if (_started)
            {
                _started = false;
                btnStart.Content = "Start";
                return;
            }

            btnStart.Content = "Stop";
            if (File.Exists(_savePath))
                _savePath = GetUniqueFilePath(_savePath);
            _started = true;
            var c = int.Parse(txtCount.Text);
            var p = txtPage.Text;
            var a = txtAcc.Text;
            Dispatcher.BeginInvoke(
                (ThreadStart)
                delegate { new Thread(() => GetUserName(c, p, a)).Start(); });
            lblStatus.Content = "Status : Login...";
        }

        public void GetUserName(int count, string page, string user)
        {
            _hasNextPage = true;
            _userCount = count;
            _acc = user;
            _token = "";
            if (!Login())
                return;
            Getter(page);
        }

        private void TxtCount_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!int.TryParse(((TextBox) sender).Text, out var _))
                ((TextBox) sender).Text = ((TextBox) sender).Text.Remove(e.Changes.ToList()[0].Offset,
                    e.Changes.ToList()[0].AddedLength);
        }

        private void Getter(string user)
        {
            re:
            try
            {
                var httpWebRequest = (HttpWebRequest) WebRequest.Create("https://www.instagram.com/" + user + "/");
                httpWebRequest.CookieContainer = _cookie;
                httpWebRequest.Method = "GET";
                httpWebRequest.KeepAlive = true;
                httpWebRequest.ContentType = "application/x-www-form-urlencoded";
                httpWebRequest.UserAgent =
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/53.0.2785.143 Safari/537.36";
                var httpWebResponse = (HttpWebResponse) httpWebRequest.GetResponse();
                _cookie.Add(httpWebResponse.Cookies);
                var streamReader2 = new StreamReader(
                    httpWebResponse.GetResponseStream() ?? throw new InvalidOperationException(),
                    Encoding.GetEncoding(httpWebResponse.CharacterSet));
                var str = streamReader2.ReadToEnd();
                var id =
                    JObject.Parse(Regex.Match(str, "window._sharedData = (.*?);</script>").Groups[1].Value)["entry_data"
                    ]["ProfilePage"][0]["user"]["id"].ToString();
                httpWebResponse.Close();
                Dispatcher.BeginInvoke(DispatcherPriority.Normal,
                    (ThreadStart) delegate { lblStatus.Content = "Status : Leaching..."; });
                while (_userCount > 0 && _hasNextPage)
                {
                    var followRequest = (HttpWebRequest) WebRequest.Create(
                        _url + HttpUtility.UrlEncode(_parameter).Replace("XID", id).Replace("XTOK", _token));
                    followRequest.Headers.Add("X-Instagram-AJAX", "1");
                    followRequest.CookieContainer = _cookie;
                    followRequest.Headers.Add("X-Requested-With", "XMLHttpRequest");
                    followRequest.Method = "GET";
                    var followResponse = (HttpWebResponse) followRequest.GetResponse();
                    var streamReader = new StreamReader(followResponse.GetResponseStream());
                    var text = streamReader.ReadToEnd();
                    var edges = JObject.Parse(text)["data"]["user"][_edge]["edges"];
                    _token = JObject.Parse(text)["data"]["user"][_edge]["page_info"]["end_cursor"]?.ToString();
                    _hasNextPage = (bool) JObject.Parse(text)["data"]["user"][_edge]["page_info"]["has_next_page"];

                    Dispatcher.BeginInvoke(
                        (ThreadStart)
                        delegate { new Thread(() => SaveEdges(edges)).Start(); });

                    if (!_started)
                        break;
                }

                Dispatcher.BeginInvoke(DispatcherPriority.Normal,
                    (ThreadStart) delegate { lblStatus.Content = "Status : Finished"; });
                Dispatcher.BeginInvoke(DispatcherPriority.Normal,
                    (ThreadStart) delegate { btnStart.Content = "Start"; });
                _started = false;
            }
            catch (WebException e)
            {
                if (((HttpWebResponse) e.Response).StatusCode.ToString() == "429")
                {
                    Dispatcher.Invoke(DispatcherPriority.Normal,
                        (ThreadStart) delegate { lblStatus.Content = "Status : Wait for 4 min"; });
                    Thread.Sleep(60000);
                    Dispatcher.Invoke(DispatcherPriority.Normal,
                        (ThreadStart) delegate { lblStatus.Content = "Status : Wait for 3 min"; });
                    Thread.Sleep(60000);
                    Dispatcher.Invoke(DispatcherPriority.Normal,
                        (ThreadStart) delegate { lblStatus.Content = "Status : Wait for 2 min"; });
                    Thread.Sleep(60000);
                    Dispatcher.Invoke(DispatcherPriority.Normal,
                        (ThreadStart) delegate { lblStatus.Content = "Status : Wait for 1 min"; });
                    Thread.Sleep(60000);
                    goto re;
                }

                if (((HttpWebResponse) e.Response).StatusCode == HttpStatusCode.BadRequest)
                    Dispatcher.BeginInvoke(DispatcherPriority.Normal,
                        (ThreadStart) delegate { lblStatus.Content = "Status : Finished"; });
                if (((HttpWebResponse) e.Response).StatusCode == HttpStatusCode.NotFound)
                    Dispatcher.BeginInvoke(DispatcherPriority.Normal,
                        (ThreadStart) delegate { lblStatus.Content = "Status : Page Not Found"; });
            }
        }

        private void SaveEdges(JToken edges)
        {
            for (var i = 0; i < edges.Count() && _userCount > 0; i++)
                lock (_syncLock)
                {
                    var user = edges[i]["node"]["username"].ToString();
                    using (var sw = File.AppendText(_savePath))
                    {
                        sw.WriteLine(user);
                        sw.Close();
                    }

                    _userCount--;
                }
        }

        private string GetUniqueFilePath(string filepath)
        {
            if (File.Exists(filepath))
            {
                var folder = Path.GetDirectoryName(filepath);
                var filename = Path.GetFileNameWithoutExtension(filepath);
                var extension = Path.GetExtension(filepath);
                var number = 1;

                var regex = Regex.Match(filepath, @"(.+) \((\d+)\)\.\w+");

                if (regex.Success)
                {
                    filename = regex.Groups[1].Value;
                    number = int.Parse(regex.Groups[2].Value);
                }

                do
                {
                    number++;
                    if (folder != null)
                        filepath = Path.Combine(folder, string.Format("{0}_{1}{2}", filename, number, extension));
                } while (File.Exists(filepath));
            }

            return filepath;
        }

        private bool Login()
        {
            var cookietok = new CookieContainer();
            var tokWebRequest = (HttpWebRequest) WebRequest.Create("https://www.instagram.com/");
            tokWebRequest.CookieContainer = cookietok;
            tokWebRequest.Method = "GET";
            tokWebRequest.KeepAlive = true;
            tokWebRequest.ContentType = "application/x-www-form-urlencoded";
            tokWebRequest.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:49.0) Gecko/20100101 Firefox/49.0";
            var tokWebResponse = (HttpWebResponse) tokWebRequest.GetResponse();
            cookietok.Add(tokWebResponse.Cookies);
            var token = tokWebResponse.Cookies.OfType<Cookie>().FirstOrDefault(c => c.Name == "csrftoken")?.Value;
            tokWebResponse.Close();
            var bytes =
                new ASCIIEncoding().GetBytes("username=" + _acc.Split(':')[0] + "&password=" + _acc.Split(':')[1]);
            var loginRequest = (HttpWebRequest) WebRequest.Create("https://www.instagram.com/accounts/login/ajax/");
            loginRequest.CookieContainer = cookietok;
            loginRequest.Headers.Add("X-CSRFToken", token);
            loginRequest.Headers.Add("X-Instagram-AJAX", "1");
            loginRequest.Headers.Add("X-Requested-With", "XMLHttpRequest");
            loginRequest.Method = "POST";
            loginRequest.KeepAlive = true;
            loginRequest.ContentType = "application/x-www-form-urlencoded";
            loginRequest.Referer = "https://www.instagram.com/";
            loginRequest.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:58.0) Gecko/20100101 Firefox/58.0";
            loginRequest.ContentLength = bytes.Length;
            var requestStream = loginRequest.GetRequestStream();
            requestStream.Write(bytes, 0, bytes.Length);
            requestStream.Close();
            var loginResponse = (HttpWebResponse) loginRequest.GetResponse();
            cookietok.Add(loginResponse.Cookies);
            _cookie = cookietok;
            var streamReader =
                new StreamReader(loginResponse.GetResponseStream() ?? throw new InvalidOperationException());
            var text = streamReader.ReadToEnd();
            var flag = (bool) JObject.Parse(text)["authenticated"];
            if (!flag)
                Dispatcher.BeginInvoke(DispatcherPriority.Normal,
                    (ThreadStart) delegate { lblStatus.Content = "Status : Account is invalid"; });
            return flag;
        }

        private void MetroWindow_Closing(object sender, CancelEventArgs e)
        {
            Environment.Exit(0);
        }

        private void BtnAbout_Click(object sender, RoutedEventArgs e)
        {
            new About().Show();
        }

        private void BtnSWC_Click(object sender, RoutedEventArgs e)
        {
            if (_swc == 1)
            {
                btnSWC.Content = "Following";
                _swc = 2;
                _url = "https://www.instagram.com/graphql/query/?query_id=17874545323001329&variables=";
                _edge = "edge_follow";
            }
            else
            {
                btnSWC.Content = "Follower";
                _swc = 1;
                _url = "https://www.instagram.com/graphql/query/?query_id=17851374694183129&variables=";
                _edge = "edge_followed_by";
            }
        }
    }
}