﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Coremero.Commands;
using Coremero.Messages;
using Coremero.Plugin.Classic.TumblrJson;
using Coremero.Storage;
using Newtonsoft.Json;
using Coremero.Utilities;

namespace Coremero.Plugin.Classic
{
    public class Tumblr : IPlugin, IDisposable
    {
        private readonly string TUMBLR_API_KEY;

        private HttpClient _httpClient = new HttpClient();

        public Tumblr(ICredentialStorage credentialStorage)
        {
            TUMBLR_API_KEY = credentialStorage.GetKey("tumblr", "fuiKNFp9vQFvjLNvx4sUwti4Yb5yGutBN4Xh10LXZhhRKjWlV4"); // Public testing API key from Tumblr.
        }

        private async Task<Stream> GetRandomTumblrImage(string tumblrUsername)
        {
            List<Photo> photos = new List<Photo>();
            for (int i = 0; i < 40; i += 20)
            {
                string blogJson = await _httpClient.GetStringAsync($"http://api.tumblr.com/v2/blog/{tumblrUsername}.tumblr.com/posts?api_key={TUMBLR_API_KEY}&type=photo&offset={i}");
                var root = JsonConvert.DeserializeObject<Rootobject>(blogJson);
                photos.AddRange(root.response.posts.SelectMany(x => x.photos));
            }

            var imageUrl = photos.GetRandom().original_size.url;

            // Store image in RAM and pass back.
            MemoryStream ms = new MemoryStream();
            using (Stream httpImageStream = await _httpClient.GetStreamAsync(imageUrl))
            {
                httpImageStream.CopyTo(ms);
            }
            ms.Seek(0, SeekOrigin.Begin);
            return ms;
        }

        [Command("homero")]
        public async Task<IMessage> Homero(IInvocationContext context, IMessage message)
        {
            return Message.Create(message.Text?.TrimCommand(), new StreamAttachment(await GetRandomTumblrImage("simpsons-latino"), "homero.jpg"));
        }

        [Command("dog")]
        public async Task<IMessage> Dog(IInvocationContext context, IMessage message)
        {
            return Message.Create(message.Text?.TrimCommand(), new StreamAttachment(await GetRandomTumblrImage("goodassdog"), "good_pupper.jpg"));
        }

        #region Business Titles

        List<string> _businessTitles = new List<string>()
        {
            "CEO",
            "CFO",
            "HR Strong Boy",
            "Executive Pillow Man",
            "Quake Live Developer",
            "Senior Executive Backend Engineer",
            "Sad Full Stack Lad",
            "Hard Working Middle Management Sad Sack",
            "Child of CEO",
            "Nepotism Hire"
        };

        #endregion

        [Command("ceo")]
        public async Task<IMessage> RealBusinessMan(IInvocationContext context, IMessage message)
        {
            string outputText = message.Text?.TrimCommand();
            if (string.IsNullOrEmpty(outputText))
            {
                IUser randomUser = context.Channel?.Users.GetRandom();
                outputText = $"{_businessTitles.GetRandom()} {randomUser?.Name} hard at work.";
            }
            return Message.Create(outputText, new StreamAttachment(await GetRandomTumblrImage("realbusinessmen"), "white_male_over_50.jpg"));
        }


        public void Dispose()
        {
            _httpClient.Dispose();
        }
    }
}
