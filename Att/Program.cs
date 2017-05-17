using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Net.Http;
using VkNet;
using System.Net;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Threading;
using System.Diagnostics;

namespace Att
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;

            Login();
        }

        private static void Login()
        {
            VkApi vk = new VkApi();

            Console.WriteLine("Login:");

            string login = Console.ReadLine();

            Console.WriteLine("Pass:");

            string password = Console.ReadLine();

            try
            {
                vk.Authorize(new ApiAuthParams()
                {
                    ApplicationId = 6032835,
                    Login = login,
                    Password = password,
                    Settings = VkNet.Enums.Filters.Settings.All
                });

                Console.Clear();
                Console.WriteLine("Login successful");
                Dialog(vk);
            }
            catch (Exception e)
            {
                Console.WriteLine("Login error");
                Login();
            }
        }

        private static void Dialog(VkApi vk)
        {
            Console.WriteLine("Enter link:");

            string link = Console.ReadLine();
            try
            {
                int id = int.Parse(link.Substring(link.LastIndexOf('c') + 1));
                Download(vk.Token, id);
                Console.Clear();
                Dialog(vk);
            }
            catch (Exception e)
            {
                Console.Clear();
                Console.WriteLine("Wrong link");
                Dialog(vk);
            }
        }

        private static void Download(string token, int id)
        {
            int offset = 0;
            int count = 0;
            List<Uri> linkList = new List<Uri>();

            do
            {
                Stopwatch sw = new Stopwatch();
                sw.Start();
                linkList = linkList.Concat(GetAttachments(token, id, ref offset, out count)).ToList();
                sw.Stop();
                if (sw.ElapsedMilliseconds < 334)
                {
                    Thread.Sleep((int)(334 - sw.ElapsedMilliseconds));
                }
                Console.Clear();
                Console.WriteLine("{0}/{1} messages", offset, count);
                Console.WriteLine("{0} files", linkList.Count);
                Console.WriteLine("{0}", sw.ElapsedMilliseconds);
            } while (offset < count);

            string path = Path.Combine(Directory.GetCurrentDirectory(), "Downloads/");
            Directory.CreateDirectory(path);
            Done = 0;
            Count = linkList.Count;

            Task.WaitAll(linkList.Select(link => DownloadFileAsync(link, path)).ToArray());
        }

        private static int Done;
        private static int Count;

        private static async Task DownloadFileAsync(Uri link, string path)
        {
            try
            {
                using (WebClient client = new WebClient())
                {
                    await client.DownloadFileTaskAsync(link, path + link.Segments.Last());
                    Done++;
                    Console.Clear();
                    Console.WriteLine("{0}/{1} fiels downloaded", Done, Count);
                }
            }
            catch (Exception)
            {
            }
        }

        private static IEnumerable<Uri> GetAttachments(string token, int id, ref int offset, out int count)
        {
            WebResponse res = WebRequest
                .CreateHttp($"https://api.vk.com/method/messages.getHistory?" +
                $"offset={offset}&" +
                $"count=200&" +
                $"peer_id=20000000{id}&" +
                $"access_token={token}&" +
                $"v=5.64")
                .GetResponse();

            using (StreamReader sr = new StreamReader(res.GetResponseStream()))
            {
                JToken resJSON = JObject.Parse(sr.ReadToEnd()).First.First;
                count = int.Parse(resJSON["count"].ToString());
                offset += 200;

                IEnumerable<Uri> result = new List<Uri>();

                var attachments = resJSON["items"]
                    .Where(o => o["attachments"] != null)
                    .SelectMany(o => o["attachments"])
                    .ToList();

                result = result.Concat(GetLinks(attachments));

                result = result.Concat(
                    GetLinks(
                        attachments
                        .Where(attachment => attachment.First.First.ToString() == "wall")
                        .Where(attachment => attachment.Last.First["attachments"] != null)
                        .Select(attachment => attachment.Last.First["attachments"].First)));

                return result;
            }
        }

        private static IEnumerable<Uri> GetLinks(IEnumerable<JToken> attachments)
        {
            return attachments
                .Where(attachment => attachment["type"].ToString() == "photo")
                .Select(attachment => GetMaxSize(attachment.Last.First).ToString())
                .Where(attachment => attachment != "False")
                .Select(attachment => new Uri(attachment));
        }

        private static JToken GetMaxSize(JToken json)
        {
            return json["photo_2560"] ??
                json["photo_1280"] ??
                json["photo_807"] ??
                json["photo_604"] ??
                json["photo_130"] ??
                json["photo_75"];
        }

        //private static void Download(VkApi vk, int id)
        //{
        //    int count = 0;
        //    int offset = 0;
        //    List<Uri> linkList = new List<Uri>();

        //    do
        //    {
        //        var history = vk.Messages.GetHistory(new VkNet.Model.RequestParams.MessagesGetHistoryParams()
        //        {
        //            Count = 200,
        //            Offset = offset,
        //            PeerId = 2000000000 + id
        //        });
        //        count = (int)history.TotalCount;
        //        offset += 200;

        //        linkList = linkList.Concat(history.Messages
        //            .Where(message => message.Attachments.Count != 0)
        //            .SelectMany(message => message.Attachments)
        //            .Where(attachment => attachment.Type == typeof(VkNet.Model.Attachments.Photo))
        //            .Select(attachment => GetLink((VkNet.Model.Attachments.Photo)attachment.Instance)))
        //            .ToList();

        //        Console.Clear();
        //        Console.WriteLine("{0}/{1} messages", offset, count);
        //        Console.WriteLine("{0} files", linkList.Count);
        //    } while (offset < count);
        //}

        //private static Uri GetLink(VkNet.Model.Attachments.Photo photo)
        //{
        //    return photo.Photo2560 ?? photo.Photo1280 ?? photo.Photo807 ?? photo.Photo604 ?? photo.Photo130 ?? photo.Photo75;
        //}




    }
}

