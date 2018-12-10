﻿using FWT.Core.Services.EventHub;
using FWT.Core.Services.Telegram;
using FWT.Core.Services.Unique;
using FWT.Infrastructure.Telegram;
using FWT.Infrastructure.Telegram.Parsers;
using Hangfire;
using OpenTl.ClientApi;
using OpenTl.Schema;
using OpenTl.Schema.Messages;
using System;
using System.Threading.Tasks;
using static FWT.Core.Helpers.Enum;

namespace FWT.Api.Jobs
{
    public class GetMessages
    {
        private readonly ITelegramService _telegramService;
        private readonly IRandomService _randomService;
        private readonly IEventHubService _eventHub;

        public GetMessages(ITelegramService telegramService, IRandomService randomService, IEventHubService eventHub)
        {
            _telegramService = telegramService;
            _randomService = randomService;
            _eventHub = eventHub;
        }

        public async Task ForPeer(int id, PeerType peerType, string phoneHashId, int offset, int maxId)
        {
            IInputPeer peer = null;
            switch (peerType)
            {
                case (PeerType.Channal):
                    {
                        peer = new TInputPeerChannel()
                        {
                            ChannelId = id,
                        };
                        break;
                    }
                case (PeerType.Chat):
                    {
                        peer = new TInputPeerChat()
                        {
                            ChatId = id
                        };
                        break;
                    }
                case (PeerType.User):
                    {
                        peer = new TInputPeerUser()
                        {
                            UserId = id
                        };
                        break;
                    }
            }

            IClientApi client = await _telegramService.BuildAsync(phoneHashId);
            IMessages history = await TelegramRequest.Handle(() =>
            {
                return client.MessagesService.GetHistoryAsync(peer, offset, maxId, 100);
            });

            var messages = MessagesParser.Parse(history, id, peerType);
            await _eventHub.SendAsync(messages);

            if (messages.Count > 0)
            {
                BackgroundJob.Schedule<GetMessages>(
                    job => job.ForPeer(id, peerType, phoneHashId, offset + 100, 0),
                    TimeSpan.FromSeconds(_randomService.Random.Next(5, 20)));
            }
        }
    }
}