#region using

using System;
using System.ComponentModel;
using System.Windows.Media;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Newtonsoft.Json.Linq;
using RestSharp;

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
        private int _error;
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
            if (string.IsNullOrEmpty(txtUserAcc.Text) && string.IsNullOrEmpty(txtPassAcc.Text) &&
                string.IsNullOrEmpty(txtCount.Text) && string.IsNullOrEmpty(txtPage.Text))
            {
                lblStatus.Content = "Status : Fill in the empty fields";
                return;
            }
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
            var a = txtUserAcc.Text + ":" + txtPassAcc.Text;
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
                var client = new RestClient("https://www.instagram.com")
                {
                    CookieContainer = _cookie
                };
                var request = new RestRequest(user + "/", Method.GET);
                var queryResult = client.Execute(request);
                if (queryResult.Content.Contains(@"<h2>Sorry, this page isn&#39;t available.</h2>"))
                {
                    Dispatcher.BeginInvoke(DispatcherPriority.Normal,
                        (ThreadStart)delegate { lblStatus.Content = "Status : Page Not Found"; });
                    _started = false;
                    return;
                }

                _cookie = client.CookieContainer;
                var id =
                    JObject.Parse(Regex.Match(queryResult.Content, "window._sharedData = (.*?);</script>").Groups[1]
                        .Value)["entry_data"
                    ]["ProfilePage"][0]["user"]["id"].ToString();
                Dispatcher.BeginInvoke(DispatcherPriority.Normal,
                    (ThreadStart) delegate { lblStatus.Content = "Status : Gathering..."; });
                while (_userCount > 0 && _hasNextPage)
                {
                    re2:
                    var client2 =
                        new RestClient(_url + HttpUtility.UrlEncode(_parameter).Replace("XID", id)
                                           .Replace("XTOK", _token))
                        {
                            CookieContainer = _cookie
                        };
                    var request2 = new RestRequest(Method.GET);
                    var queryResult2 = client2.Execute(request2).Content;
                    if (string.IsNullOrEmpty(queryResult2))
                    {
                        if (_error == 3)
                        {
                            Dispatcher.BeginInvoke(DispatcherPriority.Normal,
                                (ThreadStart) delegate
                                {
                                    lblStatus.Content = "Status : Check Internet Connection!";
                                    btnStart.Content = "Start";
                                });
                            _started = false;
                            return;
                        }

                        _error++;
                        goto re2;
                    }

                    _error = 0;
                    var edges = JObject.Parse(queryResult2)["data"]["user"][_edge]["edges"];
                    _token = JObject.Parse(queryResult2)["data"]["user"][_edge]["page_info"]["end_cursor"]?.ToString();
                    _hasNextPage =
                        (bool) JObject.Parse(queryResult2)["data"]["user"][_edge]["page_info"]["has_next_page"];

                    Dispatcher.BeginInvoke(
                        (ThreadStart)
                        delegate { new Thread(() => SaveEdges(edges)).Start(); });

                    _userCount -= edges.Count();
                    if (!_started)
                        break;
                }

                Dispatcher.BeginInvoke(DispatcherPriority.Normal,
                    (ThreadStart) delegate
                    {
                        lblStatus.Content = "Status : Finished";
                        btnStart.Content = "Start";
                    });
                _started = false;
            }
            catch (Exception e)
            {
                    Dispatcher.BeginInvoke(DispatcherPriority.Normal,
                        (ThreadStart) delegate { lblStatus.Content = "Error : " + e.Message; });
            }
        }

        private void SaveEdges(JToken edges)
        {
            lock (_syncLock)
            {
                File.AppendAllLines(_savePath, edges.Select(u => u["node"]["username"].ToString()));
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
                        filepath = Path.Combine(folder, $"{filename}_{number}{extension}");
                } while (File.Exists(filepath));
            }

            return filepath;
        }

        private bool Login()
        {
            try
            {
                var cookietok = new CookieContainer();
                var tokWebRequest = (HttpWebRequest) WebRequest.Create("https://www.instagram.com/");
                tokWebRequest.CookieContainer = cookietok;
                tokWebRequest.Method = "GET";
                tokWebRequest.KeepAlive = true;
                tokWebRequest.ContentType = "application/x-www-form-urlencoded";
                tokWebRequest.UserAgent =
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:49.0) Gecko/20100101 Firefox/49.0";
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
                loginRequest.UserAgent =
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:58.0) Gecko/20100101 Firefox/58.0";
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
            catch (WebException e)
            {
                if (((HttpWebResponse) e.Response).StatusCode == HttpStatusCode.BadRequest)
                    Dispatcher.BeginInvoke(DispatcherPriority.Normal,
                        (ThreadStart) delegate
                        {
                            lblStatus.Content = "Status : check your account in your browser or deactive two step verification";
                        });
                Dispatcher.BeginInvoke(DispatcherPriority.Normal,
                    (ThreadStart)delegate
                    {
                        btnStart.Content = "Start";
                    });
                _started = false;
                return false;
            }
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