using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using VkNet;
using VkNet.Enums.Filters;
using VkNet.Enums.SafetyEnums;
using VkNet.Exception;
using VkNet.Model.Attachments;
using VkNet.Model.RequestParams;

namespace Attachments
{
    internal class Program
    {
        private static readonly VkApi Vk = new VkApi();

        public static void Main()
        {
            MainAsync().GetAwaiter().GetResult();
        }

        private static async Task MainAsync()
        {
            await Login();
        }

        private static async Task Login()
        {
            Console.WriteLine("Login:");

            var login = Console.ReadLine();

            Console.WriteLine("Pass:");

            var password = Console.ReadLine();

            await Authorize(login, password);
        }

        private static async Task Authorize(string login, string password)
        {
            try
            {
                await Vk.AuthorizeAsync(new ApiAuthParams
                {
                    Login = login,
                    Password = password,
                    ApplicationId = 6032835,
                    Settings = Settings.All
                });

                Console.Clear();
                Console.WriteLine("Login successful");

                while (true)
                {
                    var dialogId = GetDialogId();
                    await GetAttachmentsFromHistory(dialogId);
                }
            }
            catch (VkApiAuthorizationException)
            {
                Console.Clear();
                Console.WriteLine("Login error");

                await Login();
            }
            catch (Exception)
            {
                // ignored
            }
        }

        private static int GetDialogId()
        {
            Console.WriteLine("Enter link:");

            var link = Console.ReadLine();
            var uri = new Uri(link);
            var result = false;
            var dialogId = 0;
            var id = uri.Query.Substring(uri.Query.LastIndexOf('=') + 1);

            if (id.Contains('c'))
            {
                result = int.TryParse(id.Substring(id.IndexOf('c') + 1), out dialogId);
                dialogId += 2000000000;
            }
            else
            {
                result = int.TryParse(id, out dialogId);
            }

            if (result) return dialogId;

            Console.WriteLine("Wrong link!");
            GetDialogId();

            return dialogId;
        }

        private static async Task GetAttachmentsFromHistory(int dialogId)
        {
            var offset = 0;
            var count = 0;

            do
            {
                var link = $"https://api.vk.com/method/messages.getHistory?" +
                           $"offset={offset}&" +
                           $"count=200&" +
                           $"peer_id={dialogId}&" +
                           $"access_token={Vk.Token}&" +
                           $"v=5.64";

                using (var client = new WebClient())
                {
                    var jsonString = await client.DownloadStringTaskAsync(link);

                    var responseJson = JObject.Parse(jsonString).First.First;
                    count = int.Parse(responseJson["count"].ToString());
                    offset += 200;

                    var attachments = responseJson["items"]
                        .Where(m => m["attachments"] != null)
                        .SelectMany(m => m["attachments"])
                        .Where(a => a["type"].ToString() == "photo")
                        .Select(a => GetMaxSize(a["photo"]).ToString());

                    foreach (var attachment in attachments) await Download(new Uri(attachment));
                }
            } while (offset <= count);
        }

        [Obsolete]
        private static async Task GetHistoryAttachments(int dialogId)
        {
            var startFrom = "";

            do
            {
                var link = $"https://api.vk.com/method/messages.getHistoryAttachments?" +
                           $"peer_id={2000000000 + dialogId}&" +
                           $"media_type=photo&" +
                           $"start_from={startFrom}&" +
                           $"count=200&" +
                           $"access_token={Vk.Token}&" +
                           $"v=5.69";

                using (var client = new WebClient())
                {
                    var jsonString = await client.DownloadStringTaskAsync(link);

                    var responseJson = JObject.Parse(jsonString).First.First;
                    startFrom = responseJson["next_from"]?.ToString();

                    var attachments = responseJson["items"]
                        .Where(m => m["attachment"] != null)
                        .Select(m => GetMaxSize(m["attachment"]["photo"]).ToString())
                        .ToList();
                }
            } while (startFrom != null);
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

        private static Task Download(Uri link)
        {
            new WebClient().DownloadFileAsync(link, link.Segments.Last());
            return Task.CompletedTask;
        }

        private static Uri GetLink(Photo photo)
        {
            return photo.Photo2560 ??
                   photo.Photo1280 ?? photo.Photo807 ?? photo.Photo604 ?? photo.Photo130 ?? photo.Photo75;
        }

        [Obsolete]
        private static Task GetHistoryAttachmentsVkNet(int dialogId)
        {
            var startFrom = "";

            while (startFrom != null)
            {
                var attachments = Vk.Messages.GetHistoryAttachments(new MessagesGetHistoryAttachmentsParams
                {
                    Count = 200,
                    MediaType = MediaType.Photo,
                    PeerId = 2000000000 + dialogId,
                    StartFrom = startFrom
                }, out startFrom);

                var photos = attachments
                    .Select(a => (Photo) a.Attachment.Instance)
                    .Select(GetLink);
            }

            return Task.CompletedTask;
        }

        [Obsolete]
        private static List<Uri> GetAttachmentsFromHistoryVkNet(int dialogId)
        {
            var offset = 0;
            uint totalCount = 0;
            var list = new List<Uri>();

            do
            {
                var messages = Vk.Messages.GetHistory(new MessagesGetHistoryParams
                {
                    Count = 200,
                    Offset = offset,
                    PeerId = 2000000000 + dialogId
                });

                totalCount = messages.TotalCount;
                offset += messages.Messages.Count;

                list.AddRange(messages.Messages
                    .Where(m => m.Attachments.Count != 0)
                    .SelectMany(m => m.Attachments)
                    .Where(a => a.Type == typeof(Photo))
                    .Select(a => (Photo) a.Instance)
                    .Select(GetLink));
            } while (offset <= totalCount);

            return list;
        }
    }
}