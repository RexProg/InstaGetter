using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Newtonsoft.Json.Linq;

namespace InstaGetter
{
    /// <summary>
    ///     Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        private readonly string _data =
            "q=ig_user(258819714)+%7B%0A++followed_by.after(|O|%2C+20)+%7B%0A++++page_info+%7B%0A++++++end_cursor%2C%0A++++++has_next_page%0A++++%7D%2C%0A++++nodes+%7B%0A++++++username%0A++++%7D%0A++%7D%0A%7D%0A&ref=relationships%3A%3Afollow_list&query_id=17845270936146575";

        private CookieContainer _cookie;
        private bool _hasNextPage = true;

        private string _token =
            "AQA0pcvlYMUI4sPAyj6KLHo_VKyT3ogdR1UNYd6jY5PLRLmhyxeZ3FcrvoYD9bvRMj3rtKZGaP6ASr37leFh0Z6gzea_OiipWKdfOb0wVJhdCQ";

        private int _userCount;
        private string _acc;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void btnStart_Click(object sender, RoutedEventArgs e)
        {
            int c = int.Parse(txtCount.Text);
            string p = txtPage.Text;
            string a = txtAcc.Text;
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
            _token =
                "#$$Instagram$$#";
            if (!Login())
                return;
            Getter(page);
        }

        private void txtCount_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!int.TryParse(((TextBox) sender).Text, out int a))
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
                var streamReader2 = new StreamReader(httpWebResponse.GetResponseStream(),
                    Encoding.GetEncoding(httpWebResponse.CharacterSet));
                var str = streamReader2.ReadToEnd();
                var match = Regex.Match(str, "\"csrf_token\": \"(.*?)\"");
                var token = match.Groups[1].ToString();
                var id =
                    JObject.Parse(Regex.Match(str, "window._sharedData = (.*?);</script>").Groups[1].Value)["entry_data"
                    ]["ProfilePage"][0]["user"]["id"].ToString();
                httpWebResponse.Close();
                Dispatcher.BeginInvoke(DispatcherPriority.Normal,
                    (ThreadStart) delegate { lblStatus.Content = "Status : Leaching..."; });
                while (_userCount > 0 && _hasNextPage)
                {
                    byte[] bytes;
                    if (_token == "#$$Instagram$$#")
                        bytes =
                            new ASCIIEncoding().GetBytes(_data.Replace(".after(|O|%2C+", ".first(")
                                .Replace("258819714", id));
                    else
                        bytes = new ASCIIEncoding().GetBytes(_data.Replace("|O|", _token).Replace("258819714", id));
                    var httpWebRequest2 = (HttpWebRequest) WebRequest.Create("https://www.instagram.com/query/");
                    httpWebRequest2.Headers.Add("X-CSRFToken", token);
                    httpWebRequest2.Headers.Add("X-Instagram-AJAX", "1");
                    httpWebRequest2.CookieContainer = _cookie;
                    httpWebRequest2.Headers.Add("X-Requested-With", "XMLHttpRequest");
                    httpWebRequest2.Method = "POST";
                    httpWebRequest2.KeepAlive = true;
                    httpWebRequest2.ContentType = "application/x-www-form-urlencoded";
                    httpWebRequest2.Referer = "https://www.instagram.com/" + user;
                    httpWebRequest2.UserAgent =
                        "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:49.0) Gecko/20100101 Firefox/49.0";
                    httpWebRequest2.ContentLength = bytes.Length;
                    var requestStream = httpWebRequest2.GetRequestStream();
                    requestStream.Write(bytes, 0, bytes.Length);
                    requestStream.Close();
                    var httpWebResponse2 = (HttpWebResponse) httpWebRequest2.GetResponse();
                    var streamReader3 = new StreamReader(httpWebResponse2.GetResponseStream());
                    var text = streamReader3.ReadToEnd();
                    _token = JObject.Parse(text)["followed_by"]["page_info"]["end_cursor"]?.ToString();
                    var rgx = Regex.Matches(text, "{\"username\": \"(.*?)\"}");
                    if (JObject.Parse(text)["followed_by"].Count() < 2)
                    {
                        Dispatcher.BeginInvoke(DispatcherPriority.Normal,
                            (ThreadStart) delegate { lblStatus.Content = "Status : Error..."; });
                        return;
                    }
                    _hasNextPage = (bool) JObject.Parse(text)["followed_by"]["page_info"]["has_next_page"];
                    for (var i = 0; i < rgx.Count; i++)
                    {
                        var userr = rgx[i].Groups[1].Value;
                        _userCount--;
                        using (var sw = File.AppendText(@"InstaGetter_Usernames.txt"))
                        {
                            sw.WriteLine(userr);
                            sw.Close();
                        }
                    }
                }
                Dispatcher.BeginInvoke(DispatcherPriority.Normal,
                    (ThreadStart) delegate { lblStatus.Content = "Status : Finished"; });
            }
            catch (WebException e)
            {
                if (((HttpWebResponse) e.Response).StatusCode.ToString() == "429")
                {

                    Dispatcher.Invoke(DispatcherPriority.Normal,
                        (ThreadStart) delegate { lblStatus.Content = "Status : Wait for 4 min"; });
                    Thread.Sleep(60000);
                    Dispatcher.Invoke(DispatcherPriority.Normal,
                        (ThreadStart)delegate { lblStatus.Content = "Status : Wait for 3 min"; });
                    Thread.Sleep(60000);
                    Dispatcher.Invoke(DispatcherPriority.Normal,
                        (ThreadStart)delegate { lblStatus.Content = "Status : Wait for 2 min"; });
                    Thread.Sleep(60000);
                    Dispatcher.Invoke(DispatcherPriority.Normal,
                        (ThreadStart)delegate { lblStatus.Content = "Status : Wait for 1 min"; });
                    Thread.Sleep(60000);
                    goto re;
                }
                if (((HttpWebResponse) e.Response).StatusCode == HttpStatusCode.BadRequest)
                {
                    Dispatcher.BeginInvoke(DispatcherPriority.Normal,
                        (ThreadStart) delegate { lblStatus.Content = "Status : Finished"; });
                }
                if (((HttpWebResponse)e.Response).StatusCode == HttpStatusCode.NotFound)
                {
                    Dispatcher.BeginInvoke(DispatcherPriority.Normal,
                        (ThreadStart)delegate { lblStatus.Content = "Status : Page Not Found"; });
                }
            }
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
            var streamReader2 = new StreamReader(tokWebResponse.GetResponseStream(),
                Encoding.GetEncoding(tokWebResponse.CharacterSet));
            var str = streamReader2.ReadToEnd();
            cookietok.Add(tokWebResponse.Cookies);
            var match = Regex.Match(str, "\"csrf_token\": \"(.*?)\"");
            var token = match.Groups[1].ToString();

            tokWebResponse.Close();
            var bytes =
                new ASCIIEncoding().GetBytes("username=" + _acc.Split(':')[0] + "&password=" + _acc.Split(':')[1]);
            var httpWebRequest2 = (HttpWebRequest) WebRequest.Create("https://www.instagram.com/accounts/login/ajax/");
            httpWebRequest2.CookieContainer = cookietok;
            httpWebRequest2.Headers.Add("X-CSRFToken", token);
            httpWebRequest2.Headers.Add("X-Instagram-AJAX", "1");
            httpWebRequest2.Headers.Add("X-Requested-With", "XMLHttpRequest");
            httpWebRequest2.Method = "POST";
            httpWebRequest2.KeepAlive = true;
            httpWebRequest2.ContentType = "application/x-www-form-urlencoded";
            httpWebRequest2.Referer = "https://www.instagram.com/";
            httpWebRequest2.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:49.0) Gecko/20100101 Firefox/49.0";
            httpWebRequest2.ContentLength = bytes.Length;
            var requestStream = httpWebRequest2.GetRequestStream();
            requestStream.Write(bytes, 0, bytes.Length);
            requestStream.Close();
            var httpWebResponse2 = (HttpWebResponse) httpWebRequest2.GetResponse();
            cookietok.Add(httpWebResponse2.Cookies);
            _cookie = cookietok;
            var streamReader3 = new StreamReader(httpWebResponse2.GetResponseStream());
            var text = streamReader3.ReadToEnd();
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

        private void btnAbout_Click(object sender, RoutedEventArgs e)
        {
            new About().Show();
        }
    }
}